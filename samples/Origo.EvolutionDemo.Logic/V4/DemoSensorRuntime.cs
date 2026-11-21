using System;
using System.Collections.Generic;
using System.Numerics;

namespace Origo.EvolutionDemo.Logic.V4;

/// <summary>World snapshot available to a V4 sensor evaluator.</summary>
public readonly struct DemoSensorContext
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

    /// <summary>Food type per food/ground point id; corpse points are soft.</summary>
    public IReadOnlyList<FoodType>? FoodTypes { get; init; }
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
    private static readonly bool _defaultsRegistered;
    private static bool _customFoodEvaluator;

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
        Register(SensorKindV4.FoodSoft, static context => BestFoodType(context, FoodType.Soft));
        Register(SensorKindV4.FoodTough, static context => BestFoodType(context, FoodType.Tough));
        Register(SensorKindV4.FoodToxic, static context => BestFoodType(context, FoodType.Toxic));
        _defaultsRegistered = true;
    }

    /// <summary>Registers or replaces a sensor evaluator for a stable interface id.</summary>
    public static void Register(SensorKindV4 sensor, Func<DemoSensorContext, float> evaluator)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        if (_defaultsRegistered
            && (sensor == SensorKindV4.FoodAhead
                || sensor == SensorKindV4.FoodLeft
                || sensor == SensorKindV4.FoodRight))
            _customFoodEvaluator = true;
        Evaluators[(ushort)sensor] = evaluator;
    }

    /// <summary>Fills the dense sensor buffer using only the phenotype's expressed ports.</summary>
    public static void ComputeInto(
        PhenotypeV4 phenotype,
        DemoSensorContext context,
        Span<float> sensors)
    {
        ArgumentNullException.ThrowIfNull(phenotype);
        if (sensors.Length <= MaxInterfaceId)
            throw new ArgumentException($"Sensor buffer must be larger than {MaxInterfaceId}.", nameof(sensors));
        sensors.Clear();
        var batchFood = !_customFoodEvaluator;
        var foodAhead = 0f;
        var foodLeft = 0f;
        var foodRight = 0f;
        if (batchFood)
        {
            foreach (var port in phenotype.SensorPorts)
            {
                if (port is (ushort)SensorKindV4.FoodAhead
                    or (ushort)SensorKindV4.FoodLeft
                    or (ushort)SensorKindV4.FoodRight)
                {
                    ComputeFoodCones(context, out foodAhead, out foodLeft, out foodRight);
                    break;
                }
            }
        }

        foreach (var port in phenotype.SensorPorts)
        {
            if (port > MaxInterfaceId)
                continue;
            if (batchFood)
            {
                switch (port)
                {
                    case (ushort)SensorKindV4.FoodAhead:
                        sensors[port] = foodAhead;
                        continue;
                    case (ushort)SensorKindV4.FoodLeft:
                        sensors[port] = foodLeft;
                        continue;
                    case (ushort)SensorKindV4.FoodRight:
                        sensors[port] = foodRight;
                        continue;
                }
            }

            if (Evaluators.TryGetValue(port, out var evaluator))
                sensors[port] = evaluator(context);
        }
    }

    /// <summary>
    ///     Evaluates the three food cones in one pass over the food list. The
    ///     registry fast path is used only while the built-in food evaluators are
    ///     installed; custom registrations fall back to the per-port evaluators.
    /// </summary>
    private static void ComputeFoodCones(
        DemoSensorContext context,
        out float ahead,
        out float left,
        out float right)
    {
        ahead = 0f;
        left = 0f;
        right = 0f;
        var range = context.SenseRange;
        var rangeSquared = range * range;
        var cosHalfAngle = MathF.Cos(Tuning.SenseHalfAngle);
        var forward = Forward(context);
        var leftDirection = new Vector2(forward.Y, -forward.X);
        var rightDirection = new Vector2(-forward.Y, forward.X);
        foreach (var point in context.Food)
        {
            var delta = point.Position - context.Position;
            var distanceSquared = delta.LengthSquared();
            if (distanceSquared <= 0.0001f || distanceSquared > rangeSquared)
                continue;
            var distance = MathF.Sqrt(distanceSquared);
            var score = 1f - (distance / range);
            if (!(Vector2.Dot(delta, forward) / distance < cosHalfAngle))
                ahead = MathF.Max(ahead, score);
            if (!(Vector2.Dot(delta, leftDirection) / distance < cosHalfAngle))
                left = MathF.Max(left, score);
            if (!(Vector2.Dot(delta, rightDirection) / distance < cosHalfAngle))
                right = MathF.Max(right, score);
        }
    }

    private static Vector2 Forward(DemoSensorContext context) =>
        new(MathF.Cos(context.Heading), MathF.Sin(context.Heading));

    private static Vector2 Left(DemoSensorContext context) =>
        new(Forward(context).Y, -Forward(context).X);

    private static Vector2 Right(DemoSensorContext context) =>
        new(-Forward(context).Y, Forward(context).X);

    private static float BestFoodType(DemoSensorContext context, FoodType type)
    {
        var types = context.FoodTypes
            ?? throw new InvalidOperationException(
                "FoodTypeVision sensors require DemoSensorContext.FoodTypes.");
        var best = 0f;
        var rangeSquared = context.SenseRange * context.SenseRange;
        foreach (var point in context.Food)
        {
            if (point.Id < 0 || point.Id >= types.Count)
                throw new InvalidOperationException(
                    $"Food point id {point.Id} is outside the FoodTypes table ({types.Count}).");
            if (types[point.Id] != type)
                continue;
            var delta = point.Position - context.Position;
            var distanceSquared = delta.LengthSquared();
            if (distanceSquared > rangeSquared)
                continue;
            var distance = MathF.Sqrt(distanceSquared);
            best = MathF.Max(best, 1f - (distance / context.SenseRange));
        }

        return best;
    }

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
