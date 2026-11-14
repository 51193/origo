using System;
using Origo.EvolutionDemo.Logic;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class FoodPatchDistributionTests
{
    [Fact]
    public void Center_IsDeterministicAndInsidePond()
    {
        var first = FoodPatchDistribution.Center(123, 3, 16, 10_000f);
        var second = FoodPatchDistribution.Center(123, 3, 16, 10_000f);

        Assert.Equal(first, second);
        Assert.True(first.Length() <= 10_000f * 0.82f + 0.01f);
    }

    [Fact]
    public void SampleRadius_IsBoundedAndConcentratesNearCenter()
    {
        var random = new Random(55);
        var total = 0d;
        for (var i = 0; i < 20_000; i++)
        {
            var radius = FoodPatchDistribution.SampleRadius(random, 420f, 1.8f);
            Assert.InRange(radius, 0f, 420f);
            total += radius;
        }

        Assert.True(total / 20_000d < 420d * 0.5d);
    }

    [Fact]
    public void SampleAt_StaysInsideMovingPatch()
    {
        var random = new Random(17);
        var center = new System.Numerics.Vector2(1234f, -567f);
        for (var i = 0; i < 5_000; i++)
        {
            var point = FoodPatchDistribution.SampleAt(random, center, 420f, 1.8f);
            Assert.True((point - center).Length() <= 420.01f);
        }
    }

    [Fact]
    public void Sample_StaysInsideRequestedPatchRadius()
    {
        var random = new Random(7);
        var center = FoodPatchDistribution.Center(9, 2, 16, 10_000f);
        for (var i = 0; i < 10_000; i++)
        {
            var point = FoodPatchDistribution.Sample(random, 9, 2, 16, 10_000f, 420f, 1.8f);
            Assert.True((point - center).Length() <= 420.01f);
        }
    }
}
