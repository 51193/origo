using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;

namespace Origo.Core.Snd.Strategy;

/// <summary>
///     Manages the collection of active strategies on a single entity.
///     Completely independent from <see cref="SndStrategyManager" /> (entity passive strategies),
///     sharing <see cref="SndStrategyPool" /> as the source of strategy instances.
///     Internally uses a Dictionary for O(1) index lookup and does not participate in frame traversal.
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
    ///     Bulk-recovers active strategies from metadata (called during Spawn/Load).
    ///     Iterates all indices; encountering a non-ActiveStrategyBase type is treated as data
    ///     corruption due to save/code inconsistency, immediately releasing the strategy and
    ///     throwing <see cref="InvalidOperationException" />. On partial failure during recovery,
    ///     rolls back all acquired active strategies, leaving no half-initialized state.
    /// </summary>
    public void Recover(IEnumerable<string> indices)
    {
        ReleaseAll();
        try
        {
            foreach (var index in indices)
                AcquireOrThrow(index);
        }
        catch
        {
            ReleaseAll();
            throw;
        }
    }

    /// <summary>Dynamically adds an active strategy.</summary>
    public void Add(string index)
    {
        if (string.IsNullOrWhiteSpace(index))
            throw new ArgumentException("Strategy index cannot be null or whitespace.", nameof(index));
        if (_active.ContainsKey(index))
            throw new InvalidOperationException($"Active strategy '{index}' is already attached.");
        if (!TryAcquire(index))
            throw new InvalidOperationException($"Strategy '{index}' is not an ActiveStrategyBase.");
    }

    /// <summary>
    ///     Dynamically removes an active strategy, releasing its pool reference.
    ///     Removing a strategy that is not attached throws.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="index" /> is not attached to the entity.</exception>
    public void Remove(string index)
    {
        if (!_active.Remove(index, out _))
            throw new InvalidOperationException($"Active strategy '{index}' is not attached.");
        _pool.ReleaseStrategy(index);
    }

    /// <summary>Invokes the strategy actively and returns the result.</summary>
    public object? Invoke(ISndEntity entity, ISndContext ctx, string index, object? input)
    {
        if (!_active.TryGetValue(index, out var active))
            throw new InvalidOperationException(
                $"Active strategy '{index}' not found on entity '{entity.Name}'.");
        return active.Invoke(entity, ctx, input);
    }

    /// <summary>Serializes all currently held active strategy indices.</summary>
    public IReadOnlyCollection<string> SerializeIndices() => _active.Keys;

    /// <summary>Releases all active strategies (called during Quit/Dead).</summary>
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

    private void AcquireOrThrow(string index)
    {
        var strategy = _pool.GetStrategy<BaseStrategy>(index);
        if (strategy is ActiveStrategyBase active)
        {
            _active[index] = active;
            return;
        }

        _pool.ReleaseStrategy(index);
        throw new InvalidOperationException(
            $"Strategy '{index}' (type '{strategy.GetType().FullName}') is not an ActiveStrategyBase " +
            "and cannot be recovered as an active strategy. " +
            "Active strategies must inherit from ActiveStrategyBase.");
    }
}
