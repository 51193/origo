using System;
using System.Numerics;

namespace Origo.EvolutionDemo.Logic;

/// <summary>A point the creature can sense; the ID excludes the sensing creature from its own creature sensor.</summary>
public readonly record struct SensorPoint(int Id, Vector2 Position);

/// <summary>Pure sensor calculations used by the simulation and its tests.</summary>
public static class SensorMath
{
    /// <summary>
    ///     Returns eight normalized sensor values:
    ///     food-ahead, food-left, food-right, wall-ahead, creature-ahead,
    ///     energy, sin(clock), cos(clock).
    /// </summary>
    public static float[] Compute(
        Vector2 position,
        float heading,
        float energy,
        ReadOnlySpan<SensorPoint> food,
        ReadOnlySpan<SensorPoint> creatures,
        int selfId,
        float oscillatorPhase = 0f,
        float arenaRadius = 0f,
        float senseRange = 0f)
    {
        arenaRadius = arenaRadius <= 0f ? Tuning.PondRadius : arenaRadius;
        senseRange = senseRange <= 0f ? Tuning.SenseRange : senseRange;
        var sensors = new float[Genome.SensorCount];
        ComputeInto(
            position,
            heading,
            energy,
            food,
            creatures,
            selfId,
            oscillatorPhase,
            arenaRadius,
            senseRange,
            sensors);
        return sensors;
    }

    /// <summary>
    ///     Fills a caller-owned sensor buffer. This is the hot simulation path;
    ///     avoiding a per-creature array allocation materially helps large ponds.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the output buffer has the wrong length.</exception>
    public static void ComputeInto(
        Vector2 position,
        float heading,
        float energy,
        ReadOnlySpan<SensorPoint> food,
        ReadOnlySpan<SensorPoint> creatures,
        int selfId,
        float oscillatorPhase,
        float arenaRadius,
        float senseRange,
        Span<float> sensors)
    {
        if (sensors.Length != Genome.SensorCount)
            throw new ArgumentException($"Expected {Genome.SensorCount} sensor slots.", nameof(sensors));

        var forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
        var left = new Vector2(forward.Y, -forward.X);
        var right = new Vector2(-forward.Y, forward.X);
        var cosHalfAngle = MathF.Cos(Tuning.SenseHalfAngle);

        sensors[0] = BestInCone(position, forward, food, senseRange, cosHalfAngle);
        sensors[1] = BestInCone(position, left, food, senseRange, cosHalfAngle);
        sensors[2] = BestInCone(position, right, food, senseRange, cosHalfAngle);
        sensors[3] = WallProximity(position, forward, arenaRadius, senseRange);
        sensors[4] = BestInCone(position, forward, creatures, senseRange, cosHalfAngle, selfId);
        sensors[5] = Math.Clamp(energy / Tuning.ReproductionEnergy, 0f, 1f);
        sensors[6] = MathF.Sin(oscillatorPhase);
        sensors[7] = MathF.Cos(oscillatorPhase);
    }

    private static float BestInCone(
        Vector2 origin,
        Vector2 direction,
        ReadOnlySpan<SensorPoint> points,
        float range,
        float cosHalfAngle,
        int? excludedId = null)
    {
        var best = 0f;
        var rangeSquared = range * range;
        foreach (var point in points)
        {
            if (excludedId.HasValue && point.Id == excludedId.Value)
                continue;

            var delta = point.Position - origin;
            var distanceSquared = delta.LengthSquared();
            if (distanceSquared <= 0.0001f || distanceSquared > rangeSquared)
                continue;

            var distance = MathF.Sqrt(distanceSquared);
            var dot = ((delta.X * direction.X) + (delta.Y * direction.Y)) / distance;
            if (dot < cosHalfAngle)
                continue;

            var proximity = 1f - (distance / range);
            if (proximity > best)
                best = proximity;
        }

        return best;
    }

    private static float WallProximity(Vector2 position, Vector2 forward, float arenaRadius, float range)
    {
        // Distance along the forward ray to the circular boundary:
        // |p + t*d| = R  =>  t = -b + sqrt(b^2 - c), where b=p·d, c=|p|^2-R^2.
        var b = Vector2.Dot(position, forward);
        var c = Vector2.Dot(position, position) - (arenaRadius * arenaRadius);
        var discriminant = (b * b) - c;
        if (discriminant < 0f)
            return 0f;

        var distance = -b + MathF.Sqrt(discriminant);
        if (distance < 0f)
            return 0f;

        return Math.Clamp(1f - (distance / range), 0f, 1f);
    }
}
