using System;
using System.Collections.Generic;
using Origo.EvolutionDemo.Logic.Gameplay;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

/// <summary>Compact root birth codec tests.</summary>
public sealed class RootBirthCodecTests
{
    [Fact]
    public void Serialize_SortsOmitsZeroAndRoundTrips()
    {
        var text = RootBirthCodec.Serialize(new Dictionary<int, int>
        {
            [5] = 12,
            [2] = 0,
            [9] = 3,
        });

        Assert.Equal("5:12;9:3", text);
        var parsed = RootBirthCodec.Parse(text);
        Assert.Equal(2, parsed.Count);
        Assert.Equal(12, parsed[5]);
        Assert.Equal(3, parsed[9]);
        Assert.Empty(RootBirthCodec.Parse(""));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1:")]
    [InlineData("x:1")]
    [InlineData("1:-1")]
    [InlineData("1:2;1:3")]
    public void Parse_RejectsMalformedValues(string text)
    {
        Assert.Throws<FormatException>(() => RootBirthCodec.Parse(text));
    }

    [Fact]
    public void Serialize_RejectsNegativeBirths()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RootBirthCodec.Serialize(new Dictionary<int, int> { [1] = -1 }));
    }
}
