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
using Origo.EvolutionDemo.Logic.V4;
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
    private static readonly Dictionary<string, (PhenotypeV4 Phenotype, CompiledBrainV4 Brain)> BrainCache = new(StringComparer.Ordinal);
    private static readonly List<CreatureSnapshot> CreatureScratch = [];
    private static readonly List<FoodSnapshot> FoodScratch = [];
    private static readonly List<SensorPoint> NearFoodScratch = [];
    private static readonly List<SensorPoint> NearCreatureScratch = [];
    private static readonly List<SndMetaData> MutationScratch = [];
    private static readonly List<CollisionBody> CollisionBodyScratch = [];
    private static readonly List<ISndEntity> CollisionEntityScratch = [];
    private static readonly List<CorpseSnapshot> CorpseScratch = [];
    private static SensorPoint[] FoodPointScratch = [];
    private static SensorPoint[] CreaturePointScratch = [];
    private static SensorPoint[] CorpsePointScratch = [];
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
        if (!entity.TryGetData<int>(DemoKeys.NextCorpseId).found)
            entity.SetData(DemoKeys.NextCorpseId, 1);
        if (!entity.TryGetData<int>(DemoKeys.CorpsesCreated).found)
            entity.SetData(DemoKeys.CorpsesCreated, 0);
        if (!entity.TryGetData<int>(DemoKeys.CarrionFoodReleased).found)
            entity.SetData(DemoKeys.CarrionFoodReleased, 0);
        if (!entity.TryGetData<int>(DemoKeys.CorpseMeals).found)
            entity.SetData(DemoKeys.CorpseMeals, 0);
        if (!entity.TryGetData<int>(DemoKeys.CarrionDetections).found)
            entity.SetData(DemoKeys.CarrionDetections, 0);
        if (!entity.TryGetData<int>(DemoKeys.FoodSoftMeals).found)
            entity.SetData(DemoKeys.FoodSoftMeals, 0);
        if (!entity.TryGetData<int>(DemoKeys.FoodToughMeals).found)
            entity.SetData(DemoKeys.FoodToughMeals, 0);
        if (!entity.TryGetData<int>(DemoKeys.FoodToxicMeals).found)
            entity.SetData(DemoKeys.FoodToxicMeals, 0);
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
        var nextCorpse = 1;
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
        controller.SetData(DemoKeys.NextCorpseId, nextCorpse);
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
        controller.SetData(DemoKeys.SpikeHits, 0);
        controller.SetData(DemoKeys.SpikeDrained, 0f);
        controller.SetData(DemoKeys.CorpsesCreated, 0);
        controller.SetData(DemoKeys.CarrionFoodReleased, 0);
        controller.SetData(DemoKeys.CorpseMeals, 0);
        controller.SetData(DemoKeys.CarrionDetections, 0);
        controller.SetData(DemoKeys.FoodSoftMeals, 0);
        controller.SetData(DemoKeys.FoodToughMeals, 0);
        controller.SetData(DemoKeys.FoodToxicMeals, 0);
        controller.SetData(DemoKeys.Phase, DemoKeys.PhaseStarting);
        controller.SetData(DemoKeys.GenomeVersion, 4);

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
        var corpses = CorpseScratch;
        corpses.Clear();
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
                    ReadFloat(entity, DemoKeys.MaxAge, Tuning.MaxAgeSeconds),
                    ReadBool(entity, DemoKeys.SpikeBattery, false),
                    ReadInt(entity, DemoKeys.SpikeCount, 1),
                    ReadFloat(entity, DemoKeys.SpikeSpread, 0f),
                    ReadFloat(entity, DemoKeys.SpikeJitter, 0f),
                    ReadBool(entity, DemoKeys.HardDigestOrgan, false),
                    ReadBool(entity, DemoKeys.ToxinResistanceOrgan, false)));
            }
            else if (string.Equals(kind, DemoKeys.FoodKind, StringComparison.Ordinal))
            {
                foods.Add(new FoodSnapshot(
                    entity,
                    ReadFloat(entity, DemoKeys.X, 0f),
                    ReadFloat(entity, DemoKeys.Y, 0f),
                    ReadBool(entity, DemoKeys.FoodFromCorpse, false),
                    (FoodType)Math.Clamp(ReadInt(entity, DemoKeys.FoodType, 0), 0, 2)));
            }
            else if (string.Equals(kind, DemoKeys.CorpseKind, StringComparison.Ordinal))
            {
                corpses.Add(new CorpseSnapshot(
                    entity,
                    ReadFloat(entity, DemoKeys.X, 0f),
                    ReadFloat(entity, DemoKeys.Y, 0f),
                    ReadInt(entity, DemoKeys.CorpseFoodRemaining, 0),
                    ReadFloat(entity, DemoKeys.CorpseTimer, 0f),
                    ReadFloat(entity, DemoKeys.CorpseInterval,
                        Tuning.CarcassFoodIntervalSeconds)));
            }
        }

        var sensableFoodCount = foods.Count + corpses.Count;
        if (FoodPointScratch.Length < sensableFoodCount)
            FoodPointScratch = new SensorPoint[sensableFoodCount];
        if (CreaturePointScratch.Length < creatures.Count)
            CreaturePointScratch = new SensorPoint[creatures.Count];
        if (FoodConsumedScratch.Length < foods.Count)
            FoodConsumedScratch = new bool[foods.Count];
        var foodPoints = FoodPointScratch;
        for (var i = 0; i < foods.Count; i++)
            foodPoints[i] = new SensorPoint(i, new NumericsVector2(foods[i].X, foods[i].Y));
        for (var i = 0; i < corpses.Count; i++)
        {
            foodPoints[foods.Count + i] = new SensorPoint(
                foods.Count + i,
                new NumericsVector2(corpses[i].X, corpses[i].Y));
        }
        var creaturePoints = CreaturePointScratch;
        for (var i = 0; i < creatures.Count; i++)
            creaturePoints[i] = new SensorPoint(i, new NumericsVector2(creatures[i].X, creatures[i].Y));

        if (CorpsePointScratch.Length < corpses.Count)
            CorpsePointScratch = new SensorPoint[corpses.Count];
        var corpsePoints = CorpsePointScratch;
        for (var i = 0; i < corpses.Count; i++)
            corpsePoints[i] = new SensorPoint(i, new NumericsVector2(corpses[i].X, corpses[i].Y));

        var sensorGrid = SensorGrid.Build(foodPoints, sensableFoodCount, creaturePoints, creatures.Count);
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
        var nextCorpseId = ReadInt(controller, DemoKeys.NextCorpseId, 1);
        var corpsesCreated = 0;
        var carrionFoodReleased = 0;
        var corpseMeals = 0;
        var softMeals = 0;
        var toughMeals = 0;
        var toxicMeals = 0;
        var maxGeneration = ReadInt(controller, DemoKeys.MaxGeneration, 0);
        var rng = new Random(unchecked((ReadInt(controller, DemoKeys.Seed, 1) * 397) ^ ReadInt(controller, DemoKeys.Tick, 0)));
        var alive = 0;
        var spikeAttacks = 0;
        var spikeKills = 0;
        var spikeHits = 0;
        var spikeDrained = 0f;
        var carrionDetections = 0;
        var tickDt = Tuning.SimulationTickSeconds;
        var pondTime = ReadFloat(controller, DemoKeys.Time, 0f);
        var sensorBuffer = new float[64];
        var aiStride = Math.Max(1, (creatures.Count + Tuning.AiUpdateBatch - 1) / Tuning.AiUpdateBatch);
        var tickNumber = ReadInt(controller, DemoKeys.Tick, 0);

        foreach (var corpse in corpses)
        {
            if (corpse.Entity.IsPendingKill)
                continue;
            var freeSlots = Math.Max(0, Tuning.FoodCap - (foodAlive + pendingFood));
            var release = CarcassMath.Advance(
                corpse.Timer,
                corpse.Remaining,
                corpse.Interval,
                tickDt,
                freeSlots);
            for (var pulse = 0; pulse < release.Pulses; pulse++)
            {
                var foodPosition = ClampToPond(
                    new NumericsVector2(corpse.X, corpse.Y) + RandomOffset(rng),
                    Tuning.FoodPickupRadius);
                mutations.Add(CreateFoodMeta(
                    FoodName(nextFoodId++),
                    foodPosition.X,
                    foodPosition.Y,
                    fromCorpse: true));
                foodAlive++;
                pendingFood++;
                carrionFoodReleased++;
            }

            corpse.Entity.SetData(DemoKeys.CorpseFoodRemaining, release.Remaining);
            corpse.Entity.SetData(DemoKeys.CorpseTimer, release.Timer);
            if (release.Remaining <= 0)
                session.RequestKillEntity(corpse.Entity.Name);
        }

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
            var aimDrive = Math.Clamp(ReadFloat(entity, DemoKeys.Aim, 0f), -1f, 1f);
            var output = default(BrainOutput);
            if (updateAi)
            {
                bodyGenome = ReadString(entity, DemoKeys.BodyGenome, string.Empty);
                brainGenome = ReadString(entity, DemoKeys.BrainGenome, string.Empty);
                if (!CreatureGenome.TryDecode(bodyGenome, out _))
                {
                    var fallback = CreatureGenome.RandomFounder(rng);
                    bodyGenome = fallback.Body;
                    entity.SetData(DemoKeys.BodyGenome, bodyGenome);
                }

                var body = ReadPhenotype(entity);
                var (brainPhenotype, compiledBrain) = GetBrainPhenotype(bodyGenome);
                sensorGrid.Query(creature.X, creature.Y, sensorGrid.Food, nearFood);
                sensorGrid.Query(creature.X, creature.Y, sensorGrid.Creatures, nearCreatures);
                DemoSensorRuntime.ComputeInto(
                    brainPhenotype,
                    new DemoSensorContext
                    {
                        Position = new System.Numerics.Vector2(creature.X, creature.Y),
                        Heading = creature.Heading,
                        Energy = creature.Energy,
                        MaxEnergy = creature.MaxEnergy,
                        SenseRange = body.SenseRange,
                        Food = nearFood,
                        Creatures = nearCreatures,
                        Corpses = new ArraySegment<SensorPoint>(corpsePoints, 0, corpses.Count),
                        SelfId = i,
                        OscillatorPhase = creature.Age * 2f * MathF.PI / Tuning.OscillatorPeriodSeconds,
                        ArenaRadius = Tuning.PondRadius,
                    },
                    sensorBuffer);
                if (sensorBuffer[(int)SensorKindV4.CorpseAhead] > 0f)
                    carrionDetections++;
                output = DemoPhenotypeAdapter.ToBrainOutput(body, compiledBrain, sensorBuffer);
                drive = Math.Clamp(output.MoveDrive, 0f, 1f);
                turn = Math.Clamp(output.TurnDrive, -1f, 1f);
                spikeDrive = Math.Clamp(output.SpikeDrive, -1f, 1f);
                aimDrive = Math.Clamp(output.AimDrive, -1f, 1f);
                entity.SetData(DemoKeys.Drive, drive);
                entity.SetData(DemoKeys.Turn, turn);
                entity.SetData(DemoKeys.SpikeDrive, spikeDrive);
                entity.SetData(DemoKeys.Aim, aimDrive);
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
                if (foodIndex < 0 || foodIndex >= foods.Count)
                    continue;
                if (foodConsumed[foodIndex])
                    continue;
                var delta = new NumericsVector2(foods[foodIndex].X, foods[foodIndex].Y) - position;
                if (delta.LengthSquared() > creature.FoodPickupRadius * creature.FoodPickupRadius)
                    continue;
                foodConsumed[foodIndex] = true;
                foodAlive--;
                if (foods[foodIndex].FromCorpse)
                {
                    corpseMeals++;
                    entity.SetData(
                        DemoKeys.CreatureCorpseMeals,
                        ReadInt(entity, DemoKeys.CreatureCorpseMeals, 0) + 1);
                }

                var foodType = foods[foodIndex].Type;
                var foodEnergy = FoodMath.EnergyFor(
                    foodType,
                    Tuning.FoodEnergy,
                    Tuning.ToughFoodEnergy,
                    Tuning.ToxicFoodEnergy,
                    creature.HardDigest,
                    creature.ToxinResistance);
                session.RequestKillEntity(foods[foodIndex].Entity.Name);
                energy = MathF.Min(energy + foodEnergy, creature.MaxEnergy);
                if (foodType == FoodType.Tough)
                    toughMeals++;
                else if (foodType == FoodType.Toxic)
                    toxicMeals++;
                else
                    softMeals++;
                ate = true;
                break;
            }

            var spikeCooldown = Math.Max(0f, creature.SpikeCooldown - tickDt);
            if (spikeDrive >= Tuning.SpikeThreshold
                && energy >= Tuning.SpikeEnergyCost
                && spikeCooldown <= 0f)
            {
                var count = creature.SpikeBattery
                    ? Math.Clamp(creature.SpikeCount, 2, 8)
                    : 1;
                var spread = creature.SpikeBattery
                    ? MathF.Max(0f, creature.SpikeSpread)
                    : 0f;
                var jitter = creature.SpikeBattery
                    ? MathF.Max(0f, creature.SpikeJitter)
                    : 0f;
                var aimAngle = heading + (aimDrive * Tuning.SpikeAimHalfAngleRadians);
                var range = creature.SpikeRadius;
                sensorGrid.Query(position.X, position.Y, range, sensorGrid.Creatures, nearCreatures);
                var pelletTargets = new int[count];
                var pelletDistances = new float[count];
                var pelletEnds = new string[count];
                var hitTargets = new int[count];
                var hitCount = 0;
                var maxHitDistance = 0f;
                for (var pellet = 0; pellet < count; pellet++)
                {
                    var fanOffset = count == 1
                        ? 0f
                        : -spread * 0.5f + (spread * ((pellet + 0.5f) / count));
                    var pelletAngle = aimAngle + fanOffset
                        + ((float)((rng.NextDouble() * 2d) - 1d) * jitter);
                    var aimX = MathF.Cos(pelletAngle);
                    var aimY = MathF.Sin(pelletAngle);
                    var targetIndex = -1;
                    var targetDistance = float.MaxValue;
                    foreach (var point in nearCreatures)
                    {
                        if (point.Id == i)
                            continue;
                        var candidate = creatures[point.Id];
                        if (candidate.Entity.IsPendingKill)
                            continue;
                        var alreadyHit = false;
                        for (var h = 0; h < hitCount; h++)
                        {
                            if (hitTargets[h] != point.Id)
                                continue;
                            alreadyHit = true;
                            break;
                        }

                        if (alreadyHit)
                            continue;
                        var dx = candidate.X - position.X;
                        var dy = candidate.Y - position.Y;
                        var along = (dx * aimX) + (dy * aimY);
                        if (along <= 0f || along > range || along >= targetDistance)
                            continue;
                        var perpendicularSquared = (dx * dx) + (dy * dy) - (along * along);
                        var hitRadius = candidate.BodyRadius + Tuning.SpikeBeamRadius;
                        if (perpendicularSquared > hitRadius * hitRadius)
                            continue;
                        targetIndex = point.Id;
                        targetDistance = along;
                    }

                    var endX = position.X + (aimX * range);
                    var endY = position.Y + (aimY * range);
                    if (targetIndex >= 0)
                    {
                        pelletTargets[pellet] = targetIndex;
                        pelletDistances[pellet] = targetDistance;
                        hitTargets[hitCount++] = targetIndex;
                        maxHitDistance = MathF.Max(maxHitDistance, targetDistance);
                        endX = position.X + (aimX * targetDistance);
                        endY = position.Y + (aimY * targetDistance);
                    }
                    else
                    {
                        pelletTargets[pellet] = -1;
                    }

                    pelletEnds[pellet] = string.Create(
                        CultureInfo.InvariantCulture,
                        $"{endX:0.###},{endY:0.###}");
                }

                var shotDistance = maxHitDistance > 0f ? maxHitDistance : range;
                var shotCost = Tuning.SpikeEnergyCost
                    + (Tuning.SpikePerPelletCost * count)
                    + (Tuning.SpikeEnergyPerUnit * shotDistance);
                if (energy >= shotCost)
                {
                    energy -= shotCost;
                    spikeCooldown = Tuning.SpikeCooldownSeconds
                        + (Tuning.SpikeCooldownPerExtraPellet * Math.Max(0, count - 1));
                    entity.SetData(DemoKeys.SpikeFlash, pondTime);
                    entity.SetData(DemoKeys.SpikeEndX, position.X);
                    entity.SetData(DemoKeys.SpikeEndY, position.Y);
                    entity.SetData(DemoKeys.SpikeFan, string.Join(';', pelletEnds));
                    spikeAttacks++;
                    for (var pellet = 0; pellet < count; pellet++)
                    {
                        var targetIndex = pelletTargets[pellet];
                        if (targetIndex < 0)
                            continue;
                        var target = creatures[targetIndex];
                        var targetEnergy = MathF.Max(0f, target.Energy);
                        var stolen = targetEnergy * Tuning.SpikeDrainFraction;
                        energy = MathF.Min(energy + stolen, creature.MaxEnergy);
                        target.Entity.SetData(DemoKeys.Energy, 0f);
                        session.RequestKillEntity(target.Entity.Name);
                        spikeHits++;
                        spikeKills++;
                        spikeDrained += stolen;
                        entity.SetData(DemoKeys.Kills, ReadInt(entity, DemoKeys.Kills, 0) + 1);

                        var corpsePosition = ClampToPond(
                            new NumericsVector2(target.X, target.Y),
                            Tuning.FoodPickupRadius);
                        if (Tuning.DeathFoodCount > 0)
                        {
                            mutations.Add(CreateCorpseMeta(
                                CorpseName(nextCorpseId++),
                                corpsePosition.X,
                                corpsePosition.Y,
                                Tuning.DeathFoodCount,
                                Tuning.CarcassFoodIntervalSeconds));
                            corpsesCreated++;
                        }
                    }
                }
            }

            if (!ate
                && energy >= Tuning.ReproductionEnergy
                && cooldown <= 0f
                && creatures.Count + pendingChildren < Tuning.PopulationCap)
            {
                if (bodyGenome.Length == 0)
                {
                    bodyGenome = ReadString(entity, DemoKeys.BodyGenome, string.Empty);
                }

                if (!CreatureGenome.TryDecode(bodyGenome, out var decodedParent))
                    continue;
                var parentGenome = decodedParent!;
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
                    * (1f + (0.05f * MathF.Log(1f + (parentGenome.GeneCount / 24f))));
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
                    $"M {output.MoveDrive:0.00} / T {output.TurnDrive:0.00} / S {output.SpikeDrive:0.00} / A {output.AimDrive:0.00}"));
            }

            if (energy <= 0f || age >= creature.MaxAge)
            {
                // Dead matter becomes a decaying carcass. The carcass persists
                // briefly and releases its nutrient pulses over time instead of
                // dropping all food in the same tick.
                if (Tuning.DeathFoodCount > 0)
                {
                    mutations.Add(CreateCorpseMeta(
                        CorpseName(nextCorpseId++),
                        position.X,
                        position.Y,
                        Tuning.DeathFoodCount,
                        Tuning.CarcassFoodIntervalSeconds));
                    corpsesCreated++;
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
        controller.SetData(DemoKeys.NextCorpseId, nextCorpseId);
        controller.SetData(DemoKeys.CorpsesCreated,
            ReadInt(controller, DemoKeys.CorpsesCreated, 0) + corpsesCreated);
        controller.SetData(DemoKeys.CarrionFoodReleased,
            ReadInt(controller, DemoKeys.CarrionFoodReleased, 0) + carrionFoodReleased);
        controller.SetData(DemoKeys.CorpseMeals,
            ReadInt(controller, DemoKeys.CorpseMeals, 0) + corpseMeals);
        controller.SetData(DemoKeys.CarrionDetections,
            ReadInt(controller, DemoKeys.CarrionDetections, 0) + carrionDetections);
        controller.SetData(DemoKeys.FoodSoftMeals,
            ReadInt(controller, DemoKeys.FoodSoftMeals, 0) + softMeals);
        controller.SetData(DemoKeys.FoodToughMeals,
            ReadInt(controller, DemoKeys.FoodToughMeals, 0) + toughMeals);
        controller.SetData(DemoKeys.FoodToxicMeals,
            ReadInt(controller, DemoKeys.FoodToxicMeals, 0) + toxicMeals);
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
        controller.SetData(DemoKeys.SpikeHits, ReadInt(controller, DemoKeys.SpikeHits, 0) + spikeHits);
        controller.SetData(DemoKeys.SpikeDrained, ReadFloat(controller, DemoKeys.SpikeDrained, 0f) + spikeDrained);

        // Food sources are tick-driven, not render-frame driven: run them once
        // per fixed simulation step so walk/scan/spawn clocks are reproducible
        // at every speed and during timed iteration. They reuse this tick's
        // spatial grid and the snapshot food count instead of rescanning the
        // whole session; the sources' scan/spawn clocks are phase-staggered so
        // no two sources scan or spawn on the same tick.
        FoodSourceStrategy.TickAll(controller, ctx, sensorGrid, foods.Count);

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

    private static SndMetaData CreateFoodMeta(
        string name,
        float x,
        float y,
        bool fromCorpse = false,
        FoodType foodType = FoodType.Soft) =>
        EvolutionEntityFactory.Food(name, x, y, fromCorpse, foodType);

    private static SndMetaData CreateCorpseMeta(
        string name,
        float x,
        float y,
        int foodRemaining,
        float intervalSeconds) =>
        EvolutionEntityFactory.Corpse(name, x, y, foodRemaining, intervalSeconds);

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

    private static string CorpseName(int id) => $"corpse_{id:000000}";
    private static string SourceName(int patch) => $"food_source_{patch:000}";

    private static int FoodTargetForPreset(int preset, float time, bool bloomEnabled)
    {
        var baseTarget = preset switch
        {
            0 => 2500,
            2 => 5500,
            _ => 3000,
        };
        if (!bloomEnabled)
            return Math.Min(Tuning.FoodCap, baseTarget);

        var ramp = Math.Clamp(1f - (time / Tuning.EarlyFoodRampSeconds), 0f, 1f);
        return Math.Min(Tuning.FoodCap, baseTarget + (int)MathF.Round(Tuning.EarlyFoodBonus * ramp));
    }

    private static (PhenotypeV4 Phenotype, CompiledBrainV4 Brain) GetBrainPhenotype(string bodyGenome)
    {
        if (BrainCache.TryGetValue(bodyGenome, out var cached))
            return cached;
        if (BrainCache.Count >= CacheEviction.DefaultLimit)
            CacheEviction.TrimOldestHalf(BrainCache);
        var genome = CreatureGenome.FromSerialized(bodyGenome);
        var phenotype = GenomeV4Compiler.Compile(genome.Genome, ExpressionContextV4.Neutral);
        var entry = (phenotype, GenomeV4Compiler.CompileBrain(phenotype));
        BrainCache[bodyGenome] = entry;
        return entry;
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
        0,
        ReadBool(entity, DemoKeys.SpikeBattery, false),
        ReadInt(entity, DemoKeys.SpikeCount, 1),
        ReadFloat(entity, DemoKeys.SpikeSpread, 0f),
        ReadFloat(entity, DemoKeys.SpikeJitter, 0f),
        ReadBool(entity, DemoKeys.HardDigestOrgan, false),
        ReadBool(entity, DemoKeys.ToxinResistanceOrgan, false),
        ReadBool(entity, DemoKeys.CarrionSenseOrgan, false));

    private static string FormatSensors(float[] sensors) => string.Create(
        CultureInfo.InvariantCulture,
        $"F {sensors[0]:0.00} L {sensors[1]:0.00} R {sensors[2]:0.00} W {sensors[4]:0.00} C {sensors[3]:0.00} E {sensors[5]:0.00} S/C {sensors[6]:0.00}/{sensors[7]:0.00} Carrion {sensors[8]:0.00}");

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
        float MaxAge,
        bool SpikeBattery,
        int SpikeCount,
        float SpikeSpread,
        float SpikeJitter,
        bool HardDigest,
        bool ToxinResistance);

    private readonly record struct FoodSnapshot(ISndEntity Entity, float X, float Y, bool FromCorpse, FoodType Type);

    private readonly record struct CorpseSnapshot(
        ISndEntity Entity,
        float X,
        float Y,
        int Remaining,
        float Timer,
        float Interval);
}
