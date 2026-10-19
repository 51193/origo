using System;
using Origo.Core.DataSource;
using Xunit;

namespace Origo.Core.Tests;

// ── KeyValueFileParser ─────────────────────────────────────────────────

public class KeyValueFileParserTests
{
    [Fact]
    public void KeyValueFileParser_Parse_BasicKeyValue()
    {
        var logger = new TestLogger();
        var result = KeyValueFileParser.Parse("key1: value1\nkey2: value2", "test", logger);
        Assert.Equal(2, result.Count);
        Assert.Equal("value1", result["key1"]);
        Assert.Equal("value2", result["key2"]);
    }

    [Fact]
    public void KeyValueFileParser_Parse_SkipsCommentsAndBlanks()
    {
        var logger = new TestLogger();
        var content = "# comment\n\nkey1: value1\n# another comment\nkey2: value2";
        var result = KeyValueFileParser.Parse(content, "test", logger);
        Assert.Equal(2, result.Count);
        Assert.Equal("value1", result["key1"]);
    }

    [Fact]
    public void KeyValueFileParser_Parse_EmptyContent_ReturnsEmpty()
    {
        var logger = new TestLogger();
        var result = KeyValueFileParser.Parse("", "test", logger);
        Assert.Empty(result);
    }

    [Fact]
    public void KeyValueFileParser_Parse_NullContent_ReturnsEmpty()
    {
        var logger = new TestLogger();
        var result = KeyValueFileParser.Parse(null, "test", logger);
        Assert.Empty(result);
    }

    [Fact]
    public void KeyValueFileParser_Parse_ThrowsOnInvalidLine()
    {
        var logger = new TestLogger();
        Assert.Throws<FormatException>(() =>
            KeyValueFileParser.Parse("invalid_line_no_colon", "test", logger));
    }

    [Fact]
    public void KeyValueFileParser_Parse_ThrowsOnEmptyKey()
    {
        var logger = new TestLogger();
        Assert.Throws<FormatException>(() =>
            KeyValueFileParser.Parse(": value", "test", logger));
    }

    [Fact]
    public void KeyValueFileParser_Parse_DuplicateKey_LogsWarning()
    {
        var logger = new TestLogger();
        var result = KeyValueFileParser.Parse("key: v1\nkey: v2", "test", logger);
        Assert.Equal("v2", result["key"]);
        Assert.NotEmpty(logger.Warnings);
    }

    [Fact]
    public void KeyValueFileParser_Parse_DuplicateKey_LastWinsAndWarns()
    {
        // A duplicate key is structurally valid and follows the documented
        // last-value-wins contract with an observable warning.
        var logger = new TestLogger();
        var result = KeyValueFileParser.Parse("key: v1\nkey: v2", "test", logger);

        Assert.Equal("v2", result["key"]);
        Assert.Contains(logger.Warnings, warning => warning.Contains("Duplicate key", StringComparison.Ordinal));
    }

    [Fact]
    public void KeyValueFileParser_Parse_ValueContainsColon_PreservesFullValue()
    {
        var logger = new TestLogger();
        var result = KeyValueFileParser.Parse("url: http://example.com:8080", "test", logger);
        Assert.Equal("http://example.com:8080", result["url"]);
    }

    [Fact]
    public void KeyValueFileParser_Parse_ThrowsOnNullLogger()
    {
        Assert.Throws<ArgumentNullException>(() =>
            KeyValueFileParser.Parse("key: value", "test", null!));
    }
}
