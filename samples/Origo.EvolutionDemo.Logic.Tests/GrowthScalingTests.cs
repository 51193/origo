using Origo.EvolutionDemo.Logic;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class GrowthScalingTests
{
    [Fact]
    public void SizeMultiplier_NoTrophiesIsExactOne()
    {
        Assert.Equal(1f, GrowthScaling.SizeMultiplier(0, 0));
    }

    [Fact]
    public void TintStrength_NoTrophiesIsExactZero()
    {
        Assert.Equal(0f, GrowthScaling.TintStrength(0, 0));
    }

    [Fact]
    public void SizeMultiplier_GrowsWithTrophiesAndCaps()
    {
        var previous = GrowthScaling.SizeMultiplier(0, 0);
        for (var kills = 1; kills <= 40; kills++)
        {
            var current = GrowthScaling.SizeMultiplier(kills, 0);
            Assert.True(current >= previous, "size growth must be monotonic");
            previous = current;
        }

        Assert.Equal(1.75f, GrowthScaling.SizeMultiplier(40, 0), 3);
    }

    [Fact]
    public void TintStrength_GrowsWithTrophiesAndCaps()
    {
        var previous = GrowthScaling.TintStrength(0, 0);
        for (var kills = 1; kills <= 40; kills++)
        {
            var current = GrowthScaling.TintStrength(kills, 0);
            Assert.True(current >= previous, "tint must be monotonic");
            previous = current;
        }

        Assert.Equal(0.85f, GrowthScaling.TintStrength(40, 0), 3);
    }

    [Fact]
    public void Trophies_ScavengingAlsoContributes()
    {
        Assert.True(GrowthScaling.SizeMultiplier(0, 20) > 1f);
        Assert.True(GrowthScaling.TintStrength(0, 20) > 0f);
    }

    [Fact]
    public void Trophies_NegativeCountsDoNotShrinkTheCreature()
    {
        Assert.Equal(1f, GrowthScaling.SizeMultiplier(-5, -5));
        Assert.Equal(0f, GrowthScaling.TintStrength(-5, -5));
    }
}
