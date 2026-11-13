using System.Collections.Generic;
using Origo.EvolutionDemo.Logic.Gameplay;

namespace Origo.EvolutionDemo;

/// <summary>Registers the demo's core content and phase systems as one module.</summary>
internal sealed class CoreEvolutionModule : IGameplayModule
{
    public string Id => "evolution.core";
    public string Version => "1.0.0";
    public IReadOnlyList<string> Dependencies => [];

    public void Register(GameplayContentRegistry content, GameplayModuleServices services)
    {
        new DefaultGameplayContentModule().Register(content, services);
        services.AddSystem(new SnapshotSimulationSystem());
        services.AddSystem(new CorpseSimulationSystem());
        services.AddSystem(new CoreSimulationSystem());
        services.AddSystem(new CommitSimulationSystem());
        services.AddSystem(new FoodSourceSimulationSystem());
        services.AddSystem(new ProgressionSimulationSystem());
    }
}
