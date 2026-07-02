using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Origo.Core.DataSource;
using Origo.Core.DataSource.Converters;
using Origo.Core.Snd.Metadata;
using Origo.Core.StateMachine;
using Xunit;

namespace Origo.Core.Tests;

public class DataSourceCodecTests
{
    // ── 7. JSON codec round-trip ──

    [Fact]
    public void JsonCodec_RoundTrip_ComplexTree()
    {
        var codec = TestFactory.CreateJsonCodec();

        var original = DataSourceNode.CreateObject()
            .Add("name", DataSourceNode.CreateString("test"))
            .Add("count", DataSourceNode.CreateNumber(42))
            .Add("active", DataSourceNode.CreateBoolean(true))
            .Add("nothing", DataSourceNode.CreateNull())
            .Add("tags", DataSourceNode.CreateArray()
                .Add(DataSourceNode.CreateString("a"))
                .Add(DataSourceNode.CreateString("b")))
            .Add("nested", DataSourceNode.CreateObject()
                .Add("inner", DataSourceNode.CreateNumber(3.14)));

        var json = codec.Encode(original);
        var decoded = codec.Decode(json);

        Assert.Equal("test", decoded["name"].AsString());
        Assert.Equal(42, decoded["count"].As<int>());
        Assert.True(decoded["active"].As<bool>());
        Assert.True(decoded["nothing"].IsNull);
        Assert.Equal(2, decoded["tags"].Count);
        Assert.Equal("a", decoded["tags"][0].AsString());
        Assert.Equal("b", decoded["tags"][1].AsString());
        Assert.Equal(3.14, decoded["nested"]["inner"].As<double>(), 0.001);
    }

    [Fact]
    public void JsonCodec_RoundTrip_TopLevelArray()
    {
        var codec = TestFactory.CreateJsonCodec();

        var original = DataSourceNode.CreateArray()
            .Add(DataSourceNode.CreateNumber(1))
            .Add(DataSourceNode.CreateNumber(2))
            .Add(DataSourceNode.CreateNumber(3));

        var json = codec.Encode(original);
        var decoded = codec.Decode(json);

        Assert.Equal(DataSourceNodeKind.Array, decoded.Kind);
        Assert.Equal(3, decoded.Count);
        Assert.Equal(2, decoded[1].As<int>());
    }

    // ── 8. JSON codec lazy behavior ──

    [Fact]
    public void JsonCodec_Decode_NestedObjectsAreLazy()
    {
        var codec = TestFactory.CreateJsonCodec();
        var json = """{"outer":{"inner":"value"}}""";

        var root = codec.Decode(json);

        // Accessing top-level key should work
        var outer = root["outer"];
        // Inner should be accessible through lazy expansion
        Assert.Equal("value", outer["inner"].AsString());
    }

    [Fact]
    public void JsonCodec_Decode_PrimitivesAreNotLazy()
    {
        var codec = TestFactory.CreateJsonCodec();
        var json = """{"str":"hello","num":42,"bool":true,"nil":null}""";

        var root = codec.Decode(json);

        Assert.Equal(DataSourceNodeKind.Text, root["str"].Kind);
        Assert.Equal(DataSourceNodeKind.Number, root["num"].Kind);
        Assert.Equal(DataSourceNodeKind.Bool, root["bool"].Kind);
        Assert.Equal(DataSourceNodeKind.Null, root["nil"].Kind);
    }

    // ── 9. Map codec round-trip ──

    [Fact]
    public void MapCodec_RoundTrip_FlatObject()
    {
        var codec = TestFactory.CreateMapCodec();

        var original = DataSourceNode.CreateObject()
            .Add("alpha", DataSourceNode.CreateString("one"))
            .Add("beta", DataSourceNode.CreateString("two"));

        var encoded = codec.Encode(original);
        var decoded = codec.Decode(encoded);

        Assert.Equal("one", decoded["alpha"].AsString());
        Assert.Equal("two", decoded["beta"].AsString());
    }

    // ── 10. Map codec parsing edge cases ──

    [Fact]
    public void MapCodec_Decode_IgnoresCommentsAndEmptyLines()
    {
        var codec = TestFactory.CreateMapCodec();
        var text = "# comment\n\nkey: value\n# another comment\nother: data\n";

        var node = codec.Decode(text);

        Assert.Equal(2, node.Keys.Count());
        Assert.Equal("value", node["key"].AsString());
        Assert.Equal("data", node["other"].AsString());
    }

    [Fact]
    public void MapCodec_Decode_HandlesColonsInValues()
    {
        var codec = TestFactory.CreateMapCodec();
        var text = "url: http://example.com:8080/path\n";

        var node = codec.Decode(text);

        Assert.Equal("http://example.com:8080/path", node["url"].AsString());
    }

    [Fact]
    public void MapCodec_Encode_SkipsNullValues()
    {
        var codec = TestFactory.CreateMapCodec();

        var obj = DataSourceNode.CreateObject()
            .Add("keep", DataSourceNode.CreateString("yes"))
            .Add("drop", DataSourceNode.CreateNull());

        var encoded = codec.Encode(obj);

        Assert.Contains("keep: yes", encoded);
        Assert.DoesNotContain("drop", encoded);
    }
}
