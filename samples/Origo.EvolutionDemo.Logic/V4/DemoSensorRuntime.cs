using System;
using System.Collections.Generic;
using System.Numerics;

namespace Origo.EvolutionDemo.Logic.V4;

/// <summary>World snapshot available to a V4 sensor evaluator.</summary>
public sealed class DemoSensorContext
{
    public required Vector2 Position { get; init; }
    public required float Heading { get; init; }
    public required float Energy { get; init; }
    public required float MaxEnergy { get; init; }
    public required float SenseRange { get; init; }
    public required IReadOnlyList<SensorPoint> Food { get; init; }
    public required IReadOnlyList<SensorPoint> Creatures { get; init; }
    public required IReadOnlyList<SensorPoint> Corpses { get; init; }
    public required int SelfId { get; init; }
    public required float OscillatorPhase { get; init; }
    public required float ArenaRadius { get; init; }
}

/// <summary>
///     Registry-driven sensor runtime. Only ports expressed by the phenotype are
///     evaluated; adding an interface registers a new evaluator instead of adding
///     another fixed branch to a 12-slot switch.
/// </summary>
public static class DemoSensorRuntime
{
    /// <summary>Maximum dense sensor-interface id supported by the runtime buffer.</summary>
    public const int MaxInterfaceId = 63;

    private static readonly Dictionary<ushort, Func<DemoSensorContext, float>> Evaluators = [];

    static DemoSensorRuntime()
    {
        Register(SensorKindV4.WallAhead, static context => WallProximity(context, Tuning.PondRadius));
        Register(SensorKindV4.Energy, static context =>
            Math.Clamp(context.Energy / MathF.Max(1f, context.MaxEnergy), 0f, 1f));
        Register(SensorKindV4.FoodAhead, static context =>
            BestInCone(context, context.Food, Forward(context), context.SenseRange));
        Register(SensorKindV4.FoodLeft, static context =>
            BestInCone(context, context.Food, Left(context), context.SenseRange));
        Register(SensorKindV4.FoodRight, static context =>
            BestInCone(context, context.Food, Right(context), context.SenseRange));
        Register(SensorKindV4.CreatureAhead, static context =>
            BestInCone(context, context.Creatures, Forward(context), context.SenseRange, context.SelfId));
        Register(SensorKindV4.CorpseAhead, static context =>
            BestInCone(context, context.Corpses, Forward(context), context.SenseRange));
        Register(SensorKindV4.Sin, static context => MathF.Sin(context.OscillatorPhase));
        Register(SensorKindV4.Cos, static context => MathF.Cos(context.OscillatorPhase));
    }

    /// <summary>Registers or replaces a sensor evaluator for a stable interface id.</summary>
    public static void Register(SensorKindV4 sensor, Func<DemoSensorContext, float> evaluator)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        Evaluators[(ushort)sensor] = evaluator;
    }

    /// <summary>Fills the dense sensor buffer using only the phenotype's expressed ports.</summary>
    public static void ComputeInto(
        PhenotypeV4 phenotype,
        DemoSensorContext context,
        Span<float> sensors)
    {
        ArgumentNullException.ThrowIfNull(phenotype);
        ArgumentNullException.ThrowIfNull(context);
        if (sensors.Length <= MaxInterfaceId)
            throw new ArgumentException($"Sensor buffer must be larger than {MaxInterfaceId}.", nameof(sensors));
        sensors.Clear();
        foreach (var port in phenotype.SensorPorts)
        {
            if (port > MaxInterfaceId)
                continue;
            if (Evaluators.TryGetValue(port, out var evaluator))
                sensors[port] = evaluator(context);
        }
    }

    private static Vector2 Forward(DemoSensorContext context) =>
        new(MathF.Cos(context.Heading), MathF.Sin(context.Heading));

    private static Vector2 Left(DemoSensorContext context) =>
        new(Forward(context).Y, -Forward(context).X);

    private static Vector2 Right(DemoSensorContext context) =>
        new(-Forward(context).Y, Forward(context).X);

    private static float BestInCone(
        DemoSensorContext context,
        IReadOnlyList<SensorPoint> points,
        Vector2 direction,
        float range,
        int? excludedId = null)
    {
        var best = 0f;
        var rangeSquared = range * range;
        var cosHalfAngle = MathF.Cos(Tuning.SenseHalfAngle);
        foreach (var point in points)
        {
            if (excludedId.HasValue && point.Id == excludedId.Value)
                continue;
            var delta = point.Position - context.Position;
            var distanceSquared = delta.LengthSquared();
            if (distanceSquared <= 0.0001f || distanceSquared > rangeSquared)
                continue;
            var distance = MathF.Sqrt(distanceSquared);
            var dot = Vector2.Dot(delta, direction) / distance;
            if (dot < cosHalfAngle)
                continue;
            best = MathF.Max(best, 1f - (distance / range));
        }

        return best;
    }

    private static float WallProximity(DemoSensorContext context, float arenaRadius)
    {
        var forward = Forward(context);
        var b = Vector2.Dot(context.Position, forward);
        var c = Vector2.Dot(context.Position, context.Position) - (arenaRadius * arenaRadius);
        var discriminant = (b * b) - c;
        if (discriminant < 0f)
            return 0f;
        var distance = -b + MathF.Sqrt(discriminant);
        return distance < 0f ? 0f : Math.Clamp(1f - (distance / context.SenseRange), 0f, 1f);
    }
}
