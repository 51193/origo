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
using Origo.EvolutionDemo.Logic.Gameplay;
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
    private static readonly Dictionary<string, (PhenotypeV4 Phenotype, CompiledBrainV4 Brain, bool HasSpikePort)> BrainCache = new(StringComparer.Ordinal);

    /// <summary>
    ///     Reused per-creature spike-target buffer. The plan consumes it
    ///     synchronously, so one buffer per tick is safe and avoids a fresh list
    ///     allocation for every creature.
    /// </summary>
    private static readonly List<SpikeTargetV4> SpikeTargetScratch = [];
    private static readonly DemoActionEffectState ActionStateScratch = new();

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
        // Deferred spawn callbacks do not survive a process restart; a loaded
        // save must not reserve population or food slots for work that no longer exists.
        entity.SetData(DemoKeys.PendingCreatureSpawns, 0);
        entity.SetData(DemoKeys.PendingFoodSpawns, 0);
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
        if (!entity.TryGetData<int>(DemoKeys.GameplayMode).found)
            entity.SetData(DemoKeys.GameplayMode, (int)GameplayMode.Sandbox);
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
        if (!entity.TryGetData<int>(DemoKeys.Births).found)
            entity.SetData(DemoKeys.Births, 0);
        if (!entity.TryGetData<int>(DemoKeys.FoodPatchCount).found)
            entity.SetData(DemoKeys.FoodPatchCount, Tuning.FoodPatchCount);
        if (!entity.TryGetData<int>(DemoKeys.NextSourceIndex).found)
            entity.SetData(DemoKeys.NextSourceIndex, Tuning.FoodPatchCount);
        if (!entity.TryGetData<int>(DemoKeys.PendingSourceSpawns).found)
            entity.SetData(DemoKeys.PendingSourceSpawns, 0);
        if (!entity.TryGetData<int>(DemoKeys.PendingCreatureSpawns).found)
            entity.SetData(DemoKeys.PendingCreatureSpawns, 0);
        if (!entity.TryGetData<int>(DemoKeys.PendingFoodSpawns).found)
            entity.SetData(DemoKeys.PendingFoodSpawns, 0);
        if (!entity.TryGetData<int>(DemoKeys.CarrionFoodReleased).found)
            entity.SetData(DemoKeys.CarrionFoodReleased, 0);
        if (!entity.TryGetData<int>(DemoKeys.CorpseMeals).found)
            entity.SetData(DemoKeys.CorpseMeals, 0);
        if (!entity.TryGetData<int>(DemoKeys.CarrionDetections).found)
            entity.SetData(DemoKeys.CarrionDetections, 0);
        if (!entity.TryGetData<int>(DemoKeys.SprintActions).found)
            entity.SetData(DemoKeys.SprintActions, 0);
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
    }

    private static void StartWorld(ISndEntity controller, ISndContext ctx)
    {
        var seed = ReadInt(controller, DemoKeys.Seed, 1);
        var rng = new Random(seed);
        var foodPreset = Math.Clamp(ReadInt(controller, DemoKeys.FoodPreset, 1), 0, 2);
        var patchCount = Math.Clamp(ReadInt(controller, DemoKeys.FoodPatchCount, Tuning.FoodPatchCount), 0, 64);
        var bloomEnabled = ReadBool(controller, DemoKeys.BloomEnabled, Tuning.EarlyFoodBloomEnabled);
        var foodTarget = FoodTargetForPreset(foodPreset, 0f, bloomEnabled);
        var foodCap = Tuning.FoodCap;
        var startupFood = 0;
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
                if (startupFood >= foodCap)
                    break;
                var foodPosition = ClampToPond(position + RandomOffset(rng), phenotype.FoodPickupRadius);
                metas.Add(CreateFoodMeta(FoodName(nextFood++), foodPosition.X, foodPosition.Y));
                startupFood++;
            }
        }

        var initialFood = Math.Min(
            Math.Min(Tuning.InitialFood, foodTarget),
            Math.Max(0, foodCap - startupFood));
        for (var i = 0; i < initialFood; i++)
        {
            var position = RandomFoodPosition(rng, seed, patchCount);
            metas.Add(CreateFoodMeta(FoodName(nextFood++), position.X, position.Y));
        }

        for (var patch = 0; patch < patchCount; patch++)
            metas.Add(CreateFoodSourceMeta(SourceName(patch), patch, patchCount, seed));
        controller.SetData(DemoKeys.FoodPatchCount, patchCount);
        controller.SetData(DemoKeys.NextSourceIndex, patchCount);
        controller.SetData(DemoKeys.PendingSourceSpawns, 0);
        controller.SetData(DemoKeys.PendingCreatureSpawns, 0);
        controller.SetData(DemoKeys.PendingFoodSpawns, 0);

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
        controller.SetData(DemoKeys.Births, 0);
        controller.SetData(DemoKeys.CarrionDetections, 0);
        controller.SetData(DemoKeys.SprintActions, 0);
        controller.SetData(DemoKeys.FoodSoftMeals, 0);
        controller.SetData(DemoKeys.FoodToughMeals, 0);
        controller.SetData(DemoKeys.FoodToxicMeals, 0);
        controller.SetData(DemoKeys.Phase, DemoKeys.PhaseStarting);
        controller.SetData(DemoKeys.GenomeVersion, DemoEntry.GenomeVersion);
        var gameplay = EvolutionGameSession.For(ctx).Runtime;
        GameplayStateSync.Save(controller, gameplay);

        var session = controller.OwningSession;
        ctx.Deferred.EnqueueBusinessDeferred(() =>
        {
            session.SpawnMany(metas.ToArray());
            controller.SetData(DemoKeys.Phase, DemoKeys.PhaseRunning);
        });
    }

    private static void SimulateTick(ISndEntity controller, ISndContext ctx)
    {
        var session = EvolutionGameSession.For(ctx);
        var world = new EvolutionTickContext(controller, ctx, Tuning.SimulationTickSeconds, session);
        session.Profiler.Begin();
        session.Runtime.Pipeline.Tick(world, static _ => true,
            session.Profiler.Enabled
                ? (system, milliseconds, allocated) =>
                    session.Profiler.RecordSystem(system.Id, milliseconds, allocated)
                : null);
        session.Profiler.End(world);
    }

    /// <summary>Runs the core simulation system for one fixed tick.</summary>
    internal static void RunCoreSimulation(EvolutionTickContext world)
    {
        var controller = world.Controller;
        var session = controller.OwningSession;
        var snapshot = world.Snapshot
            ?? throw new InvalidOperationException("SnapshotSystem must run before core simulation.");
        var creatures = snapshot.Creatures;
        var foods = snapshot.Foods;
        var corpses = snapshot.Corpses;
        var sensorGrid = snapshot.Grid;
        var corpsePoints = snapshot.CorpsePoints;
        var nearFood = snapshot.NearFood;
        var nearCreatures = snapshot.NearCreatures;
        var foodConsumed = snapshot.FoodConsumed;
        var foodAlive = world.FoodAlive;
        var pendingChildren = 0;
        var pendingDeferredCreatures = ReadInt(controller, DemoKeys.PendingCreatureSpawns, 0);
        var pendingFood = world.PendingFood;
        var mutations = world.Mutations;
        var collisionBodies = world.CollisionBodies;
        collisionBodies.Clear();
        var collisionEntities = world.CollisionEntities;
        collisionEntities.Clear();
        var inspectTarget = ReadString(controller, DemoKeys.InspectTarget, string.Empty);
        var bloomEnabled = ReadBool(controller, DemoKeys.BloomEnabled, Tuning.EarlyFoodBloomEnabled);
        var collisionEnabled = ReadBool(controller, DemoKeys.CollisionEnabled, Tuning.CollisionEnabled);
        var patchCount = Math.Clamp(ReadInt(controller, DemoKeys.FoodPatchCount, Tuning.FoodPatchCount), 0, 64);
        var seed = ReadInt(controller, DemoKeys.Seed, 1);
        var nextCreatureId = ReadInt(controller, DemoKeys.NextCreatureId, 1);
        var nextLineageId = ReadInt(controller, DemoKeys.NextLineageId, 1);
        var nextFoodId = world.NextFoodId;
        var nextCorpseId = ReadInt(controller, DemoKeys.NextCorpseId, 1);
        var corpsesCreated = 0;
        var corpseMeals = 0;
        var softMeals = 0;
        var toughMeals = 0;
        var toxicMeals = 0;
        var maxGeneration = ReadInt(controller, DemoKeys.MaxGeneration, 0);
        var rng = world.Rng;
        var alive = 0;
        var spikeAttacks = 0;
        var spikeKills = 0;
        var spikeHits = 0;
        var spikeDrained = 0f;
        var carrionDetections = 0;
        var sprintActions = 0;
        Span<float> actionBuffer = stackalloc float[64];
        var tickDt = Tuning.SimulationTickSeconds;
        var pondTime = ReadFloat(controller, DemoKeys.Time, 0f);
        Span<float> sensorBuffer = stackalloc float[64];
        var aiStride = Math.Max(1, (creatures.Count + Tuning.AiUpdateBatch - 1) / Tuning.AiUpdateBatch);
        var tickNumber = ReadInt(controller, DemoKeys.Tick, 0);
        var profiler = world.Session.Profiler;
        var aiTicks = 0d;
        var aiAlloc = 0L;
        Span<double> markerTicks = stackalloc double[4];
        Span<long> markerAlloc = stackalloc long[4];
        markerTicks.Clear();
        markerAlloc.Clear();

        // A purchased organ unlock injects a few carriers immediately; waiting
        // for a random mutation can make a valid purchase look ineffective.
        if (world.Session.Runtime.Progression.Mode == GameplayMode.Campaign)
        {
            foreach (var unlockId in world.Session.Runtime.TakePendingInnovations())
            {
                var organMask = CampaignProgressionRules.InnovationOrganMask(unlockId);
                if (organMask is not { } mask)
                    continue;
                for (var injected = 0; injected < 3; injected++)
                {
                    if (creatures.Count + pendingChildren + pendingDeferredCreatures + mutations.Count
                        >= Tuning.PopulationCap)
                        break;
                    for (var attempt = 0; attempt < 200; attempt++)
                    {
                        var candidate = CreatureGenome.RandomFounder(rng);
                        if ((candidate.Phenotype.OrganMask & mask) == 0)
                            continue;
                        var position = RandomPosition(rng);
                        mutations.Add(CreateCreatureMeta(
                            CreatureName(nextCreatureId++),
                            candidate,
                            candidate.Phenotype,
                            position.X,
                            position.Y,
                            NextHeading(rng),
                            MathF.Min(Tuning.InitialEnergy, candidate.Phenotype.MaxEnergy),
                            0,
                            nextLineageId++,
                            0f));
                        pendingChildren++;
                        break;
                    }
                }
            }
        }

        for (var i = 0; i < creatures.Count; i++)
        {
            var creature = creatures[i];
            if (creature.Entity.IsPendingKill)
                continue;
            var entity = creature.Entity;
            var bodyGenome = string.Empty;
            var updateAi = AiBatchPhase.ShouldUpdate(creature.StableId, tickNumber, aiStride);
            var drive = Math.Clamp(ReadFloat(entity, DemoKeys.Drive, 0f), 0f, 1f);
            var turn = Math.Clamp(ReadFloat(entity, DemoKeys.Turn, 0f), -1f, 1f);
            var spikeDrive = Math.Clamp(ReadFloat(entity, DemoKeys.SpikeDrive, 0f), -1f, 1f);
            var aimDrive = Math.Clamp(ReadFloat(entity, DemoKeys.Aim, 0f), -1f, 1f);
            var sprintDrive = Math.Clamp(ReadFloat(entity, DemoKeys.SprintDrive, 0f), -1f, 1f);
            PhenotypeV4? brainPhenotype = null;
            var hasSpikePort = false;
            var aiStart = profiler.Enabled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;
            var aiAllocStart = profiler.Enabled ? GC.GetAllocatedBytesForCurrentThread() : 0L;
            if (updateAi)
            {
                bodyGenome = creature.BodyGenome;

                // One decode/compile per genome string: GetBrainPhenotype caches
                // the compiled graph and re-uses the entity's serialized genome.
                // A corrupt genome now fails fast here instead of being silently
                // replaced by a random founder, so invalid saves cannot hide.
                var decodeStart = profiler.Enabled ? GC.GetAllocatedBytesForCurrentThread() : 0L;
                var decodeTick = profiler.Enabled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;
                var (phenotype, compiledBrain, phenotypeHasSpikePort) = GetBrainPhenotype(bodyGenome);
                if (profiler.Enabled)
                {
                    markerTicks[0] += System.Diagnostics.Stopwatch.GetElapsedTime(decodeTick).TotalMilliseconds;
                    markerAlloc[0] += GC.GetAllocatedBytesForCurrentThread() - decodeStart;
                }

                brainPhenotype = phenotype;
                hasSpikePort = phenotypeHasSpikePort;
                var senseRange = creature.SenseRange;
                var sensorStart = profiler.Enabled ? GC.GetAllocatedBytesForCurrentThread() : 0L;
                var sensorTick = profiler.Enabled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;
                // Sensor cones use this creature's expressed sense range, which
                // can exceed the default Tuning.SenseRange; query the actual
                // range so the distance-filtered grid keeps every candidate the
                // cones can see.
                sensorGrid.QueryPair(
                    creature.X,
                    creature.Y,
                    senseRange,
                    sensorGrid.Food,
                    sensorGrid.Creatures,
                    nearFood,
                    nearCreatures);
                DemoSensorRuntime.ComputeInto(
                    phenotype,
                    new DemoSensorContext
                    {
                        Position = new System.Numerics.Vector2(creature.X, creature.Y),
                        Heading = creature.Heading,
                        Energy = creature.Energy,
                        MaxEnergy = creature.MaxEnergy,
                        SenseRange = senseRange,
                        Food = nearFood,
                        Creatures = nearCreatures,
                        Corpses = new ArraySegment<SensorPoint>(corpsePoints, 0, corpses.Count),
                        SelfId = i,
                        OscillatorPhase = creature.Age * 2f * MathF.PI / Tuning.OscillatorPeriodSeconds,
                        ArenaRadius = Tuning.PondRadius,
                        FoodTypes = WorldSnapshot.FoodTypes,
                        HardDigest = creature.HardDigest,
                        ToxinResistance = creature.ToxinResistance,
                    },
                    sensorBuffer);
                if (profiler.Enabled)
                {
                    markerTicks[1] += System.Diagnostics.Stopwatch.GetElapsedTime(sensorTick).TotalMilliseconds;
                    markerAlloc[1] += GC.GetAllocatedBytesForCurrentThread() - sensorStart;
                }

                if (sensorBuffer[(int)SensorKindV4.CorpseAhead] > 0f)
                    carrionDetections++;
                var brainStart = profiler.Enabled ? GC.GetAllocatedBytesForCurrentThread() : 0L;
                var brainTick = profiler.Enabled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;
                DemoActionRuntime.Evaluate(phenotype, compiledBrain, sensorBuffer, actionBuffer);
                if (profiler.Enabled)
                {
                    markerTicks[2] += System.Diagnostics.Stopwatch.GetElapsedTime(brainTick).TotalMilliseconds;
                    markerAlloc[2] += GC.GetAllocatedBytesForCurrentThread() - brainStart;
                }
                drive = Math.Clamp(actionBuffer[(int)ActionKindV4.Move], 0f, 1f);
                turn = Math.Clamp(actionBuffer[(int)ActionKindV4.Turn], -1f, 1f);
                spikeDrive = Math.Clamp(actionBuffer[(int)ActionKindV4.Spike], -1f, 1f);
                aimDrive = Math.Clamp(actionBuffer[(int)ActionKindV4.Aim], -1f, 1f);
                sprintDrive = Math.Clamp(actionBuffer[(int)ActionKindV4.Sprint], -1f, 1f);
                entity.SetData(DemoKeys.Drive, drive);
                entity.SetData(DemoKeys.Turn, turn);
                entity.SetData(DemoKeys.SpikeDrive, spikeDrive);
                entity.SetData(DemoKeys.Aim, aimDrive);
                entity.SetData(DemoKeys.SprintDrive, sprintDrive);
            }
            if (profiler.Enabled)
            {
                aiTicks += System.Diagnostics.Stopwatch.GetElapsedTime(aiStart).TotalMilliseconds;
                aiAlloc += GC.GetAllocatedBytesForCurrentThread() - aiAllocStart;
            }
            if (brainPhenotype is null)
            {
                var (cachedPhenotype, _, cachedHasSpikePort) = GetBrainPhenotype(creature.BodyGenome);
                brainPhenotype = cachedPhenotype;
                hasSpikePort = cachedHasSpikePort;
            }

            actionBuffer[(int)ActionKindV4.Move] = drive;
            actionBuffer[(int)ActionKindV4.Turn] = turn;
            actionBuffer[(int)ActionKindV4.Spike] = spikeDrive;
            actionBuffer[(int)ActionKindV4.Aim] = aimDrive;
            actionBuffer[(int)ActionKindV4.Sprint] = sprintDrive;
            if (sprintDrive > 0.4f)
                sprintActions++;
            ActionStateScratch.Position = new System.Numerics.Vector2(creature.X, creature.Y);
            ActionStateScratch.Heading = creature.Heading;
            ActionStateScratch.MoveSpeed = creature.MoveSpeed;
            ActionStateScratch.TurnSpeed = creature.TurnSpeed;
            ActionStateScratch.DeltaSeconds = tickDt;
            ActionStateScratch.MoveSpeedMultiplier = 1f;
            ActionStateScratch.AimDrive = aimDrive;
            ActionStateScratch.SpikeRange = creature.SpikeRadius;
            ActionStateScratch.SpikeBattery = creature.SpikeBattery;
            ActionStateScratch.SpikeCount = creature.SpikeCount;
            ActionStateScratch.SpikeSpread = creature.SpikeSpread;
            ActionStateScratch.SpikeJitter = creature.SpikeJitter;
            ActionStateScratch.Random = rng;
            ActionStateScratch.SpikePlan = null;
            var targetStart = profiler.Enabled ? GC.GetAllocatedBytesForCurrentThread() : 0L;
            var targetTick = profiler.Enabled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;
            var spikeTargets = SpikeTargetScratch;
            spikeTargets.Clear();

            // The plan itself rejects out-of-range or out-of-cone candidates, but
            // the grid query returns every creature in the covering cells. Skip
            // the walk while the spike is cooling down and pre-filter candidates
            // the beam can never reach.
            // Exact complement of the action effect's early-out, including
            // non-finite values: a NaN spikeDrive would still reach the effect
            // and consume its plan, so the query must not be skipped here.
            if (hasSpikePort
                && creature.SpikeCooldown <= tickDt
                && !(spikeDrive < Tuning.SpikeThreshold))
            {
                // A target can sit slightly beyond SpikeRange but still be hit
                // near the beam edge: query the outer hit radius so the
                // distance-filtered grid query cannot drop a valid target.
                var maxTargetRadius = Tuning.BodyRadiusBase * Tuning.BodySizeMax;
                var beamReach = Tuning.SpikeBeamRadius + maxTargetRadius;
                var spikeQueryRadius = MathF.Sqrt(
                    (creature.SpikeRadius * creature.SpikeRadius) + (beamReach * beamReach)) + maxTargetRadius;
                sensorGrid.Query(creature.X, creature.Y, spikeQueryRadius, sensorGrid.Creatures, nearCreatures);
                var aimReach = MathF.Sqrt(
                    (creature.SpikeRadius * creature.SpikeRadius)
                    + ((Tuning.SpikeBeamRadius + creature.BodyRadius) * (Tuning.SpikeBeamRadius + creature.BodyRadius)));
                foreach (var point in nearCreatures)
                {
                    if (point.Id == i)
                        continue;
                    var candidate = creatures[point.Id];
                    if (candidate.Entity.IsPendingKill)
                        continue;
                    var dx = candidate.X - creature.X;
                    var dy = candidate.Y - creature.Y;
                    var reach = aimReach + candidate.BodyRadius;
                    if ((dx * dx) + (dy * dy) > reach * reach)
                        continue;
                    spikeTargets.Add(new SpikeTargetV4(
                        point.Id,
                        new System.Numerics.Vector2(candidate.X, candidate.Y),
                        candidate.Energy,
                        candidate.BodyRadius,
                        candidate.Entity.IsPendingKill));
                }
            }

            if (profiler.Enabled)
            {
                markerTicks[3] += System.Diagnostics.Stopwatch.GetElapsedTime(targetTick).TotalMilliseconds;
                markerAlloc[3] += GC.GetAllocatedBytesForCurrentThread() - targetStart;
            }
            ActionStateScratch.SpikeTargets = spikeTargets;
            DemoActionEffectRegistry.Apply(brainPhenotype, actionBuffer, ActionStateScratch);
            var heading = ActionStateScratch.Heading;
            var position = new NumericsVector2(ActionStateScratch.Position.X, ActionStateScratch.Position.Y);

            var bodyLimit = Tuning.PondRadius - creature.BodyRadius;
            if (position.LengthSquared() > bodyLimit * bodyLimit)
                position = NumericsVector2.Normalize(position) * bodyLimit;

            // A creature earlier in this tick may already have been hit by a
            // spike and written its remaining energy back to the entity; the
            // snapshot energy would otherwise overwrite that damage at commit.
            var energy = MathF.Max(0f, ReadFloat(entity, DemoKeys.Energy, creature.Energy));
            var age = creature.Age + tickDt;
            var cooldown = Math.Max(0f, creature.ReproCooldown - tickDt);
            // Sprint multiplies distance by 1.6x while enabled; the surcharge
            // scales with the configured fraction of driven movement cost so the
            // per-distance price rises with the sprint multiplier instead of
            // making sprint a free speed increase.
            var sprintSurcharge = EnergyBudgetMath.SprintSurchargePerSecond(
                Tuning.MoveEnergyPerSecond,
                drive,
                sprintDrive > 0.4f,
                Tuning.SprintSurchargeFraction);

            // The compiled V4 budget (body tissue, neural tissue, genome
            // complexity) is charged as a per-second upkeep so a larger
            // genome/brain is no longer free.
            var genomeUpkeep = EnergyBudgetMath.UpkeepPerSecond(
                brainPhenotype.Energy.MaintenancePerSecond,
                Tuning.GenomeMaintenanceScale);
            var organUpkeep = EnergyBudgetMath.OrganUpkeepPerSecond(
                System.Numerics.BitOperations.PopCount((uint)creature.OrganMask),
                Tuning.OrganUpkeepPerSecond);
            energy -= (float)(((Tuning.BaseMetabolismPerSecond * creature.BasalMetabolism)
                + (Tuning.MoveEnergyPerSecond * drive)
                + (Tuning.TurnEnergyPerSecond * Math.Abs(turn))
                + sprintSurcharge
                + genomeUpkeep
                + organUpkeep) * tickDt);
            energy = MathF.Min(energy, creature.MaxEnergy);

            var ate = false;
            sensorGrid.Query(position.X, position.Y, sensorGrid.Food, nearFood);
            var pickupRadiusSquared = creature.FoodPickupRadius * creature.FoodPickupRadius;
            var bestFoodIndex = -1;
            var bestFoodEnergy = float.NegativeInfinity;
            var bestFoodDistanceSquared = float.PositiveInfinity;
            foreach (var point in nearFood)
            {
                var foodIndex = point.Id;
                if (foodIndex < 0 || foodIndex >= foods.Count)
                    continue;
                if (foodConsumed[foodIndex])
                    continue;
                var delta = new NumericsVector2(foods[foodIndex].X, foods[foodIndex].Y) - position;
                var distanceSquared = delta.LengthSquared();
                if (distanceSquared > pickupRadiusSquared)
                    continue;
                var candidateType = foods[foodIndex].Type;
                var candidateEnergy = FoodMath.EnergyFor(
                    candidateType,
                    Tuning.FoodEnergy,
                    Tuning.ToughFoodEnergy,
                    Tuning.ToxicFoodEnergy,
                    creature.HardDigest,
                    creature.ToxinResistance);
                if (!FoodMath.IsBetterChoice(
                        candidateEnergy,
                        bestFoodEnergy,
                        distanceSquared,
                        bestFoodDistanceSquared,
                        foodIndex,
                        bestFoodIndex))
                    continue;
                bestFoodIndex = foodIndex;
                bestFoodEnergy = candidateEnergy;
                bestFoodDistanceSquared = distanceSquared;
            }

            if (bestFoodIndex >= 0)
            {
                foodConsumed[bestFoodIndex] = true;
                foodAlive--;
                if (foods[bestFoodIndex].FromCorpse)
                {
                    corpseMeals++;
                    entity.SetData(
                        DemoKeys.CreatureCorpseMeals,
                        ReadInt(entity, DemoKeys.CreatureCorpseMeals, 0) + 1);
                }

                var foodType = foods[bestFoodIndex].Type;
                session.RequestKillEntity(foods[bestFoodIndex].Entity.Name);
                energy = MathF.Min(energy + bestFoodEnergy, creature.MaxEnergy);
                if (foodType == FoodType.Tough)
                {
                    toughMeals++;
                    entity.SetData(DemoKeys.CreatureToughMeals,
                        ReadInt(entity, DemoKeys.CreatureToughMeals, 0) + 1);
                }
                else if (foodType == FoodType.Toxic)
                {
                    toxicMeals++;
                    entity.SetData(DemoKeys.CreatureToxicMeals,
                        ReadInt(entity, DemoKeys.CreatureToxicMeals, 0) + 1);
                }
                else
                {
                    softMeals++;
                    entity.SetData(DemoKeys.CreatureSoftMeals,
                        ReadInt(entity, DemoKeys.CreatureSoftMeals, 0) + 1);
                }
                ate = true;
            }

            var spikeCooldown = Math.Max(0f, creature.SpikeCooldown - tickDt);
            var shotPlan = ActionStateScratch.SpikePlan;
            if (shotPlan is not null
                && energy >= shotPlan.Cost
                && spikeCooldown <= 0f)
            {
                energy -= shotPlan.Cost;
                spikeCooldown = Tuning.SpikeCooldownSeconds
                    + (Tuning.SpikeCooldownPerExtraPellet * Math.Max(0, shotPlan.PelletCount - 1));
                entity.SetData(DemoKeys.SpikeFlash, pondTime);
                entity.SetData(DemoKeys.SpikeEndX, position.X);
                entity.SetData(DemoKeys.SpikeEndY, position.Y);
                entity.SetData(DemoKeys.SpikeFan, shotPlan.Fan);
                spikeAttacks++;
                foreach (var targetIndex in shotPlan.TargetIds)
                {
                    if (targetIndex < 0)
                        continue;
                    var target = creatures[targetIndex];
                    if (target.Entity.IsPendingKill)
                        continue;
                    var targetEnergy = MathF.Max(0f, ReadFloat(target.Entity, DemoKeys.Energy, target.Energy));
                    var damage = SpikeDamageMath.Resolve(
                        targetEnergy,
                        target.MaxEnergy,
                        Tuning.SpikeDamageFraction,
                        target.Armor,
                        Tuning.SpikeArmorDamageMultiplier,
                        Tuning.SpikeKillStealFraction);
                    energy = MathF.Min(energy + damage.StolenEnergy, creature.MaxEnergy);
                    target.Entity.SetData(DemoKeys.Energy, damage.RemainingEnergy);
                    spikeHits++;
                    spikeDrained += damage.StolenEnergy;
                    if (!damage.Lethal)
                        continue;

                    session.RequestKillEntity(target.Entity.Name);
                    spikeKills++;
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

            if (!ate
                && energy >= Tuning.ReproductionEnergy
                && cooldown <= 0f
                && creatures.Count + pendingChildren + pendingDeferredCreatures < Tuning.PopulationCap)
            {
                if (bodyGenome.Length == 0)
                {
                    bodyGenome = creature.BodyGenome;
                }

                if (!CreatureGenome.TryDecode(bodyGenome, out var decodedParent))
                    continue;
                var parentGenome = decodedParent!;
                var generation = Math.Max(0, ReadInt(entity, DemoKeys.Generation, 0));
                var lineage = Math.Max(0, ReadInt(entity, DemoKeys.Lineage, 1));
                var children = Math.Max(0, ReadInt(entity, DemoKeys.Children, 0));
                var mutationProfile = MutationProfile.FromSettings(DemoSettings.Current)
                    .WithSteps(1 + rng.Next(3));
                var childGenome = CreatureGenome.Mutate(
                    parentGenome,
                    mutationProfile,
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
                controller.SetData(DemoKeys.Births, ReadInt(controller, DemoKeys.Births, 0) + 1);
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
                entity.SetData(DemoKeys.Sensors, SensorDisplayV4.Format(brainPhenotype!.SensorPorts, sensorBuffer));
                entity.SetData(DemoKeys.Outputs, string.Create(
                    CultureInfo.InvariantCulture,
                    $"M {drive:0.00} / T {turn:0.00} / S {spikeDrive:0.00} / A {aimDrive:0.00}"));
            }

            if (energy <= 0f || age >= creature.MaxAge)
            {
                var deathVariant = world.Session.Runtime.Resolver.Resolve("death.handler")
                    ?? throw new InvalidOperationException("No enabled death.handler variant.");
                var deathContext = new DeathHandlerContext
                {
                    Controller = controller,
                    Creature = creature,
                    Position = position,
                    Rng = rng,
                    Mutations = mutations,
                    Variant = deathVariant,
                    FoodAlive = foodAlive,
                    PendingFood = pendingFood,
                    NextFoodId = nextFoodId,
                    NextCorpseId = nextCorpseId,
                    CorpsesCreated = corpsesCreated,
                };
                DemoMechanicHandlers.Death.Require(deathVariant.HandlerId).Execute(deathContext);
                foodAlive = deathContext.FoodAlive;
                pendingFood = deathContext.PendingFood;
                nextFoodId = deathContext.NextFoodId;
                nextCorpseId = deathContext.NextCorpseId;
                corpsesCreated = deathContext.CorpsesCreated;

                session.RequestKillEntity(entity.Name);
                continue;
            }

            alive++;
            if (collisionEnabled)
            {
                collisionBodies.Add(new CollisionBody { X = position.X, Y = position.Y, Radius = creature.BodyRadius });
                collisionEntities.Add(entity);
            }
        }

        if (profiler.Enabled)
        {
            profiler.RecordMarker("evolution.core.ai", aiTicks, aiAlloc);
            profiler.RecordMarker("evolution.core.genome_decode", markerTicks[0], markerAlloc[0]);
            profiler.RecordMarker("evolution.core.sensors", markerTicks[1], markerAlloc[1]);
            profiler.RecordMarker("evolution.core.brain", markerTicks[2], markerAlloc[2]);
            profiler.RecordMarker("evolution.core.spike_targets", markerTicks[3], markerAlloc[3]);
        }
        if (pendingChildren > 0)
            controller.SetData(
                DemoKeys.PendingCreatureSpawns,
                pendingDeferredCreatures + pendingChildren);
        world.PondTime = pondTime;
        world.BloomEnabled = bloomEnabled;
        world.CollisionEnabled = collisionEnabled;
        world.PatchCount = patchCount;
        world.Seed = seed;
        world.FoodPreset = ReadInt(controller, DemoKeys.FoodPreset, 1);
        world.FoodAccumulator = ReadFloat(controller, DemoKeys.FoodAccumulator, 0f);
        world.FeedRequest = ReadInt(controller, DemoKeys.FeedRequest, 0);
        world.NextCreatureId = nextCreatureId;
        world.NextLineageId = nextLineageId;
        world.NextFoodId = nextFoodId;
        world.NextCorpseId = nextCorpseId;
        world.CorpseCreated = corpsesCreated;
        world.CorpseMeals = corpseMeals;
        world.CarrionDetections = carrionDetections;
        world.SprintActions = sprintActions;
        world.SoftMeals = softMeals;
        world.ToughMeals = toughMeals;
        world.ToxicMeals = toxicMeals;
        world.MaxGeneration = maxGeneration;
        world.PendingChildren = pendingChildren;
        world.SpikeAttacks = spikeAttacks;
        world.SpikeHits = spikeHits;
        world.SpikeKills = spikeKills;
        world.SpikeDrained = spikeDrained;
        world.FoodAlive = foodAlive;
        world.PendingFood = pendingFood;
        world.SensorGrid = sensorGrid;
        world.FoodCount = foods.Count;
        world.Alive = alive;
        world.SpikeKillsTotal = ReadInt(controller, DemoKeys.SpikeKills, 0) + spikeKills;
    }

    internal static void AddPendingFoodSources(EvolutionTickContext world)
    {
        var controller = world.Controller;
        var pending = ReadInt(controller, DemoKeys.PendingSourceSpawns, 0);
        if (pending <= 0)
            return;
        var patchCount = Math.Clamp(ReadInt(controller, DemoKeys.FoodPatchCount, Tuning.FoodPatchCount), 0, 64);
        var nextIndex = Math.Max(patchCount, ReadInt(controller, DemoKeys.NextSourceIndex, patchCount));
        var seed = ReadInt(controller, DemoKeys.Seed, 1);
        for (var i = 0; i < pending; i++)
        {
            var index = nextIndex++;
            var angle = (index + 1) * 2.3999632f;
            var radius = Tuning.PondRadius * (0.35f + (0.45f * ((index % 5) / 4f)));
            world.Mutations.Add(CreateFoodSourceMeta(
                SourceName(index), index, Math.Max(patchCount, index + 1), seed,
                MathF.Cos(angle) * radius, MathF.Sin(angle) * radius));
        }

        controller.SetData(DemoKeys.NextSourceIndex, nextIndex);
        controller.SetData(DemoKeys.PendingSourceSpawns, 0);
    }

    private static SndMetaData CreateFoodSourceMeta(
        string name,
        int patchIndex,
        int patchCount,
        int seed,
        float? x = null,
        float? y = null)
    {
        var builder = new SndMetaFluentBuilder(name)
            .SetNode("main", EvolutionEntityFactory.FoodScene)
            .SetString(DemoKeys.Kind, DemoKeys.FoodSourceKind)
            .SetInt(DemoKeys.PatchIndex, patchIndex)
            .SetInt(DemoKeys.PatchCount, Math.Max(patchCount, patchIndex + 1))
            .SetInt(DemoKeys.Seed, seed)
            .AddLifecycleStrategy("evolution.foodsource");
        if (x is { } px)
            builder.SetFloat(DemoKeys.X, px);
        if (y is { } py)
            builder.SetFloat(DemoKeys.Y, py);
        return builder.Build();
    }

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

    internal static NumericsVector2 ClampToPond(NumericsVector2 position, float margin)
    {
        var limit = Tuning.PondRadius - margin;
        if (position.LengthSquared() <= limit * limit)
            return position;
        return NumericsVector2.Normalize(position) * limit;
    }

    internal static NumericsVector2 RandomFoodPosition(Random rng, int seed, int patchCount)
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

    internal static NumericsVector2 RandomOffset(Random rng)
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

    internal static int FoodTargetForPreset(int preset, float time, bool bloomEnabled)
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

    /// <summary>
    ///     Returns the compiled phenotype/brain for one serialized genome,
    ///     decoding and compiling only on a cache miss. Genomes are immutable for
    ///     a creature's lifetime, so the cache key is the canonical hex string.
    /// </summary>
    /// <remarks>
    ///     A genome that cannot be decoded or compiled throws; the simulation
    ///     fails fast rather than substituting another creature.
    /// </remarks>
    private static (PhenotypeV4 Phenotype, CompiledBrainV4 Brain, bool HasSpikePort) GetBrainPhenotype(
        string bodyGenome)
    {
        if (BrainCache.TryGetValue(bodyGenome, out var cached))
            return cached;
        if (BrainCache.Count >= CacheEviction.DefaultLimit)
            CacheEviction.TrimOldestHalf(BrainCache);
        var genome = CreatureGenome.FromSerialized(bodyGenome);
        var phenotype = GenomeV4Compiler.Compile(genome.Genome, ExpressionContextV4.Neutral);
        var entry = (phenotype, GenomeV4Compiler.CompileBrain(phenotype), HasActionPort(phenotype, ActionKindV4.Spike));
        BrainCache[bodyGenome] = entry;
        return entry;
    }

    private static bool HasActionPort(PhenotypeV4 phenotype, ActionKindV4 action)
    {
        foreach (var port in phenotype.ActionPorts)
        {
            if (port == (ushort)action)
                return true;
        }

        return false;
    }

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


}
