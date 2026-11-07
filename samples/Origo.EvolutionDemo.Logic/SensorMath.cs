using System;
using System.Numerics;

namespace Origo.EvolutionDemo.Logic;

/// <summary>A point the creature can sense; the ID excludes the sensing creature from its own creature sensor.</summary>
public readonly record struct SensorPoint(int Id, Vector2 Position);

/// <summary>Pure sensor calculations used by the simulation and its tests.</summary>
public static class SensorMath
{
    /// <summary>
    ///     Fills the twelve-slot latent sensor buffer for a body phenotype.
    ///     Inactive organ slots remain zero, but keep their stable slot indices.
    /// </summary>
    public static void ComputeInto(
        BodyPhenotype body,
        Vector2 position,
        float heading,
        float energy,
        ReadOnlySpan<SensorPoint> food,
        ReadOnlySpan<SensorPoint> creatures,
        int selfId,
        float oscillatorPhase,
        float arenaRadius,
        Span<float> sensors)
    {
        if (sensors.Length != NeuralGenome.SensorSlots)
            throw new ArgumentException($"Expected {NeuralGenome.SensorSlots} sensor slots.", nameof(sensors));

        sensors.Clear();
        var forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
        var left = new Vector2(forward.Y, -forward.X);
        var right = new Vector2(-forward.Y, forward.X);
        var cosHalfAngle = MathF.Cos(Tuning.SenseHalfAngle);

        if (body.VisionOrgan)
        {
            sensors[(int)SensorId.FoodAhead] = BestInCone(position, forward, food, body.SenseRange, cosHalfAngle);
            sensors[(int)SensorId.FoodLeft] = BestInCone(position, left, food, body.SenseRange, cosHalfAngle);
            sensors[(int)SensorId.FoodRight] = BestInCone(position, right, food, body.SenseRange, cosHalfAngle);
        }

        if (body.CreatureSenseOrgan)
            sensors[(int)SensorId.CreatureAhead] =
                BestInCone(position, forward, creatures, body.SenseRange, cosHalfAngle, selfId);
        sensors[(int)SensorId.WallAhead] = WallProximity(position, forward, arenaRadius, body.SenseRange);
        sensors[(int)SensorId.Energy] = Math.Clamp(energy / MathF.Max(1f, body.MaxEnergy), 0f, 1f);
        if (body.RhythmOrgan)
        {
            sensors[(int)SensorId.Sin] = MathF.Sin(oscillatorPhase);
            sensors[(int)SensorId.Cos] = MathF.Cos(oscillatorPhase);
        }
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
