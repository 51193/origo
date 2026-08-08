using System;
using System.IO;
using Origo.Core.Abstractions.Snd;
using Origo.Core.Blackboard;
using Origo.Core.DataSource;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     ISndArchiveFileAccess behavior tests on SndContext: correct / error / boundary paths.
///     All file I/O uses TestMemoryFileSystem (in-memory), no real disk operations.
/// </summary>
public class SndContextArchiveFileAccessTests
{
    // ── Helpers ──

    private static SndContext CreateContext(out TestMemoryFileSystem fs, out TestLogger logger)
    {
        logger = new TestLogger();
        var host = new TestSndSceneHost();
        var tm = new TypeStringMapping();
        var bb = new Blackboard.Blackboard();
        fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, tm, bb, fs);
        return new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial", "entry.json"));
    }

    private static ISndArchiveFileAccess AsFileAccess(SndContext ctx) => ctx.ArchiveFileAccess;

    // ── Correct path: ReadFile ──

    [Fact]
    public void ReadFile_ReadsJsonFromExtraDirectory()
    {
        var ctx = CreateContext(out var fs, out _);
        fs.SeedFile("root/current/extra/data.json", """{"key":"value","num":42}""");

        var node = AsFileAccess(ctx).ReadFile("data.json");

        Assert.Equal("value", node["key"].AsString());
        Assert.Equal(42, node["num"].As<int>());
    }

    [Fact]
    public void ReadFile_ReadsMapFileFromExtraDirectory()
    {
        var ctx = CreateContext(out var fs, out _);
        fs.SeedFile("root/current/extra/config.map", "name: hero\nhp: 100");

        var node = AsFileAccess(ctx).ReadFile("config.map");

        Assert.Equal("hero", node["name"].AsString());
        Assert.Equal("100", node["hp"].AsString());
    }

    [Fact]
    public void ReadFile_ReadsNestedJsonStructure()
    {
        var ctx = CreateContext(out var fs, out _);
        fs.SeedFile("root/current/extra/nested.json", """{"outer":{"inner":"deep","list":[1,2,3]}}""");

        var node = AsFileAccess(ctx).ReadFile("nested.json");

        Assert.Equal("deep", node["outer"]["inner"].AsString());
        Assert.Equal(3, node["outer"]["list"].Count);
        Assert.Equal(3, node["outer"]["list"][2].As<int>());
    }

    // ── Correct path: WriteFile ──

    [Fact]
    public void WriteFile_WritesToExtraAndCanBeReadBack()
    {
        var ctx = CreateContext(out var fs, out _);
        var node = DataSourceNode.CreateObject();
        node.Add("name", DataSourceNode.CreateString("test"));
        node.Add("value", DataSourceNode.CreateNumber(42));

        AsFileAccess(ctx).WriteFile("output.json", node);

        Assert.True(fs.Exists("root/current/extra/output.json"));
        var readBack = AsFileAccess(ctx).ReadFile("output.json");
        Assert.Equal("test", readBack["name"].AsString());
        Assert.Equal(42, readBack["value"].As<int>());
    }

    [Fact]
    public void WriteFile_WithOverwriteTrue_OverwritesExistingFile()
    {
        var ctx = CreateContext(out var fs, out _);
        fs.SeedFile("root/current/extra/output.json", """{"old":true}""");

        var node = DataSourceNode.CreateObject();
        node.Add("new", DataSourceNode.CreateString("replaced"));
        AsFileAccess(ctx).WriteFile("output.json", node, overwrite: true);

        var readBack = AsFileAccess(ctx).ReadFile("output.json");
        Assert.Equal("replaced", readBack["new"].AsString());
        Assert.False(readBack.ContainsKey("old"));
    }

    [Fact]
    public void WriteFile_WithOverwriteFalse_ThrowsWhenFileExists()
    {
        var ctx = CreateContext(out var fs, out _);
        fs.SeedFile("root/current/extra/output.json", """{"old":true}""");

        var node = DataSourceNode.CreateObject();
        node.Add("new", DataSourceNode.CreateString("value"));

        Assert.Throws<IOException>(() =>
            AsFileAccess(ctx).WriteFile("output.json", node, overwrite: false));
    }

    [Fact]
    public void WriteFile_WritesArrayNode()
    {
        var ctx = CreateContext(out var fs, out _);
        var arr = DataSourceNode.CreateArray();
        arr.Add(DataSourceNode.CreateString("a"));
        arr.Add(DataSourceNode.CreateString("b"));
        arr.Add(DataSourceNode.CreateNumber(3));

        AsFileAccess(ctx).WriteFile("array.json", arr);

        Assert.True(fs.Exists("root/current/extra/array.json"));
        var readBack = AsFileAccess(ctx).ReadFile("array.json");
        Assert.Equal(3, readBack.Count);
        Assert.Equal("a", readBack[0].AsString());
        Assert.Equal("b", readBack[1].AsString());
        Assert.Equal(3, readBack[2].As<int>());
    }

    [Fact]
    public void WriteFile_CreatesParentDirectories()
    {
        var ctx = CreateContext(out var fs, out _);
        var node = DataSourceNode.CreateObject();
        node.Add("deep", DataSourceNode.CreateString("nested"));

        AsFileAccess(ctx).WriteFile("a/b/c/data.json", node);

        Assert.True(fs.Exists("root/current/extra/a/b/c/data.json"));
        var readBack = AsFileAccess(ctx).ReadFile("a/b/c/data.json");
        Assert.Equal("nested", readBack["deep"].AsString());
    }

    // ── Correct path: FileExists ──

    [Fact]
    public void FileExists_ReturnsTrueForExistingFile()
    {
        var ctx = CreateContext(out var fs, out _);
        fs.SeedFile("root/current/extra/data.json", """{"key":1}""");

        Assert.True(AsFileAccess(ctx).FileExists("data.json"));
    }

    [Fact]
    public void FileExists_ReturnsFalseForNonexistentFile()
    {
        var ctx = CreateContext(out _, out _);

        Assert.False(AsFileAccess(ctx).FileExists("nonexistent.json"));
    }

    // ── Correct path: ReadObject / WriteObject ──

    [Fact]
    public void ReadObject_DeserializesTypedPrimitive()
    {
        var ctx = CreateContext(out var fs, out _);
        fs.SeedFile("root/current/extra/typed.json", """42""");

        var result = AsFileAccess(ctx).ReadObject<int>("typed.json");

        Assert.Equal(42, result);
    }

    [Fact]
    public void ReadWriteObject_RoundTrip_PreservesBool()
    {
        var ctx = CreateContext(out _, out _);

        AsFileAccess(ctx).WriteObject("bool.json", true);
        var readBack = AsFileAccess(ctx).ReadObject<bool>("bool.json");

        Assert.True(readBack);
    }

    [Fact]
    public void ReadWriteObject_RoundTrip_PreservesString()
    {
        var ctx = CreateContext(out _, out _);

        AsFileAccess(ctx).WriteObject("str.json", "hello world");
        var readBack = AsFileAccess(ctx).ReadObject<string>("str.json");

        Assert.Equal("hello world", readBack);
    }

    [Fact]
    public void ReadWriteObject_RoundTrip_PreservesDouble()
    {
        var ctx = CreateContext(out _, out _);

        AsFileAccess(ctx).WriteObject("double.json", 3.14159);
        var readBack = AsFileAccess(ctx).ReadObject<double>("double.json");

        Assert.Equal(3.14159, readBack);
    }

    // ── Correct path: DeleteFile ──

    [Fact]
    public void DeleteFile_RemovesExistingFile()
    {
        var ctx = CreateContext(out var fs, out _);
        fs.SeedFile("root/current/extra/data.json", """{"key":1}""");

        AsFileAccess(ctx).DeleteFile("data.json");

        Assert.False(fs.Exists("root/current/extra/data.json"));
    }

    [Fact]
    public void FileExists_ReturnsFalseAfterDelete()
    {
        var ctx = CreateContext(out var fs, out _);
        fs.SeedFile("root/current/extra/data.json", """{"key":1}""");

        AsFileAccess(ctx).DeleteFile("data.json");

        Assert.False(AsFileAccess(ctx).FileExists("data.json"));
    }

    [Fact]
    public void DeleteFile_ThenRead_Throws()
    {
        var ctx = CreateContext(out var fs, out _);
        fs.SeedFile("root/current/extra/data.json", """{"key":1}""");
        AsFileAccess(ctx).DeleteFile("data.json");

        Assert.ThrowsAny<Exception>(() =>
            AsFileAccess(ctx).ReadFile("data.json"));
    }

    [Fact]
    public void ArchiveFileAccess_IsAccessibleThroughRoleInterface()
    {
        var ctx = CreateContext(out _, out _);
        var access = ctx.ArchiveFileAccess;

        Assert.False(access.FileExists("anything.json"));
    }

    // ── Error path ──

    [Fact]
    public void ReadFile_ThrowsForNonexistentFile()
    {
        var ctx = CreateContext(out _, out _);

        Assert.ThrowsAny<Exception>(() =>
            AsFileAccess(ctx).ReadFile("nonexistent.json"));
    }

    [Fact]
    public void ReadFile_ThrowsForPathTraversal()
    {
        var ctx = CreateContext(out _, out _);

        Assert.Throws<ArgumentException>(() =>
            AsFileAccess(ctx).ReadFile("../escape.json"));
    }

    [Fact]
    public void ReadFile_DotDotInsideFileName_IsAllowed()
    {
        // A ".." substring inside a file name is not a traversal segment and
        // must not be rejected (regression for the substring-based check).
        var ctx = CreateContext(out var fs, out _);
        fs.SeedFile("root/current/extra/v1..2.map", "k: 1");

        var node = AsFileAccess(ctx).ReadFile("v1..2.map");
        Assert.False(node.IsNull);
        Assert.Equal("1", node["k"].AsString());
    }

    [Fact]
    public void WriteFile_ThrowsForPathTraversal()
    {
        var ctx = CreateContext(out _, out _);
        var node = DataSourceNode.CreateObject();

        Assert.Throws<ArgumentException>(() =>
            AsFileAccess(ctx).WriteFile("../escape.json", node));
    }

    [Fact]
    public void DeleteFile_ThrowsForNonexistentFile()
    {
        var ctx = CreateContext(out _, out _);

        Assert.Throws<InvalidOperationException>(() =>
            AsFileAccess(ctx).DeleteFile("nonexistent.json"));
    }

    [Fact]
    public void DeleteFile_ThrowsForPathTraversal()
    {
        var ctx = CreateContext(out _, out _);

        Assert.Throws<ArgumentException>(() =>
            AsFileAccess(ctx).DeleteFile("../escape.json"));
    }

    [Fact]
    public void ReadObject_ThrowsForTypeMismatch()
    {
        var ctx = CreateContext(out var fs, out _);
        fs.SeedFile("root/current/extra/str.json", "\"not a number\"");

        Assert.ThrowsAny<Exception>(() =>
            AsFileAccess(ctx).ReadObject<int>("str.json"));
    }

    [Fact]
    public void WriteFile_ThrowsForNullNode()
    {
        var ctx = CreateContext(out _, out _);

        Assert.Throws<ArgumentNullException>(() =>
            AsFileAccess(ctx).WriteFile("output.json", null!));
    }

    // ── Boundary path ──

    [Fact]
    public void WriteFile_EmptyObject_RoundTrip()
    {
        var ctx = CreateContext(out _, out _);
        var empty = DataSourceNode.CreateObject();

        AsFileAccess(ctx).WriteFile("empty.json", empty);
        var readBack = AsFileAccess(ctx).ReadFile("empty.json");

        Assert.NotNull(readBack);
        Assert.False(readBack.ContainsKey("anything"));
    }

    [Fact]
    public void WriteFile_NullValueNode_RoundTrip()
    {
        var ctx = CreateContext(out _, out _);
        var node = DataSourceNode.CreateObject();
        node.Add("nullable", DataSourceNode.CreateNull());

        AsFileAccess(ctx).WriteFile("nullable.json", node);
        var readBack = AsFileAccess(ctx).ReadFile("nullable.json");

        Assert.True(readBack["nullable"].IsNull);
    }

    [Fact]
    public void WriteFile_BooleanValues_RoundTrip()
    {
        var ctx = CreateContext(out _, out _);
        var node = DataSourceNode.CreateObject();
        node.Add("yes", DataSourceNode.CreateBoolean(true));
        node.Add("no", DataSourceNode.CreateBoolean(false));

        AsFileAccess(ctx).WriteFile("bools.json", node);
        var readBack = AsFileAccess(ctx).ReadFile("bools.json");

        Assert.True(readBack["yes"].As<bool>());
        Assert.False(readBack["no"].As<bool>());
    }

    // ── Save/Load roundtrip ──

    [Fact]
    public void WriteFile_SurvivesSaveLoadRoundTrip()
    {
        var ctx = CreateContext(out var fs, out _);
        fs.SeedFile("entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        var node = DataSourceNode.CreateObject();
        node.Add("game_data", DataSourceNode.CreateString("persisted"));
        AsFileAccess(ctx).WriteFile("game_state.json", node);

        ctx.Save.RequestSaveGame("slot_01");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists("root/save_slot_01/extra/game_state.json"));

        ctx.Save.RequestLoadGame("slot_01");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        var readBack = AsFileAccess(ctx).ReadFile("game_state.json");
        Assert.Equal("persisted", readBack["game_data"].AsString());
    }

    // ── Error path: null / whitespace relative paths ──

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReadFile_NullOrWhitespacePath_Throws(string? path)
    {
        var ctx = CreateContext(out _, out _);

        var ex = Assert.ThrowsAny<ArgumentException>(() => AsFileAccess(ctx).ReadFile(path!));
        Assert.Equal("path", ex.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WriteFile_NullOrWhitespacePath_Throws(string? path)
    {
        var ctx = CreateContext(out _, out _);

        var ex = Assert.ThrowsAny<ArgumentException>(() => AsFileAccess(ctx).WriteFile(path!, DataSourceNode.CreateObject(), overwrite: true));
        Assert.Equal("path", ex.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DeleteFile_NullOrWhitespacePath_Throws(string? path)
    {
        var ctx = CreateContext(out _, out _);

        var ex = Assert.ThrowsAny<ArgumentException>(() => AsFileAccess(ctx).DeleteFile(path!));
        Assert.Equal("path", ex.ParamName);
    }
}
