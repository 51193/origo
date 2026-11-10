using System;
using System.Collections.Generic;
using Origo.EvolutionDemo.Logic;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class FoodSourceCadenceTests
{
    [Fact]
    public void Phase_IsDeterministic()
    {
        var first = FoodSourceCadence.Phase(20261101, 3, FoodSourceCadence.Channel.Scan, 1f);
        var second = FoodSourceCadence.Phase(20261101, 3, FoodSourceCadence.Channel.Scan, 1f);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Phase_StaysWithinInterval()
    {
        foreach (var interval in new[] { 0.5f, 1f, 3f, 3600f })
        {
            for (var index = 0; index < 64; index++)
            {
                var phase = FoodSourceCadence.Phase(20261101, index, FoodSourceCadence.Channel.Spawn, interval);
                Assert.InRange(phase, 0f, MathF.BitDecrement(interval));
            }
        }
    }

    [Fact]
    public void Phase_SpreadsSourcesAcrossTheInterval()
    {
        var phases = new List<float>();
        for (var index = 0; index < 16; index++)
            phases.Add(FoodSourceCadence.Phase(20261101, index, FoodSourceCadence.Channel.Scan, 1f));

        Assert.Equal(16, phases.Count);
        Assert.True(
            new HashSet<float>(phases).Count >= 15,
            "per-source scan phases must be distinct for aligned sources to be staggered");
        Assert.DoesNotContain(0f, phases);
    }

    [Fact]
    public void Phase_ChannelsAreIndependent()
    {
        var scan = FoodSourceCadence.Phase(20261101, 5, FoodSourceCadence.Channel.Scan, 1f);
        var spawn = FoodSourceCadence.Phase(20261101, 5, FoodSourceCadence.Channel.Spawn, 1f);
        var walk = FoodSourceCadence.Phase(20261101, 5, FoodSourceCadence.Channel.Walk, 1f);

        Assert.NotEqual(scan, spawn);
        Assert.NotEqual(scan, walk);
        Assert.NotEqual(spawn, walk);
    }

    [Fact]
    public void Phase_RejectsNonPositiveInterval()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => FoodSourceCadence.Phase(1, 0, FoodSourceCadence.Channel.Scan, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => FoodSourceCadence.Phase(1, 0, FoodSourceCadence.Channel.Scan, -1f));
    }
}
