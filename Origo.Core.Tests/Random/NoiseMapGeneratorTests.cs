using System;
using Origo.Core.Random;
using Xunit;

namespace Origo.Core.Tests;

public class NoiseMapGeneratorTests
{
    public static TheoryData<int> GenerateSimplexWorleyBlendMap_InvalidSize_Data { get; } = new() { 0, -4 };

    [Fact]
    public void GenerateSimplexWorleyBlendMap_ReturnsExpectedLengthAndRange()
    {
        const int size = 32;

        var map = NoiseMapGenerator.GenerateSimplexWorleyBlendMap(size);

        Assert.Equal(size * size, map.Length);
        Assert.All(map, value => Assert.InRange(value, 0f, 1f));
    }

    [Fact]
    public void GenerateSimplexWorleyBlendMap_SameSeed_ProducesSameResult()
    {
        const int size = 16;
        const int seed = 20260414;

        var left = NoiseMapGenerator.GenerateSimplexWorleyBlendMap(size, seed);
        var right = NoiseMapGenerator.GenerateSimplexWorleyBlendMap(size, seed);

        Assert.True(left.SequenceEqual(right));
    }

    [Theory]
    [MemberData(nameof(GenerateSimplexWorleyBlendMap_InvalidSize_Data))]
    public void GenerateSimplexWorleyBlendMap_InvalidSize_Throws(int size)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            NoiseMapGenerator.GenerateSimplexWorleyBlendMap(size));

        Assert.Equal("size", exception.ParamName);
    }
}
