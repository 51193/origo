using System.Collections.Generic;
using System;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.EvolutionDemo.Logic;
using Origo.EvolutionDemo.Logic.Gameplay;

namespace Origo.EvolutionDemo;

/// <summary>Mutable state shared by systems for one fixed simulation tick.</summary>
internal sealed class EvolutionTickContext : IGameplayWorldContext
{
    public EvolutionTickContext(ISndEntity controller, ISndContext sndContext, double tickSeconds)
    {
        Controller = controller;
        SndContext = sndContext;
        TickSeconds = tickSeconds;
    }

    public ISndEntity Controller { get; }
    public ISndContext SndContext { get; }
    public double TickSeconds { get; }
    public double TimeSeconds => TickSeconds;
    public SensorGrid? SensorGrid { get; set; }
    public int FoodCount { get; set; }
    public int Alive { get; set; }
    public int SpikeKillsTotal { get; set; }
    public WorldSnapshot? Snapshot { get; set; }
    public List<SndMetaData> Mutations { get; } = [];
    public Random Rng { get; set; } = new();
    public int NextFoodId { get; set; }
    public int FoodAlive { get; set; }
    public int PendingFood { get; set; }
    public int CarrionFoodReleased { get; set; }
    public int NextCreatureId { get; set; }
    public int NextLineageId { get; set; }
    public int NextCorpseId { get; set; }
    public int CorpseCreated { get; set; }
    public int CorpseMeals { get; set; }
    public int CarrionDetections { get; set; }
    public int SprintActions { get; set; }
    public int SoftMeals { get; set; }
    public int ToughMeals { get; set; }
    public int ToxicMeals { get; set; }
    public int MaxGeneration { get; set; }
    public int SpikeAttacks { get; set; }
    public int SpikeHits { get; set; }
    public int SpikeKills { get; set; }
    public float SpikeDrained { get; set; }
    public int PendingChildren { get; set; }
    public float FoodAccumulator { get; set; }
    public int FeedRequest { get; set; }
}

/// <summary>Builds the immutable entity/sensor snapshot for one tick.</summary>
internal sealed class SnapshotSimulationSystem : IGameplaySystem
{
    public string Id => "evolution.snapshot";
    public GameplayPhase Phase => GameplayPhase.Sense;
    public int Order => 0;

    public void Tick(IGameplayWorldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context is not EvolutionTickContext world)
            throw new InvalidOperationException("SnapshotSimulationSystem requires EvolutionTickContext.");
        world.Snapshot = WorldSnapshot.Capture(world.Controller);
        world.FoodAlive = world.Snapshot.FoodAlive;
        world.PendingFood = 0;
        world.CarrionFoodReleased = 0;
        world.NextFoodId = ReadInt(world.Controller, DemoKeys.NextFoodId, 1);
        world.Mutations.Clear();
        world.Rng = new Random(unchecked(
            (ReadInt(world.Controller, DemoKeys.Seed, 1) * 397)
            ^ ReadInt(world.Controller, DemoKeys.Tick, 0)));
    }

    private static int ReadInt(ISndEntity entity, string key, int fallback) =>
        entity.TryGetData<int>(key) is { found: true } result ? result.value : fallback;
}

/// <summary>Releases corpse nutrients and expires consumed corpses before creature AI.</summary>
internal sealed class CorpseSimulationSystem : IGameplaySystem
{
    public string Id => "evolution.corpse";
    public GameplayPhase Phase => GameplayPhase.Sense;
    public int Order => 10;

