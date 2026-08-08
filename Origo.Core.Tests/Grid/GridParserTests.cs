using System.Text.Json;
using Origo.Core.Grid;
using Xunit;

namespace Origo.Core.Tests;

public class GridParserTests
{
    [Fact]
    public void ParseCoords_ValidString_ReturnsCoords()
    {
        var result = GridParser.ParseCoords("3,5");
        Assert.NotNull(result);
        Assert.Equal(3, result.Value.X);
        Assert.Equal(5, result.Value.Z);
    }

    [Fact]
    public void ParseCoords_WithSpaces_Trims()
    {
        var result = GridParser.ParseCoords(" 3 , 5 ");
        Assert.NotNull(result);
        Assert.Equal(3, result.Value.X);
        Assert.Equal(5, result.Value.Z);
    }

    [Fact]
    public void ParseCoords_NegativeValues_Works()
    {
        var result = GridParser.ParseCoords("-1,-10");
        Assert.NotNull(result);
        Assert.Equal(-1, result.Value.X);
        Assert.Equal(-10, result.Value.Z);
    }

    [Fact]
    public void ParseCoords_JsonElement_Works()
    {
        using var doc = JsonDocument.Parse("\"3,5\"");
        var result = GridParser.ParseCoords(doc.RootElement);
        Assert.NotNull(result);
        Assert.Equal(3, result.Value.X);
        Assert.Equal(5, result.Value.Z);
    }

    [Fact]
    public void ParseCoords_InvalidFormat_ReturnsNull()
    {
        Assert.Null(GridParser.ParseCoords("abc"));
        Assert.Null(GridParser.ParseCoords("3"));
        Assert.Null(GridParser.ParseCoords(""));
        Assert.Null(GridParser.ParseCoords(" "));
    }

    [Fact]
    public void ParseCoords_NullInput_ReturnsNull() => Assert.Null(GridParser.ParseCoords(null!));

    [Fact]
    public void ParseCoords_JsonElement_NumberKind_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("42");
        Assert.Null(GridParser.ParseCoords(doc.RootElement));
    }

    [Fact]
    public void ParseCoords_JsonElement_TrueKind_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("true");
        Assert.Null(GridParser.ParseCoords(doc.RootElement));
    }

    [Fact]
    public void ParseCoords_JsonElement_NullKind_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("null");
        Assert.Null(GridParser.ParseCoords(doc.RootElement));
    }

    [Fact]
    public void ParseCoords_IsCultureInvariant()
    {
        // Parsing must not depend on the ambient culture: a dot thousands
        // separator (valid in e.g. de-DE) must not change the result, and
        // the invariant interpretation ("1.000" is not an integer) applies
        // regardless of the current culture.
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture =
                System.Globalization.CultureInfo.GetCultureInfo("de-DE");

            var parsed = GridParser.ParseCoords("10,20");
            Assert.NotNull(parsed);
            Assert.Equal(10, parsed.Value.X);
            Assert.Equal(20, parsed.Value.Z);

            Assert.Null(GridParser.ParseCoords("1.000,5"));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }
}
