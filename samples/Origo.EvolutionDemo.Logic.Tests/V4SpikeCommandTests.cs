using System;
using System.Collections.Generic;
using System.Numerics;
using Origo.EvolutionDemo.Logic;
using Origo.EvolutionDemo.Logic.V4;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class V4SpikeCommandTests
{
    [Fact]
    public void Plan_SelectsNearestTargetInRayAndChargesDistance()
    {
        var input = new SpikePlanInputV4(
            0,
            Vector2.Zero,
            0f,
            0f,
            100f,
            false,
            1,
            0f,
            0f,
            [
                new SpikeTargetV4(1, new Vector2(40f, 0f), 50f, 4f, false),
                new SpikeTargetV4(2, new Vector2(20f, 0f), 50f, 4f, false),
                new SpikeTargetV4(3, new Vector2(10f, 100f), 50f, 4f, false),
            ],
            new Random(1));
        var plan = DemoSpikeActionCommand.Plan(input);
        Assert.Single(plan.TargetIds);
        Assert.Equal(2, plan.TargetIds[0]);
        Assert.True(plan.Cost > Tuning.SpikeEnergyCost);
    }

    [Fact]
    public void Plan_MissChargesMaximumRange()
    {
        var input = new SpikePlanInputV4(
            0,
            Vector2.Zero,
            0f,
            0f,
            100f,
            false,
            1,
            0f,
            0f,
            [],
            new Random(1));
        var plan = DemoSpikeActionCommand.Plan(input);
        Assert.All(plan.TargetIds, static id => Assert.Equal(-1, id));
        Assert.Equal(100f, plan.MaxDistance);
    }

    [Fact]
    public void Plan_DoesNotHitSameTargetTwice()
    {
        var input = new SpikePlanInputV4(
            0,
            Vector2.Zero,
            0f,
            0f,
            100f,
            true,
            3,
            0f,
            0f,
            [new SpikeTargetV4(1, new Vector2(40f, 0f), 50f, 4f, false)],
            new Random(1));
        var plan = DemoSpikeActionCommand.Plan(input);
        Assert.Equal(3, plan.PelletCount);
        Assert.Single(plan.TargetIds, id => id == 1);
    }
}
