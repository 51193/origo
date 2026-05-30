using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;

namespace Origo.Core.Snd.Strategy;

/// <summary>
///     管理单个实体上的主动策略集合。
///     与 <see cref="SndStrategyManager" />（实体被动策略）完全独立，
///     共享 <see cref="SndStrategyPool" /> 作为策略实例来源。
///     内部使用 Dictionary 实现 O(1) 索引查找，不参与帧遍历。
/// </summary>
internal sealed class ActiveStrategyManager
{
    private readonly Dictionary<string, ActiveStrategyBase> _active = new(StringComparer.Ordinal);
    private readonly SndStrategyPool _pool;

    public ActiveStrategyManager(SndStrategyPool pool)
    {
        ArgumentNullException.ThrowIfNull(pool);
        _pool = pool;
    }

    /// <summary>
    ///     从 metadata 批量恢复主动策略（Spawn/Load 时调用）。
    ///     遍历全部 indices，仅保留 ActiveStrategyBase 类型，其余立即释放。
    /// </summary>
    public void Recover(IEnumerable<string> indices)
    {
        ReleaseAll();
        foreach (var index in indices)
            TryAcquire(index);
    }

    /// <summary>动态添加主动策略。</summary>
    public void Add(string index)
    {
        if (string.IsNullOrWhiteSpace(index))
            throw new ArgumentException("Strategy index cannot be null or whitespace.", nameof(index));
        if (_active.ContainsKey(index))
            throw new InvalidOperationException($"Active strategy '{index}' is already attached.");
        if (!TryAcquire(index))
            throw new InvalidOperationException($"Strategy '{index}' is not an ActiveStrategyBase.");
    }

    /// <summary>动态移除主动策略。不存在的 index 静默忽略。</summary>
    public void Remove(string index)
    {
        if (_active.Remove(index, out _))
            _pool.ReleaseStrategy(index);
    }

    /// <summary>主动调用策略并返回结果。</summary>
    public object? Invoke(ISndEntity entity, ISndContext ctx, string index, object? input)
    {
        if (!_active.TryGetValue(index, out var active))
            throw new InvalidOperationException(
                $"Active strategy '{index}' not found on entity '{entity.Name}'.");
        return active.Invoke(entity, ctx, input);
    }

    /// <summary>序列化当前持有的全部主动策略索引。</summary>
    public IReadOnlyCollection<string> SerializeIndices()
    {
        return _active.Keys;
    }

    /// <summary>释放全部主动策略（Quit/Dead 时调用）。</summary>
    public void ReleaseAll()
    {
        foreach (var index in _active.Keys)
            _pool.ReleaseStrategy(index);
        _active.Clear();
    }

    private bool TryAcquire(string index)
    {
        var strategy = _pool.GetStrategy<BaseStrategy>(index);
        if (strategy is ActiveStrategyBase active)
        {
            _active[index] = active;
            return true;
        }

        _pool.ReleaseStrategy(index);
        return false;
    }
}