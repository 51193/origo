using System;
using System.Linq;
using Origo.Core.Random;
using Xunit;

namespace Origo.Core.Tests;

public class NoiseMapGeneratorTests
{
    public static TheoryData<int> GenerateSimplexWorleyBlendMap_InvalidSize_Data { get; } = [0, -4];

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

    [Fact]
    public void ExtendedOverload_ProducesValidRange()
    {
        const int size = 32;
        var map = NoiseMapGenerator.GenerateSimplexWorleyBlendMap(
            size, 42, 0.05f, 5, 2.0f, 0.5f, 2f);

        Assert.Equal(size * size, map.Length);
        Assert.All(map, value => Assert.InRange(value, 0f, 1f));
    }

    [Fact]
    public void ExtendedOverload_WithDifferentOctaves_ProducesDifferentResult()
    {
        const int size = 16;
        var map1 = NoiseMapGenerator.GenerateSimplexWorleyBlendMap(
            size, 42, 0.05f, 1, 2.0f, 0.5f, 1f);
        var map2 = NoiseMapGenerator.GenerateSimplexWorleyBlendMap(
            size, 42, 0.05f, 5, 2.0f, 0.5f, 1f);

        Assert.True(map1.Zip(map2, (a, b) => Math.Abs(a - b)).Sum() > 0.01f);
    }

    [Fact]
    public void ExtendedOverload_SameParams_SameResult()
    {
        const int size = 16;
        var map1 = NoiseMapGenerator.GenerateSimplexWorleyBlendMap(
            size, 99, 0.03f, 3, 2.5f, 0.7f, 1.5f);
        var map2 = NoiseMapGenerator.GenerateSimplexWorleyBlendMap(
            size, 99, 0.03f, 3, 2.5f, 0.7f, 1.5f);

        Assert.True(map1.SequenceEqual(map2));
    }

    [Theory]
    [InlineData(0, 2.0f, 0.5f, 1.0f, 1.0f, "octaves")]
    [InlineData(3, 1.0f, 0.5f, 1.0f, 1.0f, "lacunarity")]
    [InlineData(3, 2.0f, 0.0f, 1.0f, 1.0f, "gain")]
    [InlineData(3, 2.0f, 0.5f, 0.0f, 1.0f, "frequency")]
    [InlineData(3, 2.0f, 0.5f, 1.0f, 0.0f, "worleyFrequencyMultiplier")]
    public void ExtendedOverload_InvalidParameters_Throw(int octaves, float lacunarity, float gain, float frequency, float worleyMultiplier, string paramName)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            NoiseMapGenerator.GenerateSimplexWorleyBlendMap(16, 42, frequency, octaves, lacunarity, gain, worleyMultiplier));

        Assert.Equal(paramName, exception.ParamName);
    }
}
