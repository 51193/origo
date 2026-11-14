using System;
using System.Collections.Generic;
using System.Linq;

namespace Origo.EvolutionDemo.Logic.Gameplay;

/// <summary>Minimal world context passed to pipeline systems.</summary>
public interface IGameplayWorldContext
{
    double TimeSeconds { get; }
}

/// <summary>Phases of the fixed gameplay tick pipeline.</summary>
public enum GameplayPhase
{
    Sense,
    Decide,
    Act,
    Move,
    Eat,
    ResolveAction,
    Reproduce,
    Death,
    Commit,
    Environment,
    Progress,
}

/// <summary>One pluggable gameplay system.</summary>
public interface IGameplaySystem
{
    string Id { get; }
    GameplayPhase Phase { get; }
    int Order { get; }
    void Tick(IGameplayWorldContext context);
}

/// <summary>Startup module contract.</summary>
public interface IGameplayModule
{
    string Id { get; }
    string Version { get; }
    IReadOnlyList<string> Dependencies { get; }
    void Register(GameplayContentRegistry content, GameplayModuleServices services);
}

/// <summary>Services a module can register into without adding SND strategies.</summary>
public sealed class GameplayModuleServices
{
    private readonly List<IGameplaySystem> _systems = [];

    public IReadOnlyList<IGameplaySystem> Systems => _systems;
    public void AddSystem(IGameplaySystem system)
    {
        ArgumentNullException.ThrowIfNull(system);
        _systems.Add(system);
    }
}

/// <summary>Loads modules at startup, validates dependencies, and builds the content snapshot.</summary>
public sealed class GameplayModuleHost
{
    private readonly List<IGameplayModule> _modules = [];

    public IReadOnlyList<IGameplayModule> Modules => _modules;

    public void AddModule(IGameplayModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (_modules.Any(existing => string.Equals(existing.Id, module.Id, StringComparison.Ordinal)))
            throw new InvalidOperationException($"Duplicate module '{module.Id}'.");
        _modules.Add(module);
    }

    public (GameplayContentSnapshot Content, GameplayModuleServices Services) Load()
    {
        var content = new GameplayContentRegistry();
        var services = new GameplayModuleServices();
        var loaded = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<IGameplayModule>();
        while (ordered.Count < _modules.Count)
        {
            var progressed = false;
            foreach (var module in _modules)
            {
                if (loaded.Contains(module.Id))
                    continue;
                if (module.Dependencies.Any(dependency => !loaded.Contains(dependency)))
                    continue;
                module.Register(content, services);
                loaded.Add(module.Id);
                ordered.Add(module);
                progressed = true;
            }

            if (!progressed)
                throw new InvalidOperationException("Cyclic or missing module dependencies.");
        }

        return (content.BuildSnapshot(), services);
    }
}

/// <summary>Deterministic fixed-phase system pipeline.</summary>
public sealed class GameplayPipeline
{
    private readonly List<IGameplaySystem> _systems = [];

    public IReadOnlyList<IGameplaySystem> Systems => _systems;

    public void Add(IGameplaySystem system)
    {
        ArgumentNullException.ThrowIfNull(system);
        if (_systems.Any(existing => string.Equals(existing.Id, system.Id, StringComparison.Ordinal)))
            throw new InvalidOperationException($"Duplicate gameplay system '{system.Id}'.");
        _systems.Add(system);
        _systems.Sort(static (a, b) =>
        {
            var phase = a.Phase.CompareTo(b.Phase);
            return phase != 0 ? phase : a.Order.CompareTo(b.Order);
        });
    }

    public GameplayPipeline FromServices(GameplayModuleServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        foreach (var system in services.Systems)
            Add(system);
        return this;
    }

    public void Tick(
        IGameplayWorldContext context,
        Func<GameplayPhase, bool> isPhaseEnabled,
        Action<IGameplaySystem, double, long>? systemObserver = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(isPhaseEnabled);
        foreach (var system in _systems)
        {
            if (!isPhaseEnabled(system.Phase))
                continue;
            if (systemObserver is null)
            {
                system.Tick(context);
                continue;
            }

            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            system.Tick(context);
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            systemObserver(system, elapsed, allocated);
        }
    }
}
