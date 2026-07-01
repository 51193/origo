using System;
using Origo.Core.Abstractions.Snd;
using Origo.Core.DataSource;
using Origo.Core.Snd;
using Origo.Core.Tests.TestSupport;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     ISndFileAccess behavior tests on StrategyTestContext (memory-backed file I/O for strategy unit tests).
/// </summary>
public class StrategyTestContextFileAccessTests
{
    private static ISndFileAccess AsFileAccess(StrategyTestContext ctx) =>
        // StrategyTestContext is internal; ISndContext provides ISndFileAccess.
        (ISndFileAccess)(ISndContext)ctx;

    // ── FileExists ──

    [Fact]
    public void FileExists_ReturnsFalseForNonexistentFile()
    {
        var ctx = new StrategyTestContext();
        var fa = AsFileAccess(ctx);

        Assert.False(fa.FileExists("data/anything.json"));
    }

    [Fact]
    public void FileExists_ReturnsTrueAfterWrite()
    {
        var ctx = new StrategyTestContext();
        var fa = AsFileAccess(ctx);

        fa.WriteFile("data/created.json", DataSourceNode.CreateObject());

        Assert.True(fa.FileExists("data/created.json"));
    }

    // ── ReadFile / WriteFile (DataSourceNode) ──

    [Fact]
    public void WriteThenReadFile_RoundTrip_PreservesData()
    {
        var ctx = new StrategyTestContext();
        var fa = AsFileAccess(ctx);

        var original = DataSourceNode.CreateObject();
        original.Add("name", DataSourceNode.CreateString("test"));
        original.Add("score", DataSourceNode.CreateNumber(100));
        fa.WriteFile("data/save.json", original);

        var readBack = fa.ReadFile("data/save.json");

        Assert.Equal("test", readBack["name"].AsString());
        Assert.Equal(100, readBack["score"].AsInt());
    }

    [Fact]
    public void WriteThenReadFile_RoundTrip_ForArrayNode()
    {
        var ctx = new StrategyTestContext();
        var fa = AsFileAccess(ctx);

        var array = DataSourceNode.CreateArray();
        array.Add(DataSourceNode.CreateNumber(1));
        array.Add(DataSourceNode.CreateNumber(2));
        fa.WriteFile("data/array.json", array);

        var readBack = fa.ReadFile("data/array.json");

        Assert.Equal(2, readBack.Count);
        Assert.Equal(2, readBack[1].AsInt());
    }

    [Fact]
    public void WriteFile_OverwriteTrue_ReplacesExisting()
    {
        var ctx = new StrategyTestContext();
        var fa = AsFileAccess(ctx);

        fa.WriteFile("data/file.json", DataSourceNode.CreateObject().Add("v",
            DataSourceNode.CreateString("old")));
        fa.WriteFile("data/file.json", DataSourceNode.CreateObject().Add("v",
            DataSourceNode.CreateString("new")), overwrite: true);

        var readBack = fa.ReadFile("data/file.json");
        Assert.Equal("new", readBack["v"].AsString());
    }

    [Fact]
    public void WriteFile_OverwriteFalse_ThrowsWhenFileExists()
    {
        var ctx = new StrategyTestContext();
        var fa = AsFileAccess(ctx);

        fa.WriteFile("data/existing.json", DataSourceNode.CreateObject());

        var ex = Assert.Throws<System.IO.IOException>(() =>
            fa.WriteFile("data/existing.json", DataSourceNode.CreateObject(), overwrite: false));
        Assert.Contains("data/existing.json", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadFile_ThrowsForNonexistentFile()
    {
        var ctx = new StrategyTestContext();
        var fa = AsFileAccess(ctx);

        Assert.ThrowsAny<Exception>(() =>
            fa.ReadFile("nonexistent/file.json"));
    }

    // ── ReadObject / WriteObject (typed) ──

    [Fact]
    public void WriteThenReadObject_RoundTrip_Int()
    {
        var ctx = new StrategyTestContext();
        var fa = AsFileAccess(ctx);

        fa.WriteObject("data/int_val.json", 42);
        var readBack = fa.ReadObject<int>("data/int_val.json");

        Assert.Equal(42, readBack);
    }

    [Fact]
    public void WriteThenReadObject_RoundTrip_String()
    {
        var ctx = new StrategyTestContext();
        var fa = AsFileAccess(ctx);

        fa.WriteObject("data/str_val.json", "hello world");
        var readBack = fa.ReadObject<string>("data/str_val.json");

        Assert.Equal("hello world", readBack);
    }

    [Fact]
    public void WriteThenReadObject_RoundTrip_Bool()
    {
        var ctx = new StrategyTestContext();
        var fa = AsFileAccess(ctx);

        fa.WriteObject("data/bool_val.json", false);
        var readBack = fa.ReadObject<bool>("data/bool_val.json");

        Assert.False(readBack);
    }

    [Fact]
    public void WriteThenReadObject_RoundTrip_Double()
    {
        var ctx = new StrategyTestContext();
        var fa = AsFileAccess(ctx);

        fa.WriteObject("data/float_val.json", 2.71828);
        var readBack = fa.ReadObject<double>("data/float_val.json");

        Assert.Equal(2.71828, readBack);
    }

    [Fact]
    public void WriteObject_OverwriteTrue_ReplacesExisting()
    {
        var ctx = new StrategyTestContext();
        var fa = AsFileAccess(ctx);

        fa.WriteObject("data/typed.json", 1);
        fa.WriteObject("data/typed.json", 2, overwrite: true);

        var readBack = fa.ReadObject<int>("data/typed.json");
        Assert.Equal(2, readBack);
    }

    [Fact]
    public void ReadObject_ThrowsForNonexistentFile()
    {
        var ctx = new StrategyTestContext();
        var fa = AsFileAccess(ctx);

        Assert.ThrowsAny<Exception>(() =>
            fa.ReadObject<int>("no/such/file.json"));
    }
}
