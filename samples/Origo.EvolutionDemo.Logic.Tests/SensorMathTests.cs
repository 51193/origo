using System;
using System.Numerics;
using Origo.EvolutionDemo.Logic;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public class SensorMathTests
{
    private static readonly Vector2 Origin = Vector2.Zero;
    private const float HeadingRight = 0f;
    private static readonly BodyPhenotype Body = CreatureGenome.RandomFounder(new Random(4)).Phenotype with
    {
        VisionOrgan = true,
        RhythmOrgan = true,
        CreatureSenseOrgan = true,
    };

    [Fact]
    public void Compute_FoodAhead_IsStrongerWhenCloser()
    {
        var near = Compute([new SensorPoint(1, new Vector2(30f, 0f))], []);
        var far = Compute([new SensorPoint(1, new Vector2(110f, 0f))], []);

        Assert.True(near[(int)SensorId.FoodAhead] > far[(int)SensorId.FoodAhead]);
        Assert.InRange(near[(int)SensorId.FoodAhead], 0f, 1f);
        Assert.InRange(far[(int)SensorId.FoodAhead], 0f, 1f);
    }

    [Fact]
    public void Compute_FoodBehind_DoesNotDriveAheadSensor()
    {
        var sensors = Compute([new SensorPoint(1, new Vector2(-40f, 0f))], []);

        Assert.Equal(0f, sensors[(int)SensorId.FoodAhead]);
    }

    [Fact]
    public void Compute_FoodOnEitherSide_SelectsLeftOrRightSensor()
    {
        var left = Compute([new SensorPoint(1, new Vector2(0f, -40f))], []);
        var right = Compute([new SensorPoint(1, new Vector2(0f, 40f))], []);

        Assert.True(left[(int)SensorId.FoodLeft] > 0f);
        Assert.Equal(0f, left[(int)SensorId.FoodRight]);
        Assert.True(right[(int)SensorId.FoodRight] > 0f);
        Assert.Equal(0f, right[(int)SensorId.FoodLeft]);
    }

    [Fact]
    public void Compute_WallAhead_IsStrongerNearBoundary()
    {
        var center = Compute([], [], Origin);
        var edge = Compute([], [], new Vector2(Tuning.PondRadius - 40f, 0f));

        Assert.Equal(0f, center[(int)SensorId.WallAhead]);
        Assert.True(edge[(int)SensorId.WallAhead] > 0.5f);
    }

    [Fact]
    public void Compute_CreatureSensor_ExcludesSelfAndDetectsOthers()
    {
        var sensors = Compute(
            [],
            [
                new SensorPoint(7, Origin),
                new SensorPoint(8, new Vector2(50f, 0f)),
            ]);

        Assert.True(sensors[(int)SensorId.CreatureAhead] > 0f);
    }

    [Fact]
    public void Compute_OscillatorSensors_AreStableSineAndCosine()
    {
        var start = Compute([], [], phase: 0f);
        var quarter = Compute([], [], phase: MathF.PI / 2f);

        Assert.Equal(0f, start[(int)SensorId.Sin], 3);
        Assert.Equal(1f, start[(int)SensorId.Cos], 3);
        Assert.Equal(1f, quarter[(int)SensorId.Sin], 3);
        Assert.Equal(0f, quarter[(int)SensorId.Cos], 3);
    }

    [Fact]
    public void Compute_EnergySensor_IsNormalizedByBodyCapacity()
    {
        var full = Compute([], [], energy: Body.MaxEnergy);
        var empty = Compute([], [], energy: 0f);

        Assert.Equal(1f, full[(int)SensorId.Energy]);
        Assert.Equal(0f, empty[(int)SensorId.Energy]);
    }

    [Fact]
    public void Compute_RejectsOutOfRangeValuesByClamping()
    {
        var sensors = Compute([], [], energy: 10_000f);

        Assert.Equal(1f, sensors[(int)SensorId.Energy]);
        Assert.All(sensors, static value => Assert.InRange(value, 0f, 1f));
    }

    private static float[] Compute(
        ReadOnlySpan<SensorPoint> food,
        ReadOnlySpan<SensorPoint> creatures,
        Vector2? position = null,
        float energy = 60f,
        float phase = 0f)
    {
        var sensors = new float[CreatureGenome.SensorSlots];
        SensorMath.ComputeInto(
            Body,
            position ?? Origin,
            HeadingRight,
            energy,
            food,
            creatures,
            0,
            phase,
            Tuning.PondRadius,
            sensors);
        return sensors;
    }
}
