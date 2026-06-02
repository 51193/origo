using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;

namespace Origo.Core.Testing;

public static class StrategyTestScenario
{
    public static StrategyTestScenarioBuilder<T> For<T>(string strategyIndex)
        where T : EntityStrategyBase, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyIndex);
        return new StrategyTestScenarioBuilder<T>(strategyIndex);
    }

    public static ActiveStrategyTestScenarioBuilder<T> ForActive<T>(string strategyIndex)
        where T : ActiveStrategyBase, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyIndex);
        return new ActiveStrategyTestScenarioBuilder<T>(strategyIndex);
    }
}

public sealed class StrategyTestScenarioBuilder<T> where T : EntityStrategyBase, new()
{
    private readonly List<Action<ISndEntity>> _entitySetup = new();
    private readonly List<Action<IBlackboard>> _progressSetup = new();
    private readonly List<Action<IBlackboard>> _sessionSetup = new();
    private readonly string _strategyIndex;
    private readonly List<Action<IBlackboard>> _systemSetup = new();
    private readonly Dictionary<string, SndMetaData> _templates = new(StringComparer.Ordinal);
    private string _entityName = "__test_entity__";

    internal StrategyTestScenarioBuilder(string strategyIndex)
    {
        _strategyIndex = strategyIndex;
    }

    public StrategyTestScenarioBuilder<T> WithEntityName(string name)
    {
        _entityName = string.IsNullOrWhiteSpace(name) ? "__test_entity__" : name;
        return this;
    }

    public StrategyTestScenarioBuilder<T> WithData<TValue>(string key, TValue value)
    {
        _entitySetup.Add(e => e.SetData(key, value));
        return this;
    }

    public StrategyTestScenarioBuilder<T> WithSystemConfig<TValue>(string key, TValue value)
    {
        _systemSetup.Add(bb => bb.Set(key, value));
        return this;
    }

    public StrategyTestScenarioBuilder<T> WithProgressConfig<TValue>(string key, TValue value)
    {
        _progressSetup.Add(bb => bb.Set(key, value));
        return this;
    }

    public StrategyTestScenarioBuilder<T> WithSessionConfig<TValue>(string key, TValue value)
    {
        _sessionSetup.Add(bb => bb.Set(key, value));
        return this;
    }

    public StrategyTestScenarioBuilder<T> WithTemplate(string key, SndMetaData template)
    {
        ArgumentNullException.ThrowIfNull(template);
        _templates[key] = template;
        return this;
    }

    public StrategyTestHarness Build()
    {
        var context = new StrategyTestContext();

        foreach (var setup in _systemSetup)
            setup(context.SystemBlackboard);
        foreach (var setup in _progressSetup)
            setup(context.ProgressBlackboard);
        foreach (var setup in _sessionSetup)
        {
            var sessionBb = context.CurrentSession?.SessionBlackboard
                            ?? throw new InvalidOperationException("Session blackboard is not available.");
            setup(sessionBb);
        }

        foreach (var (key, template) in _templates)
            context.RegisterTemplate(key, template);

        var entity = new MinimalTestEntity { Name = _entityName };
        foreach (var setup in _entitySetup)
            setup(entity);

        var strategy = new T();
        strategy.AfterSpawn(entity, context);

        return new StrategyTestHarness(strategy, entity, context);
    }
}

public sealed class StrategyTestHarness
{
    private readonly StrategyTestContext _context;

    internal StrategyTestHarness(EntityStrategyBase strategy, MinimalTestEntity entity, StrategyTestContext context)
    {
        Strategy = strategy;
        Entity = entity;
        _context = context;
    }

    public ISndEntity Entity { get; }

    public IBlackboard SystemBlackboard => _context.SystemBlackboard;

    public IBlackboard ProgressBlackboard => _context.ProgressBlackboard;

    public IBlackboard SessionBlackboard => _context.CurrentSession!.SessionBlackboard;

    public IReadOnlyList<string> SaveRequests => _context.SaveRequests;

    public IReadOnlyList<string> LoadRequests => _context.LoadRequests;

    public IReadOnlyList<string> LevelSwitchRequests => _context.LevelSwitchRequests;

    public int DeferredActionCount => _context.DeferredActionCount;

    public IReadOnlyList<string> ConsoleCommands => _context.ConsoleCommands;

    internal EntityStrategyBase Strategy { get; }

    public void RunFrame(double delta = 0.016)
    {
        Strategy.Process(Entity, delta, _context);
        _context.FlushDeferredActionsForCurrentFrame();
    }

    public void RunFrames(int count, double delta = 0.016)
    {
        for (var i = 0; i < count; i++)
            RunFrame(delta);
    }

    public void TriggerAfterSpawn() => Strategy.AfterSpawn(Entity, _context);

    public void TriggerAfterLoad() => Strategy.AfterLoad(Entity, _context);

    public void TriggerAfterAdd() => Strategy.AfterAdd(Entity, _context);

    public void TriggerBeforeRemove() => Strategy.BeforeRemove(Entity, _context);

