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

public abstract class BaseStrategyTestHarness
{
    private protected readonly StrategyTestContext Context;

    private protected BaseStrategyTestHarness(ISndEntity entity, StrategyTestContext context)
    {
        Entity = entity;
        Context = context;
    }

    public ISndEntity Entity { get; }

    public IBlackboard SystemBlackboard => Context.SystemBlackboard;

    public IBlackboard ProgressBlackboard => Context.ProgressBlackboard;

    public IBlackboard SessionBlackboard => Context.CurrentSession!.SessionBlackboard;

    public IReadOnlyList<string> SaveRequests => Context.SaveRequests;

    public IReadOnlyList<string> LoadRequests => Context.LoadRequests;

    public IReadOnlyList<string> LevelSwitchRequests => Context.LevelSwitchRequests;

    public int DeferredActionCount => Context.DeferredActionCount;

    public IReadOnlyList<string> ConsoleCommands => Context.ConsoleCommands;

    public TValue GetEntityData<TValue>(string key) => Entity.GetData<TValue>(key);

    public (bool found, TValue? value) TryGetEntityData<TValue>(string key) => Entity.TryGetData<TValue>(key);
}

public sealed class StrategyTestHarness : BaseStrategyTestHarness
{
    internal StrategyTestHarness(EntityStrategyBase strategy, MinimalTestEntity entity, StrategyTestContext context)
        : base(entity, context)
    {
        Strategy = strategy;
    }

    internal EntityStrategyBase Strategy { get; }

    public void RunFrame(double delta = 0.016)
    {
        Strategy.Process(Entity, delta, Context);
        Context.FlushDeferredActionsForCurrentFrame();
    }

    public void RunFrames(int count, double delta = 0.016)
    {
        for (var i = 0; i < count; i++)
            RunFrame(delta);
    }

    public void TriggerAfterSpawn() => Strategy.AfterSpawn(Entity, Context);

    public void TriggerAfterLoad() => Strategy.AfterLoad(Entity, Context);

    public void TriggerAfterAdd() => Strategy.AfterAdd(Entity, Context);

    public void TriggerBeforeRemove() => Strategy.BeforeRemove(Entity, Context);

    public void TriggerBeforeSave() => Strategy.BeforeSave(Entity, Context);

    public void TriggerBeforeQuit() => Strategy.BeforeQuit(Entity, Context);

    public void TriggerBeforeDead() => Strategy.BeforeDead(Entity, Context);
}

public sealed class ActiveStrategyTestHarness : BaseStrategyTestHarness
{
    private readonly string _strategyIndex;

    internal ActiveStrategyTestHarness(
        ActiveStrategyBase strategy,
        MinimalTestEntity entity,
        StrategyTestContext context,
        string strategyIndex)
        : base(entity, context)
    {
        Strategy = strategy;
        _strategyIndex = strategyIndex;
    }

    internal ActiveStrategyBase Strategy { get; }

    public object? Invoke(object? input = null) => Strategy.Invoke(Entity, Context, input);

    public void FlushDeferredActions() => Context.FlushDeferredActionsForCurrentFrame();

    public object? InvokeViaEntity(object? input = null) => Entity.InvokeStrategy(_strategyIndex, input);
}

public abstract class BaseStrategyTestScenarioBuilder
{
    private protected readonly List<Action<ISndEntity>> EntitySetup = new();
    private protected readonly List<Action<IBlackboard>> ProgressSetup = new();
    private protected readonly List<Action<IBlackboard>> SessionSetup = new();
    private protected readonly string StrategyIndex;
    private protected readonly List<Action<IBlackboard>> SystemSetup = new();
    private protected readonly Dictionary<string, SndMetaData> Templates = new(StringComparer.Ordinal);
    private protected string EntityName = "__test_entity__";

    private protected BaseStrategyTestScenarioBuilder(string strategyIndex)
    {
        StrategyIndex = strategyIndex;
    }