    public void Tick(IGameplayWorldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context is not EvolutionTickContext world || world.Snapshot is null)
            throw new InvalidOperationException("CorpseSimulationSystem requires snapshot state.");
        var tickDt = Tuning.SimulationTickSeconds;
        foreach (var corpse in world.Snapshot.Corpses)
        {
            if (corpse.Entity.IsPendingKill)
                continue;
            var freeSlots = Math.Max(0, Tuning.FoodCap - (world.FoodAlive + world.PendingFood));
            var release = CarcassMath.Advance(
                corpse.Timer, corpse.Remaining, corpse.Interval, tickDt, freeSlots);
            for (var pulse = 0; pulse < release.Pulses; pulse++)
            {
                var foodPosition = WorldSimulationStrategy.ClampToPond(
                    new System.Numerics.Vector2(corpse.X, corpse.Y)
                    + WorldSimulationStrategy.RandomOffset(world.Rng),
                    Tuning.FoodPickupRadius);
                world.Mutations.Add(EvolutionEntityFactory.CorpseFood(
                    EvolutionEntityFactory.FoodName(world.NextFoodId++),
                    foodPosition.X,
                    foodPosition.Y));
                world.FoodAlive++;
                world.PendingFood++;
                world.CarrionFoodReleased++;
            }

            corpse.Entity.SetData(DemoKeys.CorpseFoodRemaining, release.Remaining);
            corpse.Entity.SetData(DemoKeys.CorpseTimer, release.Timer);
            if (release.Remaining <= 0)
                corpse.Entity.OwningSession.RequestKillEntity(corpse.Entity.Name);
        }
    }
}

/// <summary>Runs the core creature, food, corpse, and reproduction simulation.</summary>
internal sealed class CoreSimulationSystem : IGameplaySystem
{
    public string Id => "evolution.core";
    public GameplayPhase Phase => GameplayPhase.Act;
    public int Order => 0;

    public void Tick(IGameplayWorldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context is not EvolutionTickContext world)
            throw new InvalidOperationException("CoreSimulationSystem requires EvolutionTickContext.");
        WorldSimulationStrategy.RunCoreSimulation(world);
    }
}

/// <summary>Commits tick counters, ids, deferred spawns, and extinction state.</summary>
internal sealed class CommitSimulationSystem : IGameplaySystem
{
    public string Id => "evolution.commit";
    public GameplayPhase Phase => GameplayPhase.Commit;
    public int Order => 0;

    public void Tick(IGameplayWorldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context is not EvolutionTickContext world)
            throw new InvalidOperationException("CommitSimulationSystem requires EvolutionTickContext.");
        var controller = world.Controller;
        var tickDt = Tuning.SimulationTickSeconds;
        controller.SetData(DemoKeys.Time, ReadFloat(controller, DemoKeys.Time, 0f) + tickDt);
        controller.SetData(DemoKeys.Tick, ReadInt(controller, DemoKeys.Tick, 0) + 1);
        controller.SetData(DemoKeys.NextCreatureId, world.NextCreatureId);
        controller.SetData(DemoKeys.NextLineageId, world.NextLineageId);
        controller.SetData(DemoKeys.NextFoodId, world.NextFoodId);
        controller.SetData(DemoKeys.NextCorpseId, world.NextCorpseId);
        controller.SetData(DemoKeys.CorpsesCreated,
            ReadInt(controller, DemoKeys.CorpsesCreated, 0) + world.CorpseCreated);
        controller.SetData(DemoKeys.CarrionFoodReleased,
            ReadInt(controller, DemoKeys.CarrionFoodReleased, 0) + world.CarrionFoodReleased);
        controller.SetData(DemoKeys.CorpseMeals,
            ReadInt(controller, DemoKeys.CorpseMeals, 0) + world.CorpseMeals);
        controller.SetData(DemoKeys.CarrionDetections,
            ReadInt(controller, DemoKeys.CarrionDetections, 0) + world.CarrionDetections);
        controller.SetData(DemoKeys.SprintActions,
            ReadInt(controller, DemoKeys.SprintActions, 0) + world.SprintActions);
        controller.SetData(DemoKeys.FoodSoftMeals,
            ReadInt(controller, DemoKeys.FoodSoftMeals, 0) + world.SoftMeals);
        controller.SetData(DemoKeys.FoodToughMeals,
            ReadInt(controller, DemoKeys.FoodToughMeals, 0) + world.ToughMeals);
        controller.SetData(DemoKeys.FoodToxicMeals,
            ReadInt(controller, DemoKeys.FoodToxicMeals, 0) + world.ToxicMeals);
        controller.SetData(DemoKeys.MaxGeneration, world.MaxGeneration);
        controller.SetData(DemoKeys.FoodAccumulator, world.FoodAccumulator);
        controller.SetData(DemoKeys.FeedRequest, world.FeedRequest);
        controller.SetData(DemoKeys.SpikeAttacks,
            ReadInt(controller, DemoKeys.SpikeAttacks, 0) + world.SpikeAttacks);
        controller.SetData(DemoKeys.SpikeKills,
            ReadInt(controller, DemoKeys.SpikeKills, 0) + world.SpikeKills);
        controller.SetData(DemoKeys.SpikeHits,
            ReadInt(controller, DemoKeys.SpikeHits, 0) + world.SpikeHits);
        controller.SetData(DemoKeys.SpikeDrained,
            ReadFloat(controller, DemoKeys.SpikeDrained, 0f) + world.SpikeDrained);
        if (world.Mutations.Count > 0)
        {
            var spawn = world.Mutations.ToArray();
            world.SndContext.Deferred.EnqueueBusinessDeferred(
                () => world.Controller.OwningSession.SpawnMany(spawn));
        }

