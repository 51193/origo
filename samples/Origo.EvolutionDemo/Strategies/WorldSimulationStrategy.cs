using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Origo.EvolutionDemo.Logic;
using NumericsVector2 = System.Numerics.Vector2;

namespace Origo.EvolutionDemo;

/// <summary>
///     Stateless world director. All mutable simulation state lives in SND
///     entities: this strategy only reads the controller data, advances fixed
///     simulation ticks, and schedules spawn/kill work through the deferred
///     queue so the session collection is never mutated during iteration.
/// </summary>
[StrategyIndex("evolution.world")]
public sealed class WorldSimulationStrategy : LifecycleStrategyBase
{
    private const double TwoPi = Math.PI * 2d;
    private const int BrainCacheLimit = 8192;
    private static readonly Dictionary<string, NeuralBrain> BrainCache = new(StringComparer.Ordinal);
    private static readonly List<CreatureSnapshot> CreatureScratch = [];
    private static readonly List<FoodSnapshot> FoodScratch = [];
    private static readonly List<SensorPoint> NearFoodScratch = [];
    private static readonly List<SensorPoint> NearCreatureScratch = [];
    private static readonly List<SndMetaData> MutationScratch = [];
    private static readonly List<CollisionBody> CollisionBodyScratch = [];
    private static readonly List<ISndEntity> CollisionEntityScratch = [];
    private static SensorPoint[] FoodPointScratch = [];
    private static SensorPoint[] CreaturePointScratch = [];
    private static bool[] FoodConsumedScratch = [];

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
        var phase = ReadString(entity, DemoKeys.Phase, DemoKeys.PhaseMenu);
        if (string.Equals(phase, DemoKeys.PhaseStarting, StringComparison.Ordinal))
        {
            StartWorld(entity, ctx);
            return;
        }

        if (!string.Equals(phase, DemoKeys.PhaseRunning, StringComparison.Ordinal)
            || ReadBool(entity, DemoKeys.Paused, false))
            return;

        if (ReadBool(entity, DemoKeys.FastForwardActive, false))
        {
            var currentTime = ReadFloat(entity, DemoKeys.Time, 0f);
            var target = ReadFloat(entity, DemoKeys.FastForwardTarget, currentTime);
            var start = ReadFloat(entity, DemoKeys.FastForwardStart, currentTime);
            if (currentTime >= target)
            {
                entity.SetData(DemoKeys.FastForwardActive, false);
                entity.SetData(DemoKeys.FastForwardProgress, 1f);
                entity.SetData(DemoKeys.Accumulator, 0f);
                return;
            }

            // One normal simulation tick per frame. Deferred spawn/kill work is
            // flushed by the adapter between frames, so this stays identical to
            // the playable loop; it simply runs at whatever frame rate the CPU
            // and the progress overlay permit.
            SimulateTick(entity, ctx);
            var now = ReadFloat(entity, DemoKeys.Time, currentTime);
            var span = Math.Max(0.0001f, target - start);
            entity.SetData(DemoKeys.FastForwardProgress, Math.Clamp((now - start) / span, 0f, 1f));
            entity.SetData(DemoKeys.Accumulator, 0f);
            if (string.Equals(ReadString(entity, DemoKeys.Phase, string.Empty), DemoKeys.PhaseExtinct, StringComparison.Ordinal))
            {
                entity.SetData(DemoKeys.FastForwardActive, false);
                entity.SetData(DemoKeys.FastForwardProgress, 1f);
            }

            return;
        }

        var speed = Math.Clamp(ReadInt(entity, DemoKeys.Speed, 1), 1, 8);
        var accumulator = ReadFloat(entity, DemoKeys.Accumulator, 0f) + (float)(delta * speed);
        var steps = 0;
        while (accumulator >= Tuning.SimulationTickSeconds && steps < (int)Tuning.MaxCatchUpStepsPerFrame)
        {
            SimulateTick(entity, ctx);
            accumulator -= Tuning.SimulationTickSeconds;
            steps++;
            if (ReadBool(entity, DemoKeys.Initialized, false) == false)
                break;
        }