    private protected void ApplySetup(StrategyTestContext context)
    {
        foreach (var setup in SystemSetup)
            setup(context.SystemBlackboard);
        foreach (var setup in ProgressSetup)
            setup(context.ProgressBlackboard);
        foreach (var setup in SessionSetup)
        {
            var sessionBb = context.CurrentSession?.SessionBlackboard
                            ?? throw new InvalidOperationException("Session blackboard is not available.");
            setup(sessionBb);
        }

        foreach (var (key, template) in Templates)
            context.RegisterTemplate(key, template);
    }

    private protected MinimalTestEntity CreateEntity()
    {
        var entity = new MinimalTestEntity { Name = EntityName };
        foreach (var setup in EntitySetup)
            setup(entity);
        return entity;
    }
}

public sealed class StrategyTestScenarioBuilder<T> : BaseStrategyTestScenarioBuilder where T : EntityStrategyBase, new()
{
    internal StrategyTestScenarioBuilder(string strategyIndex) : base(strategyIndex)
    {
    }

    public StrategyTestScenarioBuilder<T> WithEntityName(string name)
    {
        EntityName = string.IsNullOrWhiteSpace(name) ? "__test_entity__" : name;
        return this;
    }

    public StrategyTestScenarioBuilder<T> WithData<TValue>(string key, TValue value)
    {
        EntitySetup.Add(e => e.SetData(key, value));
        return this;
    }

    public StrategyTestScenarioBuilder<T> WithSystemConfig<TValue>(string key, TValue value)
    {
        SystemSetup.Add(bb => bb.Set(key, value));
        return this;
    }

    public StrategyTestScenarioBuilder<T> WithProgressConfig<TValue>(string key, TValue value)
    {
        ProgressSetup.Add(bb => bb.Set(key, value));
        return this;
    }

    public StrategyTestScenarioBuilder<T> WithSessionConfig<TValue>(string key, TValue value)
    {
        SessionSetup.Add(bb => bb.Set(key, value));
        return this;
    }

    public StrategyTestScenarioBuilder<T> WithTemplate(string key, SndMetaData template)
    {
        ArgumentNullException.ThrowIfNull(template);
        Templates[key] = template;
        return this;
    }

    public StrategyTestHarness Build()
    {
        var context = new StrategyTestContext();
        ApplySetup(context);

        var entity = CreateEntity();

        var strategy = new T();
        strategy.AfterSpawn(entity, context);

        return new StrategyTestHarness(strategy, entity, context);
    }
}

public sealed class ActiveStrategyTestScenarioBuilder<T> : BaseStrategyTestScenarioBuilder where T : ActiveStrategyBase, new()
{
    internal ActiveStrategyTestScenarioBuilder(string strategyIndex) : base(strategyIndex)
    {
    }

    public ActiveStrategyTestScenarioBuilder<T> WithEntityName(string name)
    {
        EntityName = string.IsNullOrWhiteSpace(name) ? "__test_entity__" : name;
        return this;
    }

    public ActiveStrategyTestScenarioBuilder<T> WithData<TValue>(string key, TValue value)
    {
        EntitySetup.Add(e => e.SetData(key, value));
        return this;
    }

    public ActiveStrategyTestScenarioBuilder<T> WithSystemConfig<TValue>(string key, TValue value)
    {
        SystemSetup.Add(bb => bb.Set(key, value));
        return this;
    }

    public ActiveStrategyTestScenarioBuilder<T> WithProgressConfig<TValue>(string key, TValue value)
    {
        ProgressSetup.Add(bb => bb.Set(key, value));
        return this;
    }

    public ActiveStrategyTestScenarioBuilder<T> WithSessionConfig<TValue>(string key, TValue value)
    {
        SessionSetup.Add(bb => bb.Set(key, value));
        return this;
    }

    public ActiveStrategyTestScenarioBuilder<T> WithTemplate(string key, SndMetaData template)
    {
        ArgumentNullException.ThrowIfNull(template);
        Templates[key] = template;
        return this;
    }

    public ActiveStrategyTestHarness Build()
    {
        var context = new StrategyTestContext();
        ApplySetup(context);

        var entity = CreateEntity();

        var strategy = new T();
        entity.InvokeStrategyHandler = (index, input) => strategy.Invoke(entity, context, input);

        return new ActiveStrategyTestHarness(strategy, entity, context, StrategyIndex);
    }
}
