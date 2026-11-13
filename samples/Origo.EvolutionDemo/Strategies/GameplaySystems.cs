using System;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
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
        runtime.Pipeline.Add(new CoreSimulationSystem());
        runtime.Pipeline.Add(new FoodSourceSimulationSystem());
        runtime.Pipeline.Add(new ProgressionSimulationSystem());
        _registered = true;
    }
}
