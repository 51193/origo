using System;
using System.Numerics;
using Origo.EvolutionDemo.Logic;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public class SensorMathTests
{
    private static readonly Vector2 Origin = Vector2.Zero;
    private const float HeadingRight = 0f;

    [Fact]
    public void Compute_FoodAhead_IsStrongerWhenCloser()
    {
        var near = SensorMath.Compute(
            Origin, HeadingRight, 60f,
            [new SensorPoint(1, new Vector2(30f, 0f))],
            [], 0);
        var far = SensorMath.Compute(
            Origin, HeadingRight, 60f,
            [new SensorPoint(1, new Vector2(120f, 0f))],
            [], 0);

        Assert.True(near[0] > far[0]);
        Assert.InRange(near[0], 0f, 1f);
        Assert.InRange(far[0], 0f, 1f);
    }

    [Fact]
    public void Compute_FoodBehind_DoesNotDriveAheadSensor()
    {
        var sensors = SensorMath.Compute(
            Origin, HeadingRight, 60f,
            [new SensorPoint(1, new Vector2(-40f, 0f))],
            [], 0);

        Assert.Equal(0f, sensors[0]);
    }

    [Fact]
    public void Compute_FoodOnEitherSide_SelectsLeftOrRightSensor()
    {
        var left = SensorMath.Compute(
            Origin, HeadingRight, 60f,
            [new SensorPoint(1, new Vector2(0f, -40f))],
            [], 0);
        var right = SensorMath.Compute(
            Origin, HeadingRight, 60f,
            [new SensorPoint(1, new Vector2(0f, 40f))],
            [], 0);

        Assert.True(left[1] > 0f);
        Assert.Equal(0f, left[2]);
        Assert.True(right[2] > 0f);
        Assert.Equal(0f, right[1]);
    }

    [Fact]
    public void Compute_WallAhead_IsStrongerNearBoundary()
    {
        var center = SensorMath.Compute(Origin, HeadingRight, 60f, [], [], 0);
        var edge = SensorMath.Compute(new Vector2(Tuning.PondRadius - 40f, 0f), HeadingRight, 60f, [], [], 0);

        Assert.Equal(0f, center[3]);
        Assert.True(edge[3] > 0.5f);
    }

    [Fact]
    public void Compute_CreatureSensor_ExcludesSelfAndDetectsOthers()
    {
        var sensors = SensorMath.Compute(
            Origin, HeadingRight, 60f, [],
            [
                new SensorPoint(7, Origin),
                new SensorPoint(8, new Vector2(50f, 0f)),
            ], 7);

        Assert.True(sensors[4] > 0f);
    }

    [Fact]
    public void Compute_OscillatorSensors_AreStableSineAndCosine()
    {
        var start = SensorMath.Compute(Origin, HeadingRight, 60f, [], [], 0, 0f);
        var quarter = SensorMath.Compute(Origin, HeadingRight, 60f, [], [], 0, MathF.PI / 2f);

        Assert.Equal(0f, start[6], 3);
        Assert.Equal(1f, start[7], 3);
        Assert.Equal(1f, quarter[6], 3);
        Assert.Equal(0f, quarter[7], 3);
    }

    [Fact]
    public void Compute_EnergySensor_IsNormalized()
    {
        var full = SensorMath.Compute(Origin, HeadingRight, Tuning.ReproductionEnergy, [], [], 0);
        var empty = SensorMath.Compute(Origin, HeadingRight, 0f, [], [], 0);

        Assert.Equal(1f, full[5]);
        Assert.Equal(0f, empty[5]);
    }

    [Fact]
    public void Compute_RejectsOutOfRangeValuesByClamping()
    {
        var sensors = SensorMath.Compute(Origin, HeadingRight, 10_000f, [], [], 0);

        Assert.Equal(1f, sensors[5]);
        Assert.All(sensors, static value => Assert.InRange(value, 0f, 1f));
    }
}
