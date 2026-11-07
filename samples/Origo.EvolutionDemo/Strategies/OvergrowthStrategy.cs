using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Origo.EvolutionDemo.Logic;

namespace Origo.EvolutionDemo;

/// <summary>
///     Global SND overgrowth mechanic. It scans food-density cells and, when one
///     is overfull, consumes a large amount of that food to create a heavily
///     mutated descendant in the overfull area. It is attached to the existing
///     controller entity, so it has no presentation node of its own.
/// </summary>
[StrategyIndex("evolution.overgrowth")]
public sealed class OvergrowthStrategy : LifecycleStrategyBase
{

    /// <inheritdoc/>
    public override void AfterSpawn(ISndEntity entity, ISndContext ctx)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureDefaults(entity);
    }

    /// <inheritdoc/>
    public override void AfterLoad(ISndEntity entity, ISndContext ctx)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureDefaults(entity);
    }

    /// <inheritdoc/>
    public override void Process(ISndEntity entity, double delta, ISndContext ctx)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(ctx);
        if (!Tuning.OvergrowthEnabled
            || !string.Equals(ReadString(entity, DemoKeys.Phase, string.Empty), DemoKeys.PhaseRunning, StringComparison.Ordinal)
            || ReadBool(entity, DemoKeys.Paused, false))
            return;

        var time = ReadFloat(entity, DemoKeys.Time, 0f);
        var next = ReadFloat(entity, DemoKeys.OvergrowthNextAt, 0f);
        if (time < next)
            return;
        entity.SetData(DemoKeys.OvergrowthNextAt, time + Math.Max(1f, Tuning.OvergrowthScanIntervalSeconds));

        var session = entity.OwningSession;
        var seed = ReadInt(entity, DemoKeys.Seed, 1);
        var rng = new Random(unchecked((seed * 397) ^ ((int)(time * 1000f) * 7919)));
        var cellSize = Math.Max(100f, Tuning.OvergrowthCellSize);
        var pondRadius = Tuning.PondRadius;
        var dimension = Math.Max(1, (int)MathF.Ceiling((pondRadius * 2f) / cellSize) + 1);
        var counts = new Dictionary<long, int>();
        var cells = new Dictionary<long, System.Numerics.Vector2>();
        foreach (var food in session.GetEntities())
        {
            if (food.IsPendingKill)
                continue;
            var foodKind = food.TryGetData<string>(DemoKeys.Kind);
            if (!foodKind.found
                || !string.Equals(foodKind.value, DemoKeys.FoodKind, StringComparison.Ordinal))
                continue;

            var x = ReadFloat(food, DemoKeys.X, 0f);
            var y = ReadFloat(food, DemoKeys.Y, 0f);
            var key = CellKey(x, y, pondRadius, cellSize, dimension);
            counts[key] = counts.GetValueOrDefault(key) + 1;
            if (!cells.ContainsKey(key))
                cells[key] = CellCenter(key, dimension, pondRadius, cellSize);
        }

        long selectedKey = long.MinValue;
        var selectedCount = 0;
        foreach (var pair in counts)
        {
            if (pair.Value <= selectedCount)
                continue;
            selectedKey = pair.Key;
            selectedCount = pair.Value;
        }

        if (selectedCount < Math.Max(1, Tuning.OvergrowthFoodThreshold))
            return;

        var targetFoods = new List<ISndEntity>(Tuning.OvergrowthFoodCost);
        foreach (var food in session.GetEntities())
        {
            if (food.IsPendingKill)
                continue;
            var foodKind = food.TryGetData<string>(DemoKeys.Kind);
            if (!foodKind.found
                || !string.Equals(foodKind.value, DemoKeys.FoodKind, StringComparison.Ordinal))
                continue;
            if (CellKey(
                    ReadFloat(food, DemoKeys.X, 0f),
                    ReadFloat(food, DemoKeys.Y, 0f),
                    pondRadius,
                    cellSize,
                    dimension) == selectedKey)
                targetFoods.Add(food);
        }

        var foodCost = Math.Max(1, Tuning.OvergrowthFoodCost);
        if (targetFoods.Count < foodCost)
            return;

        var creatures = new List<ISndEntity>();
        foreach (var candidate in session.GetEntities())
        {
            if (candidate.IsPendingKill)
                continue;
            var candidateKind = candidate.TryGetData<string>(DemoKeys.Kind);
            if (!candidateKind.found
                || !string.Equals(candidateKind.value, DemoKeys.CreatureKind, StringComparison.Ordinal))
                continue;
            creatures.Add(candidate);
        }

        if (creatures.Count == 0)
            return;

        var parent = creatures[rng.Next(creatures.Count)];
        var parentGenome = new CreatureGenome(
            ReadString(parent, DemoKeys.BodyGenome, string.Empty),
            ReadString(parent, DemoKeys.BrainGenome, string.Empty));
        if (!BodyGenome.IsStructurallyValid(parentGenome.Body) || !NeuralGenome.IsValid(parentGenome.Brain))
            return;

        var mutationRate = Math.Clamp(Tuning.OvergrowthMutationRate, 0.01d, 1d);
        var childGenome = CreatureGenome.Mutate(
            parentGenome,
            mutationRate,
            mutationRate,
            0d,
            mutationRate,
            rng,
            out var changes);
        if (changes == 0)
            childGenome = CreatureGenome.Mutate(
                childGenome,
                mutationRate,
                mutationRate,
                0d,
                mutationRate,
                rng,
                out changes);
        var childPhenotype = childGenome.Phenotype;

        var generation = Math.Max(0, ReadInt(parent, DemoKeys.Generation, 0)) + 1;
        var lineage = Math.Max(0, ReadInt(parent, DemoKeys.Lineage, 1));
        if (changes >= Tuning.LineageMutationThreshold)
        {
            lineage = ReadInt(entity, DemoKeys.NextLineageId, 1);
            entity.SetData(DemoKeys.NextLineageId, lineage + 1);
        }

        var nextCreatureId = ReadInt(entity, DemoKeys.NextCreatureId, 1);
        entity.SetData(DemoKeys.NextCreatureId, nextCreatureId + 1);
        var cellCenter = cells[selectedKey];
        var childPosition = ClampToPond(
            cellCenter + new System.Numerics.Vector2(
                (float)((rng.NextDouble() * 2d) - 1d) * cellSize * 0.35f,
                (float)((rng.NextDouble() * 2d) - 1d) * cellSize * 0.35f),
            childPhenotype.BodyRadius);
        var child = EvolutionEntityFactory.Creature(
            EvolutionEntityFactory.CreatureName(nextCreatureId),
            childGenome,
            childPhenotype,
            childPosition.X,
            childPosition.Y,
            (float)(rng.NextDouble() * Math.Tau),
            MathF.Min(Tuning.OvergrowthChildEnergy, childPhenotype.MaxEnergy),
            generation,
            lineage,
            0f);

        for (var i = 0; i < foodCost; i++)
            session.RequestKillEntity(targetFoods[i].Name);

        entity.SetData(DemoKeys.OvergrowthCount, ReadInt(entity, DemoKeys.OvergrowthCount, 0) + 1);
        ctx.Deferred.EnqueueBusinessDeferred(() => session.SpawnMany(child));
    }

    private static long CellKey(float x, float y, float radius, float cellSize, int dimension)
    {
        var cellX = Math.Clamp((int)((x + radius) / cellSize), 0, dimension - 1);
        var cellY = Math.Clamp((int)((y + radius) / cellSize), 0, dimension - 1);
        return ((long)cellY * dimension) + cellX;
    }

    private static System.Numerics.Vector2 CellCenter(long key, int dimension, float radius, float cellSize)
    {
        var cellX = (int)(key % dimension);
        var cellY = (int)(key / dimension);
        return new System.Numerics.Vector2(
            -radius + ((cellX + 0.5f) * cellSize),
            -radius + ((cellY + 0.5f) * cellSize));
    }

    private static System.Numerics.Vector2 ClampToPond(System.Numerics.Vector2 position, float margin)
    {
        var limit = Math.Max(1f, Tuning.PondRadius - margin);
        var lengthSquared = (position.X * position.X) + (position.Y * position.Y);
        if (lengthSquared <= limit * limit)
            return position;
        var scale = limit / MathF.Sqrt(lengthSquared);
        return new System.Numerics.Vector2(position.X * scale, position.Y * scale);
    }

    private static void EnsureDefaults(ISndEntity entity)
    {
        if (!string.Equals(ReadString(entity, DemoKeys.Kind, string.Empty), DemoKeys.ControllerKind, StringComparison.Ordinal))
            return;
        if (!entity.TryGetData<float>(DemoKeys.OvergrowthNextAt).found)
            entity.SetData(DemoKeys.OvergrowthNextAt, 0f);
        if (!entity.TryGetData<int>(DemoKeys.OvergrowthCount).found)
            entity.SetData(DemoKeys.OvergrowthCount, 0);
    }

    private static string ReadString(ISndEntity entity, string key, string fallback)
    {
        var (found, value) = entity.TryGetData<string>(key);
        return found && value is not null ? value : fallback;
    }

    private static float ReadFloat(ISndEntity entity, string key, float fallback)
    {
        var (found, value) = entity.TryGetData<float>(key);
        return found ? value : fallback;
    }

    private static int ReadInt(ISndEntity entity, string key, int fallback)
    {
        var (found, value) = entity.TryGetData<int>(key);
        return found ? value : fallback;
    }

    private static bool ReadBool(ISndEntity entity, string key, bool fallback)
    {
        var (found, value) = entity.TryGetData<bool>(key);
        return found ? value : fallback;
    }
}