        if (world.Alive == 0
            && world.PendingChildren == 0
            && ReadBool(controller, DemoKeys.Initialized, false))
            controller.SetData(DemoKeys.Phase, DemoKeys.PhaseExtinct);
    }

    private static float ReadFloat(ISndEntity entity, string key, float fallback) =>
        entity.TryGetData<float>(key) is { found: true } result ? result.value : fallback;

    private static int ReadInt(ISndEntity entity, string key, int fallback) =>
        entity.TryGetData<int>(key) is { found: true } result ? result.value : fallback;

    private static bool ReadBool(ISndEntity entity, string key, bool fallback) =>
        entity.TryGetData<bool>(key) is { found: true } result ? result.value : fallback;
}

/// <summary>Runs the SND food-source lifecycle strategies in the environment phase.</summary>
internal sealed class FoodSourceSimulationSystem : IGameplaySystem
{
    public string Id => "evolution.foodsource";
    public GameplayPhase Phase => GameplayPhase.Environment;
    public int Order => 0;

    public void Tick(IGameplayWorldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context is not EvolutionTickContext world
            || world.SensorGrid is null)
            throw new InvalidOperationException("FoodSourceSimulationSystem requires core tick state.");
        FoodSourceStrategy.TickAll(world.Controller, world.SndContext, world.SensorGrid, world.FoodCount);
    }
}

/// <summary>Advances lifetime and declarative milestone rewards.</summary>
internal sealed class ProgressionSimulationSystem : IGameplaySystem
{
    public string Id => "evolution.progression";
    public GameplayPhase Phase => GameplayPhase.Progress;
    public int Order => 0;

    public void Tick(IGameplayWorldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context is not EvolutionTickContext world)
            throw new InvalidOperationException("ProgressionSimulationSystem requires EvolutionTickContext.");
        var runtime = GameplayRuntime.EnsureCurrent();
        runtime.Progression.LifetimeSeconds += Tuning.SimulationTickSeconds;
        runtime.EvaluateMilestones(
            runtime.Progression.LifetimeSeconds,
            world.Alive,
            world.SpikeKillsTotal);
    }
}

/// <summary>Registers the demo's core systems into the gameplay pipeline.</summary>
internal static class DemoGameplaySystems
{
    private static bool _registered;

    public static void Register(GameplayRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        if (_registered)
            return;
        runtime.Pipeline.Add(new SnapshotSimulationSystem());
        runtime.Pipeline.Add(new CorpseSimulationSystem());
        runtime.Pipeline.Add(new CoreSimulationSystem());
        runtime.Pipeline.Add(new CommitSimulationSystem());
        runtime.Pipeline.Add(new FoodSourceSimulationSystem());
        runtime.Pipeline.Add(new ProgressionSimulationSystem());
        _registered = true;
    }
}
