using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Logging;

namespace Origo.Core.Snd.Strategy;

internal sealed class SndStrategyManager
{
    private const string LogTag = nameof(SndStrategyManager);
    private readonly ILogger _logger;
    private readonly SndStrategyPool _pool;
    private readonly List<StrategyEntry> _processBuffer = new();
    private readonly List<StrategyEntry> _strategies = new();

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
                if (strategy is EntityStrategyBase entityStrategy)
                    InsertSorted(new StrategyEntry
                        { Index = index, Strategy = entityStrategy });
                else
                    _pool.ReleaseStrategy(index);
            }
        }
        catch
        {
            ReleaseStrategiesOnly();
            throw;
        }

        _logger.Log(LogLevel.Info, LogTag,
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
        Action<EntityStrategyBase, ISndEntity, ISndContext> hook,
        ISndEntity entity,
        ISndContext ctx)
    {
        foreach (var s in _strategies.ToArray()) hook(s.Strategy, entity, ctx);
    }

    internal IReadOnlyCollection<string> GetStrategyIndices() => _strategies.Select(s => s.Index).ToArray();

    public void Add(ISndEntity entity, string index, ISndContext ctx)
    {
        var strategy = _pool.GetStrategy<EntityStrategyBase>(index);
        var entry = new StrategyEntry { Index = index, Strategy = strategy };

        InsertSorted(entry);
        strategy.AfterAdd(entity, ctx);
        _logger.Log(LogLevel.Debug, LogTag, new LogMessageBuilder()
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
        _logger.Log(LogLevel.Debug, LogTag, new LogMessageBuilder()
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
        public required EntityStrategyBase Strategy { get; init; }
    }
}
