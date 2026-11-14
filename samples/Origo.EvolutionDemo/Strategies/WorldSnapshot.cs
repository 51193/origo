using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.EvolutionDemo.Logic;
using Origo.EvolutionDemo.Logic.V4;
using NumericsVector2 = System.Numerics.Vector2;

namespace Origo.EvolutionDemo;

/// <summary>Immutable per-tick creature view consumed by simulation systems.</summary>
internal readonly record struct CreatureSnapshot(
    ISndEntity Entity,
    float X,
    float Y,
    float Heading,
    float Energy,
    float Age,
    float ReproCooldown,
    float SpikeCooldown,
    float BodyRadius,
    float FoodPickupRadius,
    float MaxEnergy,
    float MoveSpeed,
    float TurnSpeed,
    float SenseRange,
    float SpikeRadius,
    float BasalMetabolism,
    float MaxAge,
    int OrganMask,
    int SpikeCount,
    float SpikeSpread,
    float SpikeJitter)
{
    public bool SpikeBattery => (OrganMask & (int)DemoOrganMaskV4.SpikeBattery) != 0;
    public bool HardDigest => (OrganMask & (int)DemoOrganMaskV4.HardDigest) != 0;
    public bool ToxinResistance => (OrganMask & (int)DemoOrganMaskV4.ToxinResistance) != 0;
}

internal readonly record struct FoodSnapshot(ISndEntity Entity, float X, float Y, bool FromCorpse, FoodType Type);

internal readonly record struct CorpseSnapshot(
    ISndEntity Entity,
    float X,
    float Y,
    int Remaining,
    float Timer,
    float Interval);

/// <summary>Reusable scratch and entity view for one simulation tick.</summary>
internal sealed class WorldSnapshot
{
    private static readonly List<CreatureSnapshot> _creatures = [];
    private static readonly List<FoodSnapshot> _foods = [];
    private static readonly List<CorpseSnapshot> _corpses = [];
    private static readonly List<SensorPoint> _nearFood = [];
    private static readonly List<SensorPoint> _nearCreatures = [];
    private static SensorPoint[] _foodPoints = [];
    private static SensorPoint[] _creaturePoints = [];
    private static SensorPoint[] _corpsePoints = [];
    private static bool[] _foodConsumed = [];

    private WorldSnapshot(
        List<CreatureSnapshot> creatures,
        List<FoodSnapshot> foods,
        List<CorpseSnapshot> corpses,
        SensorGrid grid,
        List<SensorPoint> nearFood,
        List<SensorPoint> nearCreatures,
        bool[] foodConsumed,
        SensorPoint[] corpsePoints)
    {
        Creatures = creatures;
        Foods = foods;
        Corpses = corpses;
        Grid = grid;
        NearFood = nearFood;
        NearCreatures = nearCreatures;
        FoodConsumed = foodConsumed;
        CorpsePoints = corpsePoints;
    }

    public List<CreatureSnapshot> Creatures { get; }
    public List<FoodSnapshot> Foods { get; }
    public List<CorpseSnapshot> Corpses { get; }
    public SensorGrid Grid { get; }
    public List<SensorPoint> NearFood { get; }
    public List<SensorPoint> NearCreatures { get; }
    public bool[] FoodConsumed { get; }
    public SensorPoint[] CorpsePoints { get; }
    public int FoodAlive => Foods.Count;

