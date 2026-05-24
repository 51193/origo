using System.Collections.Generic;
using Origo.Core.Save.Meta;
using Xunit;

namespace Origo.Core.Tests;

// ── SavePathLayout ─────────────────────────────────────────────────────

public class SaveMetaMapCodecExtendedTests
{
    [Fact]
    public void SaveMetaMapCodec_Parse_BasicContent()
    {
        var logger = new TestLogger();
        var result = SaveMetaMapCodec.Parse("key1: value1\nkey2: value2", logger);
        Assert.Equal(2, result.Count);
        Assert.Equal("value1", result["key1"]);
    }

    [Fact]
    public void SaveMetaMapCodec_Parse_NullContent_ReturnsEmpty()
    {
        var logger = new TestLogger();
        var result = SaveMetaMapCodec.Parse(null, logger);
        Assert.Empty(result);
    }

    [Fact]
    public void SaveMetaMapCodec_Serialize_SortsByKey()
    {
        var map = new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" };
        var text = SaveMetaMapCodec.Serialize(map);
        Assert.StartsWith("a: 1", text);
        Assert.Contains("b: 2", text);
    }

    [Fact]
    public void SaveMetaMapCodec_Serialize_NullMap_ReturnsEmpty() =>
        Assert.Equal(string.Empty, SaveMetaMapCodec.Serialize(null));

    [Fact]
    public void SaveMetaMapCodec_Serialize_EmptyMap_ReturnsEmpty()
    {
        Assert.Equal(string.Empty,
            SaveMetaMapCodec.Serialize(new Dictionary<string, string>()));
    }

    [Fact]
    public void SaveMetaMapCodec_RoundTrip()
    {
        var logger = new TestLogger();
        var original = new Dictionary<string, string> { ["name"] = "Test", ["score"] = "100" };
        var serialized = SaveMetaMapCodec.Serialize(original);
        var parsed = SaveMetaMapCodec.Parse(serialized, logger);
        Assert.Equal("Test", parsed["name"]);
        Assert.Equal("100", parsed["score"]);
    }
}

// ── DelegateSaveMetaContributor ────────────────────────────────────────