    public void TriggerBeforeSave() => Strategy.BeforeSave(Entity, _context);

    public void TriggerBeforeQuit() => Strategy.BeforeQuit(Entity, _context);

    public void TriggerBeforeDead() => Strategy.BeforeDead(Entity, _context);

    public TValue GetEntityData<TValue>(string key) => Entity.GetData<TValue>(key);

    public (bool found, TValue? value) TryGetEntityData<TValue>(string key) => Entity.TryGetData<TValue>(key);
}

public sealed class ActiveStrategyTestScenarioBuilder<T> where T : ActiveStrategyBase, new()
{
    private readonly List<Action<ISndEntity>> _entitySetup = new();
    private readonly List<Action<IBlackboard>> _progressSetup = new();
    private readonly List<Action<IBlackboard>> _sessionSetup = new();
    private readonly string _strategyIndex;
    private readonly List<Action<IBlackboard>> _systemSetup = new();
    private readonly Dictionary<string, SndMetaData> _templates = new(StringComparer.Ordinal);
    private string _entityName = "__test_entity__";

    internal ActiveStrategyTestScenarioBuilder(string strategyIndex)
    {
        _strategyIndex = strategyIndex;
    }

    public ActiveStrategyTestScenarioBuilder<T> WithEntityName(string name)
    {
        _entityName = string.IsNullOrWhiteSpace(name) ? "__test_entity__" : name;
        return this;
    }

    public ActiveStrategyTestScenarioBuilder<T> WithData<TValue>(string key, TValue value)
    {
        _entitySetup.Add(e => e.SetData(key, value));
        return this;
    }

    public ActiveStrategyTestScenarioBuilder<T> WithSystemConfig<TValue>(string key, TValue value)
    {
        _systemSetup.Add(bb => bb.Set(key, value));
        return this;
    }

    public ActiveStrategyTestScenarioBuilder<T> WithProgressConfig<TValue>(string key, TValue value)
    {
        _progressSetup.Add(bb => bb.Set(key, value));
        return this;
    }

    public ActiveStrategyTestScenarioBuilder<T> WithSessionConfig<TValue>(string key, TValue value)
    {
        _sessionSetup.Add(bb => bb.Set(key, value));
        return this;
    }

    public ActiveStrategyTestScenarioBuilder<T> WithTemplate(string key, SndMetaData template)
    {
        ArgumentNullException.ThrowIfNull(template);
        _templates[key] = template;
        return this;
    }

    public ActiveStrategyTestHarness Build()
    {
        var context = new StrategyTestContext();

        foreach (var setup in _systemSetup)
            setup(context.SystemBlackboard);
        foreach (var setup in _progressSetup)
            setup(context.ProgressBlackboard);
        foreach (var setup in _sessionSetup)
        {
            var sessionBb = context.CurrentSession?.SessionBlackboard
                            ?? throw new InvalidOperationException("Session blackboard is not available.");
            setup(sessionBb);
        }

        foreach (var (key, template) in _templates)
            context.RegisterTemplate(key, template);

        var entity = new MinimalTestEntity { Name = _entityName };
        foreach (var setup in _entitySetup)
            setup(entity);

        var strategy = new T();

        entity.InvokeStrategyHandler = (index, input) => strategy.Invoke(entity, context, input);

        return new ActiveStrategyTestHarness(strategy, entity, context, _strategyIndex);
    }
}

public sealed class ActiveStrategyTestHarness
{
    private readonly StrategyTestContext _context;
    private readonly string _strategyIndex;

    internal ActiveStrategyTestHarness(
        ActiveStrategyBase strategy,
        MinimalTestEntity entity,
        StrategyTestContext context,
        string strategyIndex)
    {
        Strategy = strategy;
        Entity = entity;
        _context = context;
        _strategyIndex = strategyIndex;
    }

    public ISndEntity Entity { get; }

    public IBlackboard SystemBlackboard => _context.SystemBlackboard;

    public IBlackboard ProgressBlackboard => _context.ProgressBlackboard;

    public IBlackboard SessionBlackboard => _context.CurrentSession!.SessionBlackboard;

    public IReadOnlyList<string> SaveRequests => _context.SaveRequests;

    public IReadOnlyList<string> LoadRequests => _context.LoadRequests;

    public IReadOnlyList<string> LevelSwitchRequests => _context.LevelSwitchRequests;

    public int DeferredActionCount => _context.DeferredActionCount;

    public IReadOnlyList<string> ConsoleCommands => _context.ConsoleCommands;

    internal ActiveStrategyBase Strategy { get; }

    public object? Invoke(object? input = null) => Strategy.Invoke(Entity, _context, input);

    public void FlushDeferredActions() => _context.FlushDeferredActionsForCurrentFrame();

    public object? InvokeViaEntity(object? input = null) => Entity.InvokeStrategy(_strategyIndex, input);

    public TValue GetEntityData<TValue>(string key) => Entity.GetData<TValue>(key);

    public (bool found, TValue? value) TryGetEntityData<TValue>(string key) => Entity.TryGetData<TValue>(key);
}