    public static WorldSnapshot Capture(ISndEntity controller)
    {
        ArgumentNullException.ThrowIfNull(controller);
        var session = controller.OwningSession;
        var entities = session.GetEntities();
        _creatures.Clear();
        _foods.Clear();
        _corpses.Clear();
        foreach (var entity in entities)
        {
            if (entity.IsPendingKill)
                continue;
            var kindResult = entity.TryGetData<string>(DemoKeys.Kind);
            if (!kindResult.found)
                continue;
            var kind = kindResult.value;
            if (string.Equals(kind, DemoKeys.CreatureKind, StringComparison.Ordinal))
                _creatures.Add(ReadCreature(entity));
            else if (string.Equals(kind, DemoKeys.FoodKind, StringComparison.Ordinal))
                _foods.Add(new FoodSnapshot(entity,
                    ReadFloat(entity, DemoKeys.X, 0f),
                    ReadFloat(entity, DemoKeys.Y, 0f),
                    ReadBool(entity, DemoKeys.FoodFromCorpse, false),
                    (FoodType)Math.Clamp(ReadInt(entity, DemoKeys.FoodType, 0), 0, 2)));
            else if (string.Equals(kind, DemoKeys.CorpseKind, StringComparison.Ordinal))
                _corpses.Add(new CorpseSnapshot(entity,
                    ReadFloat(entity, DemoKeys.X, 0f),
                    ReadFloat(entity, DemoKeys.Y, 0f),
                    ReadInt(entity, DemoKeys.CorpseFoodRemaining, 0),
                    ReadFloat(entity, DemoKeys.CorpseTimer, 0f),
                    ReadFloat(entity, DemoKeys.CorpseInterval, Tuning.CarcassFoodIntervalSeconds)));
        }

        var sensableFoodCount = _foods.Count + _corpses.Count;
        if (_foodPoints.Length < sensableFoodCount)
            _foodPoints = new SensorPoint[sensableFoodCount];
        if (_creaturePoints.Length < _creatures.Count)
            _creaturePoints = new SensorPoint[_creatures.Count];
        if (_foodConsumed.Length < _foods.Count)
            _foodConsumed = new bool[_foods.Count];
        for (var i = 0; i < _foods.Count; i++)
            _foodPoints[i] = new SensorPoint(i, new NumericsVector2(_foods[i].X, _foods[i].Y));
        for (var i = 0; i < _corpses.Count; i++)
            _foodPoints[_foods.Count + i] = new SensorPoint(
                _foods.Count + i, new NumericsVector2(_corpses[i].X, _corpses[i].Y));
        for (var i = 0; i < _creatures.Count; i++)
            _creaturePoints[i] = new SensorPoint(i, new NumericsVector2(_creatures[i].X, _creatures[i].Y));
        if (_corpsePoints.Length < _corpses.Count)
            _corpsePoints = new SensorPoint[_corpses.Count];
        for (var i = 0; i < _corpses.Count; i++)
            _corpsePoints[i] = new SensorPoint(i, new NumericsVector2(_corpses[i].X, _corpses[i].Y));

        var grid = SensorGrid.Build(_foodPoints, sensableFoodCount, _creaturePoints, _creatures.Count);
        _nearFood.Clear();
        _nearCreatures.Clear();
        Array.Clear(_foodConsumed, 0, _foods.Count);
        return new WorldSnapshot(_creatures, _foods, _corpses, grid, _nearFood, _nearCreatures,
            _foodConsumed, _corpsePoints);
    }

    private static CreatureSnapshot ReadCreature(ISndEntity entity) => new(
        entity,
        ReadFloat(entity, DemoKeys.X, 0f),
        ReadFloat(entity, DemoKeys.Y, 0f),
        ReadFloat(entity, DemoKeys.Heading, 0f),
        ReadFloat(entity, DemoKeys.Energy, Tuning.InitialEnergy),
        ReadFloat(entity, DemoKeys.Age, 0f),
        ReadFloat(entity, DemoKeys.ReproCooldown, 0f),
        ReadFloat(entity, DemoKeys.SpikeCooldown, 0f),
        ReadFloat(entity, DemoKeys.BodyRadius, Tuning.BodyRadius),
        ReadFloat(entity, DemoKeys.FoodPickupRadius, Tuning.FoodPickupRadius),
        ReadFloat(entity, DemoKeys.MaxEnergy, 200f),
        ReadFloat(entity, DemoKeys.MoveSpeed, Tuning.CreatureSpeed),
        ReadFloat(entity, DemoKeys.TurnSpeed, Tuning.TurnSpeed),
        ReadFloat(entity, DemoKeys.SenseRange, Tuning.SenseRange),
        ReadFloat(entity, DemoKeys.SpikeRadius, Tuning.SpikeRadius),
        ReadFloat(entity, DemoKeys.BasalMetabolism, 1f),
        ReadFloat(entity, DemoKeys.MaxAge, Tuning.MaxAgeSeconds),
        ReadInt(entity, DemoKeys.OrganMask, 0),
        ReadInt(entity, DemoKeys.SpikeCount, 1),
        ReadFloat(entity, DemoKeys.SpikeSpread, 0f),
        ReadFloat(entity, DemoKeys.SpikeJitter, 0f));

    private static float ReadFloat(ISndEntity entity, string key, float fallback) =>
        entity.TryGetData<float>(key) is { found: true } result ? result.value : fallback;

    private static int ReadInt(ISndEntity entity, string key, int fallback) =>
        entity.TryGetData<int>(key) is { found: true } result ? result.value : fallback;

    private static bool ReadBool(ISndEntity entity, string key, bool fallback) =>
        entity.TryGetData<bool>(key) is { found: true } result ? result.value : fallback;
}
