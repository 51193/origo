using System;
using Origo.Core.Abstractions.Snd;
using Origo.Core.Blackboard;
using Origo.Core.DataSource;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     ISndFileAccess behavior tests on SndContext: correct / error / boundary paths.
///     All file I/O uses TestFileSystem (in-memory), no real disk operations.
/// </summary>
public class SndContextFileAccessTests
{
    // ── Helpers ──

    private static SndContext CreateContext(out TestFileSystem fs, out TestLogger logger)
    {
        logger = new TestLogger();
        var host = new TestSndSceneHost();
        var tm = new TypeStringMapping();
        var bb = new Blackboard.Blackboard();
        fs = new TestFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, tm, bb, fs);
        return new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial", "entry.json"));
    }

    private static ISndFileAccess AsFileAccess(SndContext ctx) => ctx;

    // ── Correct path: ReadFile ──

    [Fact]
    public void ReadFile_ReadsJsonAndReturnsParsedTree()
    {
        var ctx = CreateContext(out var fs, out _);
        fs.SeedFile("data/sample.json", """{"key":"value","num":42}""");

        var node = AsFileAccess(ctx).ReadFile("data/sample.json");

        Assert.Equal("value", node["key"].AsString());
        Assert.Equal(42, node["num"].AsInt());
    }

    [Fact]
    public void ReadFile_ReadsMapFileAndReturnsParsedTree()
    {
        var ctx = CreateContext(out var fs, out _);
        fs.SeedFile("data/config.map", "name: hero\nhp: 100");

        var node = AsFileAccess(ctx).ReadFile("data/config.map");

        Assert.Equal("hero", node["name"].AsString());
        Assert.Equal("100", node["hp"].AsString());
    }

    [Fact]
    public void ReadFile_ReadsNestedJsonStructure()
    {
        var ctx = CreateContext(out var fs, out _);
        fs.SeedFile("data/nested.json", """{"outer":{"inner":"deep","list":[1,2,3]}}""");

        var node = AsFileAccess(ctx).ReadFile("data/nested.json");

        Assert.Equal("deep", node["outer"]["inner"].AsString());
        Assert.Equal(3, node["outer"]["list"].Count);
        Assert.Equal(3, node["outer"]["list"][2].AsInt());
    }

    // ── Correct path: WriteFile ──

    [Fact]
    public void WriteFile_WritesNodeAndCanBeReadBack()
    {
        var ctx = CreateContext(out var fs, out _);
        var node = DataSourceNode.CreateObject();
        node.Add("name", DataSourceNode.CreateString("test"));
        node.Add("value", DataSourceNode.CreateNumber(42));

        AsFileAccess(ctx).WriteFile("output/data.json", node);

        Assert.True(fs.Exists("output/data.json"));
        var readBack = AsFileAccess(ctx).ReadFile("output/data.json");
        Assert.Equal("test", readBack["name"].AsString());
        Assert.Equal(42, readBack["value"].AsInt());
    }

    [Fact]
    public void WriteFile_WithOverwriteTrue_OverwritesExistingFile()
    {
        var ctx = CreateContext(out var fs, out _);
        fs.SeedFile("output/data.json", """{"old":true}""");

        var node = DataSourceNode.CreateObject();
        node.Add("new", DataSourceNode.CreateString("replaced"));
        AsFileAccess(ctx).WriteFile("output/data.json", node, overwrite: true);

        var readBack = AsFileAccess(ctx).ReadFile("output/data.json");
        Assert.Equal("replaced", readBack["new"].AsString());
        Assert.False(readBack.ContainsKey("old"));
    }

    [Fact]
    public void WriteFile_WithOverwriteFalse_ThrowsWhenFileExists()
    {
        var ctx = CreateContext(out var fs, out _);
        fs.SeedFile("output/data.json", """{"existing":true}""");

        var node = DataSourceNode.CreateObject();
        node.Add("key", DataSourceNode.CreateString("val"));

        var ex = Assert.Throws<System.IO.IOException>(() =>
            AsFileAccess(ctx).WriteFile("output/data.json", node, overwrite: false));
        Assert.Contains("output/data.json", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteFile_WritesArrayNode()
    {
        var ctx = CreateContext(out _, out _);
        var array = DataSourceNode.CreateArray();
        array.Add(DataSourceNode.CreateString("a"));
        array.Add(DataSourceNode.CreateString("b"));
        array.Add(DataSourceNode.CreateString("c"));

        AsFileAccess(ctx).WriteFile("output/array.json", array);

        var readBack = AsFileAccess(ctx).ReadFile("output/array.json");
        Assert.Equal(3, readBack.Count);
        Assert.Equal("b", readBack[1].AsString());
    }

    // ── Correct path: FileExists ──

    [Fact]
    public void FileExists_ReturnsTrueForExistingFile()
    {
        var ctx = CreateContext(out var fs, out _);
        fs.SeedFile("data/exists.json", "{}");

        Assert.True(AsFileAccess(ctx).FileExists("data/exists.json"));
    }

    [Fact]
    public void FileExists_ReturnsFalseForNonexistentFile()
    {
        var ctx = CreateContext(out _, out _);
        Assert.False(AsFileAccess(ctx).FileExists("data/does_not_exist.json"));
    }

    [Fact]
    public void FileExists_ReturnsFalseForEmptyPath()
    {
        var ctx = CreateContext(out _, out _);
        // Empty path is invalid and gateway throws ArgumentException
        Assert.Throws<ArgumentException>(() =>
            AsFileAccess(ctx).FileExists(""));
    }

    // ── Correct path: ReadObject / WriteObject ──

    [Fact]
    public void ReadObject_DeserializesJsonToTypedPrimitive()
    {
        var ctx = CreateContext(out var fs, out _);
        fs.SeedFile("data/int.json", """42""");

        var value = AsFileAccess(ctx).ReadObject<int>("data/int.json");

        Assert.Equal(42, value);
    }

    [Fact]
    public void ReadObject_DeserializesJsonToString()
    {
        var ctx = CreateContext(out var fs, out _);
        fs.SeedFile("data/str.json", "\"hello world\"");

        var value = AsFileAccess(ctx).ReadObject<string>("data/str.json");

        Assert.Equal("hello world", value);
    }

    [Fact]
    public void WriteObject_SerializesTypedValueAndCanBeReadBack()
    {
        var ctx = CreateContext(out var fs, out _);

        AsFileAccess(ctx).WriteObject("output/typed.json", 123);

        Assert.True(fs.Exists("output/typed.json"));
        var readBack = AsFileAccess(ctx).ReadObject<int>("output/typed.json");
        Assert.Equal(123, readBack);
    }

    [Fact]
    public void WriteObject_WithOverwrite_ReplacesExisting()
    {
        var ctx = CreateContext(out var fs, out _);
        fs.SeedFile("output/typed.json", """100""");

        AsFileAccess(ctx).WriteObject("output/typed.json", 999, overwrite: true);

        var readBack = AsFileAccess(ctx).ReadObject<int>("output/typed.json");
        Assert.Equal(999, readBack);
    }

    [Fact]
    public void ReadWriteObject_RoundTrip_PreservesBool()
    {
        var ctx = CreateContext(out _, out _);

        AsFileAccess(ctx).WriteObject("output/bool.json", true);
        var readBack = AsFileAccess(ctx).ReadObject<bool>("output/bool.json");

        Assert.True(readBack);
    }

    [Fact]
    public void ReadWriteObject_RoundTrip_PreservesDouble()
    {
        var ctx = CreateContext(out _, out _);

        AsFileAccess(ctx).WriteObject("output/double.json", 3.14159);
        var readBack = AsFileAccess(ctx).ReadObject<double>("output/double.json");

        Assert.Equal(3.14159, readBack);
    }

    // ── Error path ──

    [Fact]
    public void ReadFile_ThrowsForNonexistentFile()
    {
        var ctx = CreateContext(out _, out _);

        Assert.ThrowsAny<Exception>(() =>
            AsFileAccess(ctx).ReadFile("nonexistent/file.json"));
    }

    [Fact]
    public void ReadFile_ThrowsForNullPath()
    {
        var ctx = CreateContext(out _, out _);

        Assert.Throws<ArgumentException>(() =>
            AsFileAccess(ctx).ReadFile(null!));
    }

    [Fact]
    public void ReadFile_ThrowsForInvalidJson()
    {
        var ctx = CreateContext(out var fs, out _);
        fs.SeedFile("data/bad.json", "{broken");

        var node = AsFileAccess(ctx).ReadFile("data/bad.json");
        // JSON parsing is lazy — exception occurs on access, not on Read
        Assert.ThrowsAny<Exception>(() => { var _ = node.AsString(); });
    }

    [Fact]
    public void WriteFile_ThrowsForNullNode()
    {
        var ctx = CreateContext(out _, out _);

        Assert.Throws<ArgumentNullException>(() =>
            AsFileAccess(ctx).WriteFile("output.json", null!));
    }

    [Fact]
    public void ReadObject_ThrowsForNonexistentFile()
    {
        var ctx = CreateContext(out _, out _);

        Assert.ThrowsAny<Exception>(() =>
            AsFileAccess(ctx).ReadObject<int>("nonexistent/file.json"));
    }

    [Fact]
    public void ReadObject_ThrowsForTypeMismatch()
    {
        var ctx = CreateContext(out var fs, out _);
        fs.SeedFile("data/str.json", "\"not a number\"");

        Assert.ThrowsAny<Exception>(() =>
            AsFileAccess(ctx).ReadObject<int>("data/str.json"));
    }

    // ── Boundary path ──

    [Fact]
    public void WriteFile_EmptyObject_RoundTrip()
    {
        var ctx = CreateContext(out _, out _);
        var empty = DataSourceNode.CreateObject();

        AsFileAccess(ctx).WriteFile("output/empty.json", empty);
        var readBack = AsFileAccess(ctx).ReadFile("output/empty.json");

        Assert.NotNull(readBack);
        Assert.False(readBack.ContainsKey("anything"));
    }

    [Fact]
    public void WriteFile_NullValueNode_RoundTrip()
    {
        var ctx = CreateContext(out _, out _);
        var node = DataSourceNode.CreateObject();
        node.Add("nullable", DataSourceNode.CreateNull());

        AsFileAccess(ctx).WriteFile("output/nullable.json", node);
        var readBack = AsFileAccess(ctx).ReadFile("output/nullable.json");

        Assert.True(readBack["nullable"].IsNull);
    }

    [Fact]
    public void WriteFile_BooleanValues_RoundTrip()
    {
        var ctx = CreateContext(out _, out _);
        var node = DataSourceNode.CreateObject();
        node.Add("flag_true", DataSourceNode.CreateBoolean(true));
        node.Add("flag_false", DataSourceNode.CreateBoolean(false));

        AsFileAccess(ctx).WriteFile("output/bools.json", node);
        var readBack = AsFileAccess(ctx).ReadFile("output/bools.json");

        Assert.True(readBack["flag_true"].AsBool());
        Assert.False(readBack["flag_false"].AsBool());
    }

    [Fact]
    public void FileAccess_IsAccessibleThroughRoleInterface()
    {
        var ctx = CreateContext(out var fs, out _);
        fs.SeedFile("data/sample.json", """{"x":1}""");

        ISndFileAccess fa = ctx;
        Assert.True(fa.FileExists("data/sample.json"));
        var node = fa.ReadFile("data/sample.json");
        Assert.Equal(1, node["x"].AsInt());
    }
}
