using System;
using System.Collections.Generic;
using System.Globalization;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Origo.EvolutionDemo.Logic;
using Origo.EvolutionDemo.Logic.Gameplay;

namespace Origo.EvolutionDemo;

/// <summary>
///     A living food source. The controller drives it once per fixed simulation
///     tick (never per render frame); it periodically scans food in its
///     settlement and globally, then spawns food with a probability that falls
///     as the local density rises. Local capacity keeps one source from
///     consuming the global food cap while nearby creatures are absent.
/// </summary>
[StrategyIndex("evolution.foodsource")]
public sealed class FoodSourceStrategy : LifecycleStrategyBase
{
    private const string SourceKind = "food_source";

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

    /// <summary>Runs every food source exactly once for one simulation tick.</summary>
    internal static void TickAll(
        ISndEntity controller,
        ISndContext ctx,
        SensorGrid grid,
        int foodCount)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(grid);
        var session = controller.OwningSession;
        foreach (var source in session.GetEntities())
        {
            if (source.IsPendingKill)
                continue;
            var (found, kind) = source.TryGetData<string>(DemoKeys.Kind);
            if (!found || !string.Equals(kind, SourceKind, StringComparison.Ordinal))
                continue;
            TickSource(source, controller, ctx, grid, foodCount);
        }
    }

    private static void TickSource(
        ISndEntity entity,
        ISndEntity controller,
        ISndContext ctx,
        SensorGrid grid,
        int foodCount)
    {
        var session = entity.OwningSession;
        var time = ReadFloat(controller, DemoKeys.Time, 0f);
        var patchIndex = ReadInt(entity, DemoKeys.PatchIndex, 0);
        var patchCount = Math.Max(1, ReadInt(entity, DemoKeys.PatchCount, 1));
        var seed = ReadInt(entity, DemoKeys.Seed, 1);
        UpdateWalk(entity, seed, patchIndex, time);
        var nextScan = ReadFloat(entity, DemoKeys.NextScanAt, 0f);
        if (time >= nextScan)
        {
            var scan = Scan(entity, controller, grid, foodCount);
            entity.SetData(DemoKeys.LocalFood, scan.Local);
            entity.SetData(DemoKeys.GlobalFood, scan.Global);
            entity.SetData(DemoKeys.LocalCapacity, scan.Capacity);
            var scanStep = 0;
            do
            {
                nextScan += Math.Max(0.02f, Tuning.FoodSourceScanIntervalSeconds);
                scanStep++;
            }
            while (time >= nextScan && scanStep < 4);

            entity.SetData(DemoKeys.NextScanAt, nextScan);
        }

        var nextSpawn = ReadFloat(entity, DemoKeys.NextSpawnAt, 0f);
        if (time < nextSpawn)
            return;
        var spawnStep = 0;
        do
        {
            nextSpawn += Math.Max(0.02f, Tuning.FoodSourceSpawnIntervalSeconds);
            spawnStep++;
        }
        while (time >= nextSpawn && spawnStep < 4);

        entity.SetData(DemoKeys.NextSpawnAt, nextSpawn);

        var localFood = ReadInt(entity, DemoKeys.LocalFood, 0);
        var globalFood = ReadInt(entity, DemoKeys.GlobalFood, 0);
        var capacity = Math.Max(1, ReadInt(entity, DemoKeys.LocalCapacity, Tuning.FoodPatchLocalCapacity));
        if (localFood >= capacity || globalFood >= Tuning.FoodCap)
            return;

        var period = Math.Max(1f, ReadFloat(entity, DemoKeys.CyclePeriod, 90f));
        var phase = ReadFloat(entity, DemoKeys.CyclePhase, 0f);
        var low = ReadFloat(entity, DemoKeys.CycleLow, 0.15f);
        var high = ReadFloat(entity, DemoKeys.CycleHigh, 2f);
        var wave = 0.5f + (0.5f * MathF.Sin(((time + phase) / period) * MathF.Tau));
        var cycleFactor = low + ((high - low) * wave);
        var density = Math.Clamp(
            (1f - Math.Clamp(localFood / (float)capacity, 0f, 1f)) * cycleFactor,
            0f,
            1f);
        var spawnCount = ReadInt(entity, DemoKeys.SourceSpawnCount, 0);
        var nextFoodId = Math.Max(1, ReadInt(entity, DemoKeys.SourceNextFoodId, 1));
        var rng = new Random(unchecked((seed * 397) ^ (patchIndex * 7919) ^ (spawnCount * 104729)));
        var metas = new List<SndMetaData>();
        for (var i = 0; i < Tuning.FoodSourceSpawnBurst; i++)
        {
            if (localFood + metas.Count >= capacity || globalFood + metas.Count >= Tuning.FoodCap)
                break;
            if (rng.NextDouble() >= density)
                continue;

            var sourceCenter = new System.Numerics.Vector2(
                ReadFloat(entity, DemoKeys.X, 0f),
                ReadFloat(entity, DemoKeys.Y, 0f));
            var point = FoodPatchDistribution.SampleAt(
                rng,
                sourceCenter,
                Tuning.FoodPatchRadius,
                Tuning.FoodPatchFalloff);
            var foodLimit = Tuning.PondRadius - Tuning.FoodPickupRadius;
            var foodDistanceSquared = (point.X * point.X) + (point.Y * point.Y);
            if (foodDistanceSquared > foodLimit * foodLimit)
            {
                var scale = foodLimit / MathF.Sqrt(foodDistanceSquared);
                point = new System.Numerics.Vector2(point.X * scale, point.Y * scale);
            }

            var typeRoll = rng.NextDouble();
            var foodType = typeRoll < Tuning.ToughFoodShare
                ? FoodType.Tough
                : typeRoll < Tuning.ToughFoodShare + Tuning.ToxicFoodShare
                    ? FoodType.Toxic
                    : FoodType.Soft;
            metas.Add(EvolutionEntityFactory.Food(
                string.Create(CultureInfo.InvariantCulture, $"foodsrc_{patchIndex}_{nextFoodId++}"),
                point.X,
                point.Y,
                foodType: foodType));
        }

        if (metas.Count == 0)
            return;

        var foodSpawned = metas.Count;
        var spawnVariant = GameplayRuntime.EnsureCurrent().Resolver.Resolve("spawn.policy");
        if (spawnVariant is not null)
        {
            var nextCreature = Math.Max(1, ReadInt(controller, DemoKeys.NextCreatureId, 1));
            var nextLineage = Math.Max(1, ReadInt(controller, DemoKeys.NextLineageId, 1));
            var spawnContext = new SpawnPolicyContext
            {
                Controller = controller,
                Source = entity,
                Rng = rng,
                Metas = metas,
                HandlerId = spawnVariant.HandlerId,
                Variant = spawnVariant,
                NextCreatureId = nextCreature,
                NextLineageId = nextLineage,
            };
            DemoMechanicHandlers.Spawn.Require(spawnVariant.HandlerId).Execute(spawnContext);
            if (spawnContext.NextCreatureId != nextCreature)
                controller.SetData(DemoKeys.NextCreatureId, spawnContext.NextCreatureId);
            if (spawnContext.NextLineageId != nextLineage)
                controller.SetData(DemoKeys.NextLineageId, spawnContext.NextLineageId);
        }

        entity.SetData(DemoKeys.SourceSpawnCount, spawnCount + 1);
        entity.SetData(DemoKeys.SourceNextFoodId, nextFoodId);
        entity.SetData(DemoKeys.LocalFood, localFood + foodSpawned);
        var spawn = metas.ToArray();
        ctx.Deferred.EnqueueBusinessDeferred(() => session.SpawnMany(spawn));
    }

    private static void UpdateWalk(ISndEntity entity, int seed, int patchIndex, float time)
    {
        var nextWalk = ReadFloat(entity, DemoKeys.SourceWalkNextAt, 0f);
        if (time < nextWalk)
            return;

        var walkStep = ReadInt(entity, DemoKeys.SourceWalkStep, 0);
        var heading = ReadFloat(entity, DemoKeys.Heading, 0f);
        var speed = ReadFloat(entity, DemoKeys.SourceWalkSpeed, 8f);
        var x = ReadFloat(entity, DemoKeys.X, 0f);
        var y = ReadFloat(entity, DemoKeys.Y, 0f);
        var steps = 0;
        while (time >= nextWalk && steps < 4)
        {
            walkStep++;
            var rng = new Random(unchecked(
                (seed * 397) ^ ((patchIndex + 1) * 7919) ^ (walkStep * 104729)));
            heading += (float)(((rng.NextDouble() * 2d) - 1d) * Tuning.FoodSourceWalkTurnRadians);
            x += MathF.Cos(heading) * speed * Math.Max(0.02f, Tuning.FoodSourceWalkIntervalSeconds);
            y += MathF.Sin(heading) * speed * Math.Max(0.02f, Tuning.FoodSourceWalkIntervalSeconds);
            var limit = Tuning.PondRadius - Tuning.FoodPatchRadius;
            var limitSquared = limit * limit;
            if ((x * x) + (y * y) > limitSquared)
            {
                var scale = limit / MathF.Sqrt((x * x) + (y * y));
                x *= scale;
                y *= scale;
                heading = MathF.Atan2(-y, -x)
                    + (float)(((rng.NextDouble() * 2d) - 1d) * 0.6d);
            }

            nextWalk += Math.Max(0.02f, Tuning.FoodSourceWalkIntervalSeconds);
            steps++;
        }

        entity.SetData(DemoKeys.X, x);
        entity.SetData(DemoKeys.Y, y);
        entity.SetData(DemoKeys.Heading, heading);
        entity.SetData(DemoKeys.SourceWalkSpeed, speed);
        entity.SetData(DemoKeys.SourceWalkStep, walkStep);
        entity.SetData(DemoKeys.SourceWalkNextAt, nextWalk);
    }

    /// <summary>
    ///     Counts the food inside the source's patch. The local count comes from
    ///     the tick's spatial grid (foods only: corpse points carry ids at or
    ///     above <paramref name="foodCount" />), and the global count is the
    ///     controller snapshot's food total, so a source never has to walk the
    ///     whole session.
    /// </summary>
    private static (int Local, int Global, int Capacity) Scan(
        ISndEntity source,
        ISndEntity controller,
        SensorGrid grid,
        int foodCount)
    {
        var centerX = ReadFloat(source, DemoKeys.X, 0f);
        var centerY = ReadFloat(source, DemoKeys.Y, 0f);
        var radius = Tuning.FoodPatchRadius;
        var radiusSquared = radius * radius;
        var localFood = new List<SensorPoint>();
        grid.Query(centerX, centerY, radius, grid.Food, localFood);
        var local = 0;
        foreach (var point in localFood)
        {
            if (point.Id >= foodCount)
                continue;
            var dx = point.Position.X - centerX;
            var dy = point.Position.Y - centerY;
            if ((dx * dx) + (dy * dy) <= radiusSquared)
                local++;
        }

        var capacity = Tuning.FoodPatchLocalCapacity;
        var bloomEnabled = ReadBool(controller, DemoKeys.BloomEnabled, false);
        var time = ReadFloat(controller, DemoKeys.Time, 0f);
        if (bloomEnabled && time < Tuning.EarlyFoodRampSeconds)
        {
            var bonus = Tuning.EarlyFoodBonus * (1f - (time / Tuning.EarlyFoodRampSeconds));
            var patchCount = Math.Max(1, ReadInt(source, DemoKeys.PatchCount, 1));
            capacity += (int)MathF.Round(bonus / patchCount);
        }

        return (local, foodCount, Math.Max(1, capacity));
    }

    private static void EnsureDefaults(ISndEntity entity)
    {
        if (!entity.TryGetData<string>(DemoKeys.Kind).found)
            entity.SetData(DemoKeys.Kind, SourceKind);

        var seed = ReadInt(entity, DemoKeys.Seed, 1);
        var index = ReadInt(entity, DemoKeys.PatchIndex, 0);

        // Sources must not share clock phases: sixteen aligned scans or spawn
        // bursts in one tick are a visible frame spike. Each source starts its
        // clocks at a deterministic offset derived from the pond seed and its
        // patch index; loaded saves keep the clocks they persisted.
        if (!entity.TryGetData<float>(DemoKeys.NextScanAt).found)
            entity.SetData(
                DemoKeys.NextScanAt,
                FoodSourceCadence.Phase(
                    seed,
                    index,
                    FoodSourceCadence.Channel.Scan,
                    MathF.Max(0.02f, Tuning.FoodSourceScanIntervalSeconds)));
        if (!entity.TryGetData<float>(DemoKeys.NextSpawnAt).found)
            entity.SetData(
                DemoKeys.NextSpawnAt,
                FoodSourceCadence.Phase(
                    seed,
                    index,
                    FoodSourceCadence.Channel.Spawn,
                    MathF.Max(0.02f, Tuning.FoodSourceSpawnIntervalSeconds)));
        if (!entity.TryGetData<int>(DemoKeys.SourceSpawnCount).found)
            entity.SetData(DemoKeys.SourceSpawnCount, 0);
        if (!entity.TryGetData<int>(DemoKeys.SourceNextFoodId).found)
            entity.SetData(DemoKeys.SourceNextFoodId, 1);
        if (!entity.TryGetData<int>(DemoKeys.LocalFood).found)
            entity.SetData(DemoKeys.LocalFood, 0);
        if (!entity.TryGetData<int>(DemoKeys.GlobalFood).found)
            entity.SetData(DemoKeys.GlobalFood, 0);
        if (!entity.TryGetData<int>(DemoKeys.LocalCapacity).found)
            entity.SetData(DemoKeys.LocalCapacity, Tuning.FoodPatchLocalCapacity);

        var patchCount = Math.Max(1, ReadInt(entity, DemoKeys.PatchCount, 1));
        var rng = new Random(unchecked((seed * 22695477) ^ ((index + 1) * 1013904223)));
        if (!entity.TryGetData<float>(DemoKeys.X).found)
        {
            var center = FoodPatchDistribution.Center(seed, index, patchCount, Tuning.PondRadius);
            entity.SetData(DemoKeys.X, center.X);
            entity.SetData(DemoKeys.Y, center.Y);
        }
        if (!entity.TryGetData<float>(DemoKeys.Heading).found)
            entity.SetData(DemoKeys.Heading, (float)(rng.NextDouble() * Math.Tau));
        if (!entity.TryGetData<float>(DemoKeys.SourceWalkSpeed).found)
            entity.SetData(
                DemoKeys.SourceWalkSpeed,
                Tuning.FoodSourceWalkSpeedMin
                + ((float)rng.NextDouble() * (Tuning.FoodSourceWalkSpeedMax - Tuning.FoodSourceWalkSpeedMin)));
        if (!entity.TryGetData<float>(DemoKeys.SourceWalkNextAt).found)
            entity.SetData(
                DemoKeys.SourceWalkNextAt,
                FoodSourceCadence.Phase(
                    seed,
                    index,
                    FoodSourceCadence.Channel.Walk,
                    MathF.Max(0.02f, Tuning.FoodSourceWalkIntervalSeconds)));
        if (!entity.TryGetData<int>(DemoKeys.SourceWalkStep).found)
            entity.SetData(DemoKeys.SourceWalkStep, 0);
        if (!entity.TryGetData<float>(DemoKeys.CyclePeriod).found)
            entity.SetData(
                DemoKeys.CyclePeriod,
                Tuning.FoodSourceCyclePeriodMin
                + ((float)rng.NextDouble() * (Tuning.FoodSourceCyclePeriodMax - Tuning.FoodSourceCyclePeriodMin)));
        if (!entity.TryGetData<float>(DemoKeys.CyclePhase).found)
            entity.SetData(
                DemoKeys.CyclePhase,
                (float)(rng.NextDouble() * ReadFloat(entity, DemoKeys.CyclePeriod, 90f)));
        if (!entity.TryGetData<float>(DemoKeys.CycleLow).found)
            entity.SetData(
                DemoKeys.CycleLow,
                Tuning.FoodSourceCycleLowMin
                + ((float)rng.NextDouble() * (Tuning.FoodSourceCycleLowMax - Tuning.FoodSourceCycleLowMin)));
        if (!entity.TryGetData<float>(DemoKeys.CycleHigh).found)
            entity.SetData(
                DemoKeys.CycleHigh,
                Tuning.FoodSourceCycleHighMin
                + ((float)rng.NextDouble() * (Tuning.FoodSourceCycleHighMax - Tuning.FoodSourceCycleHighMin)));
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
