using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Logging;

namespace Origo.Core.Snd.Strategy;

/// <summary>
///     管理单个实体上的策略集合以及其生命周期回调。
///     仅作为 Core 内部实现细节，对程序集外不可见。
/// </summary>
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

    public void Load(IEnumerable<string> indices, ISndEntity entity, ISndContext ctx)
    {
        Recover(indices);
        TriggerAfterLoad(entity, ctx);
    }

    public void Spawn(IEnumerable<string> indices, ISndEntity entity, ISndContext ctx)
    {
        Recover(indices);
        TriggerAfterSpawn(entity, ctx);
    }

    public void Quit(ISndEntity entity, ISndContext ctx)
    {
        TriggerBeforeQuit(entity, ctx);
        Release();
    }

    public void Dead(ISndEntity entity, ISndContext ctx)
    {
        TriggerBeforeDead(entity, ctx);
        Release();
    }

    public void Add(ISndEntity entity, string index, ISndContext ctx)
    {
        var strategy = _pool.GetStrategy<EntityStrategyBase>(index);
        var entry = new StrategyEntry { Index = index, Strategy = strategy };

        InsertSorted(entry);
        strategy.AfterAdd(entity, ctx);
        _logger.Log(LogLevel.Info, LogTag, new LogMessageBuilder()
            .AddSuffix("entityName", entity.Name)
            .AddSuffix("strategyIndex", index)
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
        _logger.Log(LogLevel.Info, LogTag, new LogMessageBuilder()
            .AddSuffix("entityName", entity.Name)
            .AddSuffix("strategyIndex", index)
            .Build("Strategy removed."));
    }

    public IReadOnlyCollection<string> SerializeIndices(ISndEntity entity, ISndContext ctx)
    {
        TriggerBeforeSave(entity, ctx);
        return _strategies.Select(s => s.Index).ToArray();
    }

    public void Process(ISndEntity entity, double delta, ISndContext ctx)
    {
        // 策略按优先级升序排列（同优先级按插入顺序）。Process 中可通过实体接口增删策略，因此基于快照迭代。
        _processBuffer.Clear();
        _processBuffer.AddRange(_strategies);
        foreach (var entry in _processBuffer)
            entry.Strategy.Process(entity, delta, ctx);
    }

    private void Recover(IEnumerable<string> indices)
    {
        Release();
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
            Release();
            throw;
        }

        _logger.Log(LogLevel.Info, LogTag,
            new LogMessageBuilder().Build($"Strategies recovered: {_strategies.Count}."));
    }

    private void Release()
    {
        foreach (var entry in _strategies) _pool.ReleaseStrategy(entry.Index);

        _strategies.Clear();
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

    private void TriggerAfterSpawn(ISndEntity entity, ISndContext ctx)
    {
        // 允许 AfterSpawn 中增删策略/清场等导致集合变化，因此对快照迭代。
        foreach (var s in _strategies.ToArray()) s.Strategy.AfterSpawn(entity, ctx);
    }

    private void TriggerAfterLoad(ISndEntity entity, ISndContext ctx)
    {
        // 允许 AfterLoad 中增删策略/清场等导致集合变化，因此对快照迭代。
        foreach (var s in _strategies.ToArray()) s.Strategy.AfterLoad(entity, ctx);
    }

    private void TriggerBeforeSave(ISndEntity entity, ISndContext ctx)
    {
        // 允许 BeforeSave 中增删策略，因此对快照迭代。
        foreach (var s in _strategies.ToArray()) s.Strategy.BeforeSave(entity, ctx);
    }

    private void TriggerBeforeQuit(ISndEntity entity, ISndContext ctx)
    {
        // 允许 BeforeQuit 中触发清场/销毁导致集合变化，因此对快照迭代。
        foreach (var s in _strategies.ToArray()) s.Strategy.BeforeQuit(entity, ctx);
    }

    private void TriggerBeforeDead(ISndEntity entity, ISndContext ctx)
    {
        // 允许 BeforeDead 中触发销毁导致集合变化，因此对快照迭代。
        foreach (var s in _strategies.ToArray()) s.Strategy.BeforeDead(entity, ctx);
    }

    private sealed class StrategyEntry
    {
        public required string Index { get; init; }
        public required EntityStrategyBase Strategy { get; init; }
    }
}
