using System;
using System.Collections.Generic;
using Origo.EvolutionDemo.Logic.Gameplay;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

/// <summary>Regression tests for the compact per-root death codec.</summary>
public sealed class RootDeathCodecTests
{
    [Fact]
    public void Serialize_SortsOmitsZeroAndParsesRoundTrip()
    {
        var text = RootDeathCodec.Serialize(new Dictionary<int, RootDeathTally>
        {
            [5] = new(2, 1, 0),
            [2] = new(0, 0, 0),
            [9] = new(0, 4, 3),
        });

        Assert.Equal("5:2,1,0;9:0,4,3", text);
        var parsed = RootDeathCodec.Parse(text);
        Assert.Equal(2, parsed.Count);
        Assert.Equal(new RootDeathTally(2, 1, 0), parsed[5]);
        Assert.Equal(new RootDeathTally(0, 4, 3), parsed[9]);
        Assert.Empty(RootDeathCodec.Parse(""));
    }

    [Theory]
    [InlineData("1:2,3")]
    [InlineData("1:2,3,4,5")]
    [InlineData("x:2,3,4")]
    [InlineData("1:-1,3,4")]
    [InlineData("1:2,3,4;1:1,1,1")]
    public void Parse_RejectsMalformedValues(string text)
    {
        Assert.Throws<FormatException>(() => RootDeathCodec.Parse(text));
    }

    [Fact]
    public void Serialize_RejectsNegativeTotals()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RootDeathCodec.Serialize(new Dictionary<int, RootDeathTally> { [1] = new(-1, 0, 0) }));
    }
}