        entity.SetData(DemoKeys.Accumulator, accumulator);
    }

    private static void EnsureDefaults(ISndEntity entity)
    {
        if (!entity.TryGetData<string>(DemoKeys.Phase).found)
            entity.SetData(DemoKeys.Phase, DemoKeys.PhaseMenu);
        if (!entity.TryGetData<int>(DemoKeys.Seed).found)
            entity.SetData(DemoKeys.Seed, Random.Shared.Next(1, int.MaxValue));
        if (!entity.TryGetData<float>(DemoKeys.MutationRate).found)
            entity.SetData(DemoKeys.MutationRate, (float)Tuning.DefaultMutationRate);
        if (!entity.TryGetData<int>(DemoKeys.FoodPreset).found)
            entity.SetData(DemoKeys.FoodPreset, 1);
        if (!entity.TryGetData<int>(DemoKeys.Speed).found)
            entity.SetData(DemoKeys.Speed, 1);
        if (!entity.TryGetData<bool>(DemoKeys.Paused).found)
            entity.SetData(DemoKeys.Paused, false);
        if (!entity.TryGetData<float>(DemoKeys.Time).found)
            entity.SetData(DemoKeys.Time, 0f);
        if (!entity.TryGetData<int>(DemoKeys.Tick).found)
            entity.SetData(DemoKeys.Tick, 0);
        if (!entity.TryGetData<float>(DemoKeys.Accumulator).found)
            entity.SetData(DemoKeys.Accumulator, 0f);
        if (!entity.TryGetData<float>(DemoKeys.FoodAccumulator).found)
            entity.SetData(DemoKeys.FoodAccumulator, 0f);
        if (!entity.TryGetData<bool>(DemoKeys.Initialized).found)
            entity.SetData(DemoKeys.Initialized, false);
        if (!entity.TryGetData<int>(DemoKeys.NextCreatureId).found)
            entity.SetData(DemoKeys.NextCreatureId, 1);
        if (!entity.TryGetData<int>(DemoKeys.NextFoodId).found)
            entity.SetData(DemoKeys.NextFoodId, 1);
        if (!entity.TryGetData<int>(DemoKeys.NextLineageId).found)
            entity.SetData(DemoKeys.NextLineageId, 1);
        if (!entity.TryGetData<int>(DemoKeys.MaxGeneration).found)
            entity.SetData(DemoKeys.MaxGeneration, 0);
        if (!entity.TryGetData<int>(DemoKeys.FeedRequest).found)
            entity.SetData(DemoKeys.FeedRequest, 0);
        if (!entity.TryGetData<bool>(DemoKeys.FastForwardActive).found)
            entity.SetData(DemoKeys.FastForwardActive, false);
        if (!entity.TryGetData<float>(DemoKeys.FastForwardStart).found)
            entity.SetData(DemoKeys.FastForwardStart, 0f);
        if (!entity.TryGetData<float>(DemoKeys.FastForwardTarget).found)
            entity.SetData(DemoKeys.FastForwardTarget, 0f);
        if (!entity.TryGetData<float>(DemoKeys.FastForwardProgress).found)
            entity.SetData(DemoKeys.FastForwardProgress, 0f);
        if (!entity.TryGetData<float>(DemoKeys.RealSeconds).found)
            entity.SetData(DemoKeys.RealSeconds, 0f);
        if (!entity.TryGetData<bool>(DemoKeys.CollisionEnabled).found)
            entity.SetData(DemoKeys.CollisionEnabled, Tuning.CollisionEnabled);
        if (!entity.TryGetData<bool>(DemoKeys.BloomEnabled).found)
            entity.SetData(DemoKeys.BloomEnabled, Tuning.EarlyFoodBloomEnabled);
        var configResult = entity.TryGetData<string>(DemoKeys.ConfigJson);
        if (configResult.found && configResult.value is not null)
            DemoSettings.Current = DemoSettings.FromJson(configResult.value);
        else
            entity.SetData(DemoKeys.ConfigJson, DemoSettings.Current.ToJson());
        if (!entity.TryGetData<int>(DemoKeys.FoodPatchCount).found)
            entity.SetData(DemoKeys.FoodPatchCount, Tuning.FoodPatchCount);
    }

    private static void StartWorld(ISndEntity controller, ISndContext ctx)
    {
        var seed = ReadInt(controller, DemoKeys.Seed, 1);
        var rng = new Random(seed);
        var foodPreset = Math.Clamp(ReadInt(controller, DemoKeys.FoodPreset, 1), 0, 2);
        var patchCount = Math.Clamp(ReadInt(controller, DemoKeys.FoodPatchCount, Tuning.FoodPatchCount), 0, 64);
        var bloomEnabled = ReadBool(controller, DemoKeys.BloomEnabled, Tuning.EarlyFoodBloomEnabled);
        var foodTarget = FoodTargetForPreset(foodPreset, 0f, bloomEnabled);
        var metas = new List<SndMetaData>();
        var nextCreature = 1;
        var nextFood = 1;
        const int founderLineages = 6;

        for (var i = 0; i < Tuning.InitialPopulation; i++)
        {
            var position = RandomPosition(rng);
            var genome = CreatureGenome.RandomFounder(rng);
            var phenotype = genome.Phenotype;
            metas.Add(CreateCreatureMeta(
                CreatureName(nextCreature++),
                genome,
                phenotype,
                position.X,
                position.Y,
                NextHeading(rng),
                MathF.Min(Tuning.InitialEnergy, phenotype.MaxEnergy),
                0,
                (i % founderLineages) + 1,
                0f));

            // A few guaranteed starter nutrients near each founder prevent a
            // random-spawn drought from killing the initial generation before
            // evolution can begin.
            for (var snack = 0; snack < Tuning.StartupFoodPerFounder; snack++)
            {
                var foodPosition = ClampToPond(position + RandomOffset(rng), phenotype.FoodPickupRadius);
                metas.Add(CreateFoodMeta(FoodName(nextFood++), foodPosition.X, foodPosition.Y));
            }
        }

        var initialFood = Math.Min(Tuning.InitialFood, foodTarget);
        for (var i = 0; i < initialFood; i++)
        {
            var position = RandomFoodPosition(rng, seed, patchCount);
            metas.Add(CreateFoodMeta(FoodName(nextFood++), position.X, position.Y));
        }

        for (var patch = 0; patch < patchCount; patch++)
            metas.Add(CreateFoodSourceMeta(SourceName(patch), patch, patchCount, seed));

        controller.SetData(DemoKeys.Initialized, true);
        controller.SetData(DemoKeys.NextCreatureId, nextCreature);
        controller.SetData(DemoKeys.NextFoodId, nextFood);
        controller.SetData(DemoKeys.NextLineageId, founderLineages + 1);
        controller.SetData(DemoKeys.MaxGeneration, 0);
        controller.SetData(DemoKeys.Time, 0f);
        controller.SetData(DemoKeys.Tick, 0);
        controller.SetData(DemoKeys.Accumulator, 0f);
        controller.SetData(DemoKeys.FoodAccumulator, 0f);
        controller.SetData(DemoKeys.Paused, false);
        controller.SetData(DemoKeys.FeedRequest, 0);
        controller.SetData(DemoKeys.SpikeAttacks, 0);
        controller.SetData(DemoKeys.SpikeKills, 0);
        controller.SetData(DemoKeys.Phase, DemoKeys.PhaseStarting);
        controller.SetData(DemoKeys.GenomeVersion, 2);

        var session = controller.OwningSession;
        ctx.Deferred.EnqueueBusinessDeferred(() =>
        {
            session.SpawnMany(metas.ToArray());
            controller.SetData(DemoKeys.Phase, DemoKeys.PhaseRunning);
        });
    }

    private static void SimulateTick(ISndEntity controller, ISndContext ctx)
    {
        var session = controller.OwningSession;
        var entities = session.GetEntities();
        var creatures = CreatureScratch;
        creatures.Clear();
        var foods = FoodScratch;
        foods.Clear();
        foreach (var entity in entities)
        {
            if (entity.IsPendingKill)
                continue;
            var kindResult = entity.TryGetData<string>(DemoKeys.Kind);
            if (!kindResult.found)
                continue;
            var kind = kindResult.value;
            if (string.Equals(kind, DemoKeys.CreatureKind, StringComparison.Ordinal))
            {
                creatures.Add(new CreatureSnapshot(
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
                    ReadFloat(entity, DemoKeys.MaxAge, Tuning.MaxAgeSeconds)));
            }
            else if (string.Equals(kind, DemoKeys.FoodKind, StringComparison.Ordinal))
            {
                foods.Add(new FoodSnapshot(
                    entity,
                    ReadFloat(entity, DemoKeys.X, 0f),
                    ReadFloat(entity, DemoKeys.Y, 0f)));
            }
        }

        if (FoodPointScratch.Length < foods.Count)
            FoodPointScratch = new SensorPoint[foods.Count];
        if (CreaturePointScratch.Length < creatures.Count)
            CreaturePointScratch = new SensorPoint[creatures.Count];
        if (FoodConsumedScratch.Length < foods.Count)
            FoodConsumedScratch = new bool[foods.Count];
        var foodPoints = FoodPointScratch;
        for (var i = 0; i < foods.Count; i++)
            foodPoints[i] = new SensorPoint(i, new NumericsVector2(foods[i].X, foods[i].Y));
        var creaturePoints = CreaturePointScratch;
        for (var i = 0; i < creatures.Count; i++)
            creaturePoints[i] = new SensorPoint(i, new NumericsVector2(creatures[i].X, creatures[i].Y));

        var sensorGrid = SensorGrid.Build(foodPoints, foods.Count, creaturePoints, creatures.Count);
        var nearFood = NearFoodScratch;
        nearFood.Clear();
        var nearCreatures = NearCreatureScratch;
        nearCreatures.Clear();
        var foodConsumed = FoodConsumedScratch;
        Array.Clear(foodConsumed, 0, foods.Count);
        var foodAlive = foods.Count;
        var pendingChildren = 0;
        var pendingFood = 0;
        var mutations = MutationScratch;
        mutations.Clear();
        var collisionBodies = CollisionBodyScratch;
        collisionBodies.Clear();
        var collisionEntities = CollisionEntityScratch;
        collisionEntities.Clear();
        var bodyMutationRate = (float)Tuning.BodyMutationRate;
        var inspectTarget = ReadString(controller, DemoKeys.InspectTarget, string.Empty);
        var bloomEnabled = ReadBool(controller, DemoKeys.BloomEnabled, Tuning.EarlyFoodBloomEnabled);
        var collisionEnabled = ReadBool(controller, DemoKeys.CollisionEnabled, Tuning.CollisionEnabled);
        var patchCount = Math.Clamp(ReadInt(controller, DemoKeys.FoodPatchCount, Tuning.FoodPatchCount), 0, 64);
        var seed = ReadInt(controller, DemoKeys.Seed, 1);
        var nextCreatureId = ReadInt(controller, DemoKeys.NextCreatureId, 1);
        var nextLineageId = ReadInt(controller, DemoKeys.NextLineageId, 1);
        var nextFoodId = ReadInt(controller, DemoKeys.NextFoodId, 1);
        var maxGeneration = ReadInt(controller, DemoKeys.MaxGeneration, 0);
        var rng = new Random(unchecked((ReadInt(controller, DemoKeys.Seed, 1) * 397) ^ ReadInt(controller, DemoKeys.Tick, 0)));
        var alive = 0;
        var spikeAttacks = 0;
        var spikeKills = 0;
        var tickDt = Tuning.SimulationTickSeconds;
        var pondTime = ReadFloat(controller, DemoKeys.Time, 0f);
        var sensorBuffer = new float[NeuralGenome.SensorSlots];
        var aiStride = Math.Max(1, (creatures.Count + Tuning.AiUpdateBatch - 1) / Tuning.AiUpdateBatch);
        var tickNumber = ReadInt(controller, DemoKeys.Tick, 0);

        for (var i = 0; i < creatures.Count; i++)
        {
            var creature = creatures[i];
            if (creature.Entity.IsPendingKill)
                continue;
            var entity = creature.Entity;
            var bodyGenome = string.Empty;
            var brainGenome = string.Empty;
            var updateAi = tickNumber == 0 || ((i + tickNumber) % aiStride == 0);
            var drive = Math.Clamp(ReadFloat(entity, DemoKeys.Drive, 0f), 0f, 1f);
            var turn = Math.Clamp(ReadFloat(entity, DemoKeys.Turn, 0f), -1f, 1f);
            var spikeDrive = Math.Clamp(ReadFloat(entity, DemoKeys.SpikeDrive, 0f), -1f, 1f);
            var output = default(BrainOutput);
            if (updateAi)
            {
                bodyGenome = ReadString(entity, DemoKeys.BodyGenome, string.Empty);
                brainGenome = ReadString(entity, DemoKeys.BrainGenome, string.Empty);
                if (!BodyGenome.IsStructurallyValid(bodyGenome)
                    || !NeuralGenome.IsValid(brainGenome))
                {
                    var fallback = CreatureGenome.RandomFounder(rng);
                    bodyGenome = fallback.Body;
                    brainGenome = fallback.Brain;
                    entity.SetData(DemoKeys.BodyGenome, bodyGenome);
                    entity.SetData(DemoKeys.BrainGenome, brainGenome);
                }

                var body = ReadPhenotype(entity);
                var brain = GetBrain(bodyGenome, brainGenome);
                sensorGrid.Query(creature.X, creature.Y, sensorGrid.Food, nearFood);
                sensorGrid.Query(creature.X, creature.Y, sensorGrid.Creatures, nearCreatures);
                SensorMath.ComputeInto(
                    body,
                    new NumericsVector2(creature.X, creature.Y),
                    creature.Heading,
                    creature.Energy,
                    CollectionsMarshal.AsSpan(nearFood),
                    CollectionsMarshal.AsSpan(nearCreatures),
                    i,
                    creature.Age * 2f * MathF.PI / Tuning.OscillatorPeriodSeconds,
                    Tuning.PondRadius,
                    sensorBuffer);
                output = brain.Evaluate(body, sensorBuffer);
                drive = Math.Clamp(output.MoveDrive, 0f, 1f);
                turn = Math.Clamp(output.TurnDrive, -1f, 1f);
                spikeDrive = Math.Clamp(output.SpikeDrive, -1f, 1f);
                entity.SetData(DemoKeys.Drive, drive);
                entity.SetData(DemoKeys.Turn, turn);
                entity.SetData(DemoKeys.SpikeDrive, spikeDrive);
            }
            var heading = creature.Heading + (turn * creature.TurnSpeed * tickDt);
            var position = new NumericsVector2(creature.X, creature.Y);
            if (drive > Tuning.DriveThreshold)
            {
                var forward = new NumericsVector2(MathF.Cos(heading), MathF.Sin(heading));
                position += forward * (creature.MoveSpeed * drive * tickDt);
            }

            var bodyLimit = Tuning.PondRadius - creature.BodyRadius;
            if (position.LengthSquared() > bodyLimit * bodyLimit)
                position = NumericsVector2.Normalize(position) * bodyLimit;

            var energy = creature.Energy;
            var age = creature.Age + tickDt;
            var cooldown = Math.Max(0f, creature.ReproCooldown - tickDt);
            energy -= (float)(((Tuning.BaseMetabolismPerSecond * creature.BasalMetabolism)
                + (Tuning.MoveEnergyPerSecond * drive)
                + (Tuning.TurnEnergyPerSecond * Math.Abs(turn))) * tickDt);
            energy = MathF.Min(energy, creature.MaxEnergy);

            var ate = false;
            sensorGrid.Query(position.X, position.Y, sensorGrid.Food, nearFood);
            foreach (var point in nearFood)
            {
                var foodIndex = point.Id;
                if (foodConsumed[foodIndex])
                    continue;
                var delta = new NumericsVector2(foods[foodIndex].X, foods[foodIndex].Y) - position;
                if (delta.LengthSquared() > creature.FoodPickupRadius * creature.FoodPickupRadius)
                    continue;
                foodConsumed[foodIndex] = true;
                foodAlive--;
                session.RequestKillEntity(foods[foodIndex].Entity.Name);
                energy = MathF.Min(energy + Tuning.FoodEnergy, creature.MaxEnergy);
                ate = true;
                break;
            }

            var spikeCooldown = Math.Max(0f, creature.SpikeCooldown - tickDt);
            if (spikeDrive >= Tuning.SpikeThreshold
                && energy >= Tuning.SpikeEnergyCost
                && spikeCooldown <= 0f)
            {
                energy -= Tuning.SpikeEnergyCost;
                spikeCooldown = Tuning.SpikeCooldownSeconds;
                entity.SetData(DemoKeys.SpikeFlash, pondTime);
                var targets = 0;
                sensorGrid.Query(position.X, position.Y, sensorGrid.Creatures, nearCreatures);
                foreach (var point in nearCreatures)
                {
                    if (point.Id == i)
                        continue;
                    var target = creatures[point.Id];
                    if (target.Entity.IsPendingKill)
                        continue;
                    var dx = target.X - position.X;
                    var dy = target.Y - position.Y;
                    if ((dx * dx) + (dy * dy) > creature.SpikeRadius * creature.SpikeRadius)
                        continue;
                    session.RequestKillEntity(target.Entity.Name);
                    targets++;
                }

                spikeAttacks++;
                spikeKills += targets;
            }

            if (!ate
                && energy >= Tuning.ReproductionEnergy
                && cooldown <= 0f
                && creatures.Count + pendingChildren < Tuning.PopulationCap)
            {
                if (bodyGenome.Length == 0 || brainGenome.Length == 0)
                {
                    bodyGenome = ReadString(entity, DemoKeys.BodyGenome, string.Empty);
                    brainGenome = ReadString(entity, DemoKeys.BrainGenome, string.Empty);
                }

                var parentGenome = new CreatureGenome(bodyGenome, brainGenome);
                var generation = Math.Max(0, ReadInt(entity, DemoKeys.Generation, 0));
                var lineage = Math.Max(0, ReadInt(entity, DemoKeys.Lineage, 1));
                var children = Math.Max(0, ReadInt(entity, DemoKeys.Children, 0));
                var childGenome = CreatureGenome.Mutate(
                    parentGenome,
                    bodyMutationRate,
                    Tuning.BodyLengthMutationRate,
                    Tuning.FrameshiftMutationRate,
                    Tuning.BrainMutationRate,
                    rng,
                    out var changes);
                if (changes >= Tuning.LineageMutationThreshold)
                    lineage = nextLineageId++;
                var bodyLengthCost = Tuning.ReproductionParentCost
                    * (1f + (0.05f * MathF.Log(1f + (bodyGenome.Length / 72f))));
                energy -= bodyLengthCost;
                cooldown = Tuning.ReproductionCooldownSeconds;
                children++;
                generation++;
                maxGeneration = Math.Max(maxGeneration, generation);
                var childPhenotype = childGenome.Phenotype;
                var childPosition = ClampToPond(
                    position + RandomOffset(rng),
                    childPhenotype.BodyRadius);
                mutations.Add(CreateCreatureMeta(
                    CreatureName(nextCreatureId++),
                    childGenome,
                    childPhenotype,
                    childPosition.X,
                    childPosition.Y,
                    heading,
                    MathF.Min(Tuning.ReproductionChildEnergy, childPhenotype.MaxEnergy),
                    generation,
                    lineage,
                    0f));
                entity.SetData(DemoKeys.Children, children);
                pendingChildren++;
            }

            entity.SetData(DemoKeys.PrevX, creature.X);
            entity.SetData(DemoKeys.PrevY, creature.Y);
            entity.SetData(DemoKeys.PrevHeading, creature.Heading);
            entity.SetData(DemoKeys.X, position.X);
            entity.SetData(DemoKeys.Y, position.Y);
            entity.SetData(DemoKeys.Heading, heading);
            entity.SetData(DemoKeys.Energy, energy);
            entity.SetData(DemoKeys.Age, age);
            entity.SetData(DemoKeys.ReproCooldown, cooldown);
            entity.SetData(DemoKeys.SpikeCooldown, spikeCooldown);
            if (updateAi
                && inspectTarget.Length > 0
                && string.Equals(inspectTarget, entity.Name, StringComparison.Ordinal))
            {
                entity.SetData(DemoKeys.Sensors, FormatSensors(sensorBuffer));
                entity.SetData(DemoKeys.Outputs, string.Create(
                    CultureInfo.InvariantCulture,
                    $"M {output.MoveDrive:0.00} / T {output.TurnDrive:0.00} / S {output.SpikeDrive:0.00}"));
            }

            if (energy <= 0f || age >= creature.MaxAge)
            {
                // Dead matter feeds the survivors. This closes the early loop
                // where a bad generation would otherwise leave an empty pond
                // with no nutrient recovery path.
                var detritus = Math.Min(
                    Tuning.DeathFoodCount,
                    Math.Max(0, Tuning.FoodCap - (foodAlive + pendingFood)));
                for (var bite = 0; bite < detritus; bite++)
                {
                    var foodPosition = ClampToPond(position + RandomOffset(rng), creature.FoodPickupRadius);
                    mutations.Add(CreateFoodMeta(FoodName(nextFoodId++), foodPosition.X, foodPosition.Y));
                    foodAlive++;
                    pendingFood++;
                }

                session.RequestKillEntity(entity.Name);
                continue;
            }

            alive++;
            collisionBodies.Add(new CollisionBody { X = position.X, Y = position.Y, Radius = creature.BodyRadius });
            collisionEntities.Add(entity);
        }

        if (collisionEnabled && collisionBodies.Count > 1)
        {
            var bodyArray = collisionBodies.ToArray();
            if (bodyArray.Length >= 128)
                CreatureCollision.ResolveSweep(bodyArray, Tuning.PondRadius);
            else
                CreatureCollision.Resolve(
                    bodyArray,
                    Tuning.PondRadius,
                    Tuning.CollisionIterations);
            for (var i = 0; i < bodyArray.Length; i++)
            {
                collisionEntities[i].SetData(DemoKeys.X, bodyArray[i].X);
                collisionEntities[i].SetData(DemoKeys.Y, bodyArray[i].Y);
            }
        }

        var foodTarget = FoodTargetForPreset(ReadInt(controller, DemoKeys.FoodPreset, 1), pondTime, bloomEnabled);
        var bloomRate = bloomEnabled && pondTime < Tuning.EarlyFoodRampSeconds
            ? Tuning.EarlyFoodSpawnBonus * (1f - (pondTime / Tuning.EarlyFoodRampSeconds))
            : 0f;
        var foodAccumulator = ReadFloat(controller, DemoKeys.FoodAccumulator, 0f)
            + ((Tuning.FoodPerSecond + bloomRate) * tickDt);
        var feedRequest = ReadInt(controller, DemoKeys.FeedRequest, 0);
        while (feedRequest > 0 && foodAlive < Tuning.FoodCap)
        {
            var position = RandomFoodPosition(rng, seed, patchCount);
            mutations.Add(CreateFoodMeta(FoodName(nextFoodId++), position.X, position.Y));
            foodAlive++;
            pendingFood++;
            feedRequest--;
        }

        if (patchCount <= 0)
        {
            while (foodAccumulator >= 1f
                && foodAlive + pendingFood < foodTarget
                && foodAlive < Tuning.FoodCap)
            {
                var position = RandomFoodPosition(rng, seed, patchCount);
                mutations.Add(CreateFoodMeta(FoodName(nextFoodId++), position.X, position.Y));
                foodAccumulator -= 1f;
                foodAlive++;
                pendingFood++;
            }
        }
        else
        {
            // Patch sources are the only natural food producer when patches
            // are active; keep the accumulator bounded so a later uniform
            // configuration does not inherit a stale burst.
            foodAccumulator = Math.Min(foodAccumulator, 1f);
        }

        controller.SetData(DemoKeys.Time, ReadFloat(controller, DemoKeys.Time, 0f) + tickDt);
        controller.SetData(DemoKeys.Tick, ReadInt(controller, DemoKeys.Tick, 0) + 1);
        controller.SetData(DemoKeys.NextCreatureId, nextCreatureId);
        controller.SetData(DemoKeys.NextLineageId, nextLineageId);
        controller.SetData(DemoKeys.NextFoodId, nextFoodId);
        controller.SetData(DemoKeys.MaxGeneration, maxGeneration);
        controller.SetData(DemoKeys.FoodAccumulator, foodAccumulator);
        controller.SetData(DemoKeys.FeedRequest, feedRequest);

        if (mutations.Count > 0)
        {
            var spawn = mutations.ToArray();
            ctx.Deferred.EnqueueBusinessDeferred(() => session.SpawnMany(spawn));
        }

        controller.SetData(DemoKeys.SpikeAttacks, ReadInt(controller, DemoKeys.SpikeAttacks, 0) + spikeAttacks);
        controller.SetData(DemoKeys.SpikeKills, ReadInt(controller, DemoKeys.SpikeKills, 0) + spikeKills);

        // Food sources are tick-driven, not render-frame driven: run them once
        // per fixed simulation step so walk/scan/spawn clocks are reproducible
        // at every speed and during timed iteration.
        FoodSourceStrategy.TickAll(controller, ctx);

        if (alive == 0 && pendingChildren == 0 && ReadBool(controller, DemoKeys.Initialized, false))
            controller.SetData(DemoKeys.Phase, DemoKeys.PhaseExtinct);
    }

    private static SndMetaData CreateFoodSourceMeta(string name, int patchIndex, int patchCount, int seed) =>
        new SndMetaFluentBuilder(name)
            .SetNode("main", EvolutionEntityFactory.FoodScene)
            .SetString(DemoKeys.Kind, DemoKeys.FoodSourceKind)
            .SetInt(DemoKeys.PatchIndex, patchIndex)
            .SetInt(DemoKeys.PatchCount, patchCount)
            .SetInt(DemoKeys.Seed, seed)
            .AddLifecycleStrategy("evolution.foodsource")
            .Build();

    private static SndMetaData CreateCreatureMeta(
        string name,
        CreatureGenome genome,
        BodyPhenotype phenotype,
        float x,
        float y,
        float heading,
        float energy,
        int generation,
        int lineage,
        float cooldown) =>
        EvolutionEntityFactory.Creature(
            name,
            genome,
            phenotype,
            x,
            y,
            heading,
            energy,
            generation,
            lineage,
            cooldown);

    private static SndMetaData CreateFoodMeta(string name, float x, float y) =>
        EvolutionEntityFactory.Food(name, x, y);

    private sealed class SensorGrid
    {
        private readonly List<SensorPoint>[] _food;
        private readonly List<SensorPoint>[] _creatures;
        private readonly int _dimension;
        private readonly float _cellSize;

        private SensorGrid(
            List<SensorPoint>[] food,
            List<SensorPoint>[] creatures,
            int dimension,
            float cellSize)
        {
            _food = food;
            _creatures = creatures;
            _dimension = dimension;
            _cellSize = cellSize;
        }

        /// <summary>Food buckets, addressed by the grid's cell index.</summary>
        public List<SensorPoint>[] Food => _food;

        /// <summary>Creature buckets, addressed by the grid's cell index.</summary>
        public List<SensorPoint>[] Creatures => _creatures;

        private static SensorGrid? GridScratch;

        public static SensorGrid Build(
            SensorPoint[] food,
            int foodCount,
            SensorPoint[] creatures,
            int creatureCount)
        {
            var cellSize = MathF.Max(Tuning.SenseRange, Tuning.PondRadius / 10f);
            var dimension = Math.Max(1, (int)MathF.Ceiling((Tuning.PondRadius * 2f) / cellSize) + 1);
            var grid = GridScratch;
            if (grid is null
                || grid._dimension != dimension
                || MathF.Abs(grid._cellSize - cellSize) > 0.001f)
            {
                var foodBuckets = new List<SensorPoint>[dimension * dimension];
                var creatureBuckets = new List<SensorPoint>[dimension * dimension];
                for (var i = 0; i < foodBuckets.Length; i++)
                {
                    foodBuckets[i] = [];
                    creatureBuckets[i] = [];
                }

                grid = new SensorGrid(foodBuckets, creatureBuckets, dimension, cellSize);
                GridScratch = grid;
            }
            else
            {
                for (var i = 0; i < grid._food.Length; i++)
                {
                    grid._food[i].Clear();
                    grid._creatures[i].Clear();
                }
            }

            for (var i = 0; i < foodCount; i++)
                grid._food[grid.CellIndex(food[i].Position.X, food[i].Position.Y)].Add(food[i]);
            for (var i = 0; i < creatureCount; i++)
                grid._creatures[grid.CellIndex(creatures[i].Position.X, creatures[i].Position.Y)].Add(creatures[i]);
            return grid;
        }

        public void Query(float x, float y, List<SensorPoint>[] buckets, List<SensorPoint> target)
        {
            target.Clear();
            var cellX = CellCoordinate(x);
            var cellY = CellCoordinate(y);
            for (var dy = -1; dy <= 1; dy++)
            {
                var row = cellY + dy;
                if (row < 0 || row >= _dimension)
                    continue;
                for (var dx = -1; dx <= 1; dx++)
                {
                    var column = cellX + dx;
                    if (column < 0 || column >= _dimension)
                        continue;
                    target.AddRange(buckets[(row * _dimension) + column]);
                }
            }
        }

        private int CellIndex(float x, float y) => (CellCoordinate(y) * _dimension) + CellCoordinate(x);

        private int CellCoordinate(float value)
        {
            var cell = (int)((value + Tuning.PondRadius) / _cellSize);
            return Math.Clamp(cell, 0, _dimension - 1);
        }
    }

    private static NumericsVector2 ClampToPond(NumericsVector2 position, float margin)
    {
        var limit = Tuning.PondRadius - margin;
        if (position.LengthSquared() <= limit * limit)
            return position;
        return NumericsVector2.Normalize(position) * limit;
    }

    private static NumericsVector2 RandomFoodPosition(Random rng, int seed, int patchCount)
    {
        if (patchCount <= 0)
            return RandomPosition(rng);

        var patch = rng.Next(patchCount);
        var point = FoodPatchDistribution.Sample(
            rng,
            seed,
            patch,
            patchCount,
            Tuning.PondRadius,
            Tuning.FoodPatchRadius,
            Tuning.FoodPatchFalloff);
        return ClampToPond(new NumericsVector2(point.X, point.Y), Tuning.FoodPickupRadius);
    }

    private static NumericsVector2 RandomPosition(Random rng)
    {
        var angle = rng.NextDouble() * TwoPi;
        var radius = Math.Sqrt(rng.NextDouble()) * (Tuning.PondRadius - 24d);
        return new NumericsVector2(
            (float)(Math.Cos(angle) * radius),
            (float)(Math.Sin(angle) * radius));
    }

    private static NumericsVector2 RandomOffset(Random rng)
    {
        var angle = rng.NextDouble() * TwoPi;
        var distance = 8d + (rng.NextDouble() * 10d);
        return new NumericsVector2(
            (float)(Math.Cos(angle) * distance),
            (float)(Math.Sin(angle) * distance));
    }

    private static float NextHeading(Random rng) => (float)(rng.NextDouble() * TwoPi);

    private static string CreatureName(int id) => $"creature_{id:000000}";

    private static string FoodName(int id) => $"food_{id:000000}";
    private static string SourceName(int patch) => $"food_source_{patch:000}";

    private static int FoodTargetForPreset(int preset, float time, bool bloomEnabled)
    {
        var baseTarget = preset switch
        {
            0 => 2500,
            2 => 5500,
            _ => 4000,
        };
        if (!bloomEnabled)
            return Math.Min(Tuning.FoodCap, baseTarget);

        var ramp = Math.Clamp(1f - (time / Tuning.EarlyFoodRampSeconds), 0f, 1f);
        return Math.Min(Tuning.FoodCap, baseTarget + (int)MathF.Round(Tuning.EarlyFoodBonus * ramp));
    }

    private static NeuralBrain GetBrain(string bodyGenome, string brainGenome)
    {
        var key = bodyGenome + "|" + brainGenome;
        if (BrainCache.TryGetValue(key, out var cached))
            return cached;
        if (BrainCache.Count >= BrainCacheLimit)
            BrainCache.Clear();
        var brain = NeuralGenome.Decode(brainGenome);
        BrainCache[key] = brain;
        return brain;
    }

    private static BodyPhenotype ReadPhenotype(ISndEntity entity) => new(
        ReadFloat(entity, DemoKeys.BodyRadius, Tuning.BodyRadius) / MathF.Max(0.5f, Tuning.BodyRadiusBase),
        ReadFloat(entity, DemoKeys.BodyRadius, Tuning.BodyRadius),
        ReadFloat(entity, DemoKeys.FoodPickupRadius, Tuning.FoodPickupRadius),
        ReadFloat(entity, DemoKeys.MaxEnergy, 200f),
        ReadFloat(entity, DemoKeys.MoveSpeed, Tuning.CreatureSpeed),
        ReadFloat(entity, DemoKeys.TurnSpeed, Tuning.TurnSpeed),
        ReadFloat(entity, DemoKeys.SenseRange, Tuning.SenseRange),
        ReadFloat(entity, DemoKeys.SpikeRadius, Tuning.SpikeRadius),
        ReadFloat(entity, DemoKeys.BasalMetabolism, 1f),
        ReadFloat(entity, DemoKeys.MaxAge, Tuning.MaxAgeSeconds),
        ReadBool(entity, DemoKeys.SpikeOrgan, true),
        ReadBool(entity, DemoKeys.RhythmOrgan, true),
        ReadBool(entity, DemoKeys.VisionOrgan, true),
        ReadBool(entity, DemoKeys.CreatureSenseOrgan, true),
        ReadInt(entity, DemoKeys.HiddenLayers, 2),
        ReadInt(entity, DemoKeys.HiddenWidth1, 5),
        ReadInt(entity, DemoKeys.HiddenWidth2, 5),
        ReadInt(entity, DemoKeys.HiddenWidth3, 5),
        0);

    private static string FormatSensors(float[] sensors) => string.Create(
        CultureInfo.InvariantCulture,
        $"F {sensors[0]:0.00} L {sensors[1]:0.00} R {sensors[2]:0.00} W {sensors[4]:0.00} C {sensors[3]:0.00} E {sensors[5]:0.00} R {sensors[6]:0.00} C {sensors[7]:0.00}");

    private static string ReadString(ISndEntity entity, string key, string fallback)
    {
        var (found, value) = entity.TryGetData<string>(key);
        return found && value is not null ? value : fallback;
    }

    private static int ReadInt(ISndEntity entity, string key, int fallback)
    {
        var (found, value) = entity.TryGetData<int>(key);
        return found ? value : fallback;
    }

    private static float ReadFloat(ISndEntity entity, string key, float fallback)
    {
        var (found, value) = entity.TryGetData<float>(key);
        return found ? value : fallback;
    }


    private static bool ReadBool(ISndEntity entity, string key, bool fallback)
    {
        var (found, value) = entity.TryGetData<bool>(key);
        return found ? value : fallback;
    }

    private readonly record struct CreatureSnapshot(
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
        float MaxAge);

    private readonly record struct FoodSnapshot(ISndEntity Entity, float X, float Y);
}
