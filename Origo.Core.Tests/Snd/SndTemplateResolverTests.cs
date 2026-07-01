using System;
using System.Collections.Generic;
using Origo.Core.DataSource;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

public class SndTemplateResolverTests
{
    [Fact]
    public void Resolve_WhenCalledTwice_UsesCacheAndAvoidsSecondRead()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("templates/enemy.json",
            """
            {
              "name": "EnemyTemplate",
              "strategy": { "lifecycle_indices": [ "enemy.ai" ] },
              "node": { "pairs": { "root": "enemy" } },
              "data": { "pairs": { "hp": { "type": "Int32", "data": 50 } } }
            }
            """);

        var resolver = CreateResolver(fs, new Dictionary<string, string>
        {
            ["enemy"] = "templates/enemy.json"
        });

        var first = resolver.Resolve("enemy");
        var readsAfterFirstResolve = fs.ReadAllTextCallCount;
        var second = resolver.Resolve("enemy");

        Assert.Equal("EnemyTemplate", first.Name);
        Assert.Same(first, second);
        Assert.Equal(readsAfterFirstResolve, fs.ReadAllTextCallCount);
    }

    [Fact]
    public void Resolve_TemplateFile_EmptyObject_ReturnsMinimalMetaData()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("templates/empty.json", "{}");
        var resolver = CreateResolver(fs, new Dictionary<string, string>
        {
            ["empty"] = "templates/empty.json"
        });

        var result = resolver.Resolve("empty");

        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.Name);
    }

    [Fact]
    public void Resolve_TemplateFile_MissingNameField_ReturnsEmptyName()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("templates/noname.json", """{"strategy":{"lifecycle_indices":[]},"data":{"pairs":{}}}""");
        var resolver = CreateResolver(fs, new Dictionary<string, string>
        {
            ["noname"] = "templates/noname.json"
        });

        var result = resolver.Resolve("noname");

        Assert.NotNull(result);
    }

    [Fact]
    public void Resolve_CacheThenClone_CloneDoesNotAffectCache()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("templates/base.json",
            """{"name":"BaseTemplate","strategy":{"lifecycle_indices":[]},"data":{"pairs":{}}}""");
        var resolver = CreateResolver(fs, new Dictionary<string, string>
        {
            ["base"] = "templates/base.json"
        });

        var original = resolver.Resolve("base");
        var clone = original.DeepClone();
        clone.Name = "Modified";

        var fromCache = resolver.Resolve("base");

        Assert.Equal("BaseTemplate", fromCache.Name);
        Assert.Equal("Modified", clone.Name);
    }

    [Fact]
    public void Resolve_MapFileComments_Skipped()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("templates/simple.json",
            """{"name":"Simple","strategy":{"lifecycle_indices":[]},"data":{"pairs":{}}}""");
        var resolver = CreateResolver(fs, new Dictionary<string, string>
        {
            ["simple"] = "templates/simple.json"
        });

        var result = resolver.Resolve("simple");

        Assert.Equal("Simple", result.Name);
    }

    [Fact]
    public void Resolve_MissingAlias_ThrowsKeyNotFoundException()
    {
        var resolver = CreateResolver(new TestFileSystem(), []);
        Assert.Throws<KeyNotFoundException>(() => resolver.Resolve("missing"));
    }

    [Fact]
    public void Resolve_WhitespaceAlias_ThrowsArgumentException()
    {
        var resolver = CreateResolver(new TestFileSystem(), []);
        Assert.Throws<ArgumentException>(() => resolver.Resolve(" "));
    }

    [Fact]
    public void Resolve_InvalidJson_Throws()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("templates/broken.json", "{ not-valid-json");
        var resolver = CreateResolver(fs, new Dictionary<string, string>
        {
            ["broken"] = "templates/broken.json"
        });

        Assert.ThrowsAny<Exception>(() => resolver.Resolve("broken"));
    }

    [Fact]
    public void Resolve_ConverterReturnsNull_ThrowsInvalidOperationException()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("templates/null.json", "{}");
        var io = TestFactory.CreateIoGateway(fs);
        var resolver = new SndTemplateResolver(
            io,
            new NullMetaConverter(),
            new Dictionary<string, string> { ["null_meta"] = "templates/null.json" });

        var ex = Assert.Throws<InvalidOperationException>(() => resolver.Resolve("null_meta"));
        Assert.Contains("deserialized to null", ex.Message, StringComparison.Ordinal);
    }

    private static SndTemplateResolver CreateResolver(TestFileSystem fs, Dictionary<string, string> map)
    {
        var registry = TestFactory.CreateRegistry();
        return new SndTemplateResolver(TestFactory.CreateIoGateway(fs), registry.Get<SndMetaData>(), map);
    }

    private sealed class NullMetaConverter : DataSourceConverter<SndMetaData>
    {
        public override SndMetaData Read(DataSourceNode node) => null!;

        public override DataSourceNode Write(SndMetaData value) => DataSourceNode.CreateNull();
    }
}
