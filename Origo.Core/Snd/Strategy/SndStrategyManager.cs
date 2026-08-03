using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Logging;

namespace Origo.Core.Snd.Strategy;

/// <summary>
///     Per-entity passive strategy manager. Stores strategies acquired from
///     the <see cref="SndStrategyPool" /> in priority-sorted order and
///     dispatches lifecycle hooks (AfterSpawn, AfterLoad, BeforeSave,
///     BeforeQuit, BeforeDead) via snapshot iteration.
///     <para>
///         Recovery is all-or-nothing: if any strategy fails validation
///         during <c>RecoverStrategiesOnly</c>, all previously recovered
///         strategies are released before the exception propagates.
///     </para>
/// </summary>
internal sealed class SndStrategyManager
{
    private const string _logTag = nameof(SndStrategyManager);
    private readonly ILogger _logger;
    private readonly SndStrategyPool _pool;
    private readonly List<StrategyEntry> _processBuffer = [];
    private readonly List<StrategyEntry> _strategies = [];

    public SndStrategyManager(SndStrategyPool pool, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(pool);
        _pool = pool;
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    internal void RecoverStrategiesOnly(IEnumerable<string> indices)
    {
        ReleaseStrategiesOnly();
        try
        {
            foreach (var index in indices)
            {
                var strategy = _pool.GetStrategy<BaseStrategy>(index);
                if (strategy is LifecycleStrategyBase lifecycleStrategy)
                    InsertSorted(new StrategyEntry
                    { Index = index, Strategy = lifecycleStrategy });
                else
                {
                    _pool.ReleaseStrategy(index);
                    throw new InvalidOperationException(
                        $"Strategy '{index}' (type '{strategy.GetType().FullName}') is not a LifecycleStrategyBase " +
                        "and cannot be recovered as an entity strategy. " +
                        "Entity strategies must inherit from LifecycleStrategyBase.");
                }
            }
        }
        catch
        {
            ReleaseStrategiesOnly();
            throw;
        }

        _logger.Log(LogLevel.Info, _logTag,
            new LogMessageBuilder().Build($"Strategies recovered: {_strategies.Count}."));
    }

    internal void ReleaseStrategiesOnly()
    {
        foreach (var entry in _strategies) _pool.ReleaseStrategy(entry.Index);

        _strategies.Clear();
    }

    internal void TriggerAfterSpawn(ISndEntity entity, ISndContext ctx)
        => TriggerAll((s, e, c) => s.AfterSpawn(e, c), entity, ctx);

    internal void TriggerAfterLoad(ISndEntity entity, ISndContext ctx)
        => TriggerAll((s, e, c) => s.AfterLoad(e, c), entity, ctx);

    internal void TriggerBeforeSave(ISndEntity entity, ISndContext ctx)
        => TriggerAll((s, e, c) => s.BeforeSave(e, c), entity, ctx);

    internal void TriggerBeforeQuit(ISndEntity entity, ISndContext ctx)
        => TriggerAll((s, e, c) => s.BeforeQuit(e, c), entity, ctx);

    internal void TriggerBeforeDead(ISndEntity entity, ISndContext ctx)
        => TriggerAll((s, e, c) => s.BeforeDead(e, c), entity, ctx);

    private void TriggerAll(
        Action<LifecycleStrategyBase, ISndEntity, ISndContext> hook,
        ISndEntity entity,
        ISndContext ctx)
    {
        foreach (var s in _strategies.ToArray()) hook(s.Strategy, entity, ctx);
    }

    internal IReadOnlyCollection<string> GetStrategyIndices() => [.. _strategies.Select(s => s.Index)];

    internal bool HasMounted(string index) => _strategies.Any(s => s.Index == index);

    public void Add(ISndEntity entity, string index, ISndContext ctx)
    {
        if (_strategies.Any(s => s.Index == index))
            throw new InvalidOperationException(
                $"Strategy '{index}' is already mounted on entity '{entity.Name}'. " +
                "Remove the existing strategy before adding it again.");

        var strategy = _pool.GetStrategy<LifecycleStrategyBase>(index);
        var entry = new StrategyEntry { Index = index, Strategy = strategy };

        InsertSorted(entry);
        try
        {
            strategy.AfterAdd(entity, ctx);
        }
        catch
        {
            _strategies.Remove(entry);
            _pool.ReleaseStrategy(index);
            throw;
        }

        _logger.Log(LogLevel.Debug, _logTag, new LogMessageBuilder()
            .AddContext("entityName", entity.Name)
            .AddContext("strategyIndex", index)
            .Build("Strategy added."));
    }

    public void Remove(ISndEntity entity, string index, ISndContext ctx)
    {
        var i = _strategies.FindLastIndex(s => s.Index == index);
        if (i < 0) return;

        var entry = _strategies[i];
        entry.Strategy.BeforeRemove(entity, ctx);
        _strategies.RemoveAt(i);
        _pool.ReleaseStrategy(index);
        _logger.Log(LogLevel.Debug, _logTag, new LogMessageBuilder()
            .AddContext("entityName", entity.Name)
            .AddContext("strategyIndex", index)
            .Build("Strategy removed."));
    }

    public void Process(ISndEntity entity, double delta, ISndContext ctx)
    {
        _processBuffer.Clear();
        _processBuffer.AddRange(_strategies);
        foreach (var entry in _processBuffer)
            entry.Strategy.Process(entity, delta, ctx);
    }

    private void InsertSorted(StrategyEntry entry)
    {
        var priority = _pool.GetPriority(entry.Index);
        var insertIndex = _strategies.Count;
        for (var i = 0; i < _strategies.Count; i++)
            if (_pool.GetPriority(_strategies[i].Index) > priority)
            {
                insertIndex = i;
                break;
            }

        _strategies.Insert(insertIndex, entry);
    }

    private sealed class StrategyEntry
    {
        public required string Index { get; init; }
        public required LifecycleStrategyBase Strategy { get; init; }
    }
}
