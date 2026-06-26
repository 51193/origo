using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Logging;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd.Strategy;

internal sealed class ObserverStrategyManager
{
    private static readonly IReadOnlyCollection<string> EmptyIndices = Array.Empty<string>();
    private readonly List<ObserverBindingEntry> _bindings = new();
    private readonly ISndContext _context;
    private readonly ILogger _logger;
    private readonly SndStrategyPool _pool;

    public ObserverStrategyManager(SndStrategyPool pool, ISndContext context, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        _pool = pool;
        _context = context;
        _logger = logger;
    }

    internal void RecoverStrategiesOnly(IEnumerable<string> indices)
    {
        foreach (var index in indices)
        {
            var strategy = _pool.GetStrategy<ObserverStrategyBase>(index);
            _pool.ReleaseStrategy(index);
        }
    }

    internal void Mount(ISndEntity entity, ISndEntity target, string observerIndex)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(target);
        if (string.IsNullOrWhiteSpace(observerIndex))
            throw new ArgumentException("Observer strategy index cannot be null or whitespace.", nameof(observerIndex));

        var strategy = _pool.GetStrategy<ObserverStrategyBase>(observerIndex);
        try
        {
            var keys = ObserverStrategyMetadata.GetDataKeys(strategy.GetType());
            var entry = new ObserverBindingEntry
            {
                TargetName = target.Name,
                ObserverIndex = observerIndex,
                Strategy = strategy,
                DataKeys = keys,
                TargetEntity = target
            };

            foreach (var key in keys)
            {
                var entityCapture = entity;
                var strategyCapture = strategy;
                var targetCapture = target;
                Action<ISndEntity, TypedData, TypedData> wrappedCb = (t, o, n) =>
                    strategyCapture.OnDataChanged(entityCapture, _context, targetCapture, key, o, n);
                entry.DataWrappers[key] = wrappedCb;
                ((ISndEntityRawSubscription)target).SubscribeDataRaw(key, wrappedCb, null);
            }

            _bindings.Add(entry);

            strategy.OnMounted(entity, _context, target);

            _logger.Log(LogLevel.Debug, nameof(ObserverStrategyManager),
                new LogMessageBuilder()
                    .AddContext("entityName", entity.Name)
                    .AddContext("targetName", target.Name)
                    .AddContext("observerIndex", observerIndex)
                    .Build("Observer strategy mounted."));
        }
        catch
        {
            _pool.ReleaseStrategy(observerIndex);
            throw;
        }
    }

    internal void Unmount(ISndEntity entity, ISndEntity target, string observerIndex)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(target);

        var i = FindBindingIndex(target.Name, observerIndex);
        if (i < 0) return;

        var binding = _bindings[i];
        _bindings.RemoveAt(i);

        foreach (var (key, wrapper) in binding.DataWrappers)
            ((ISndEntityRawSubscription)target).UnsubscribeDataRaw(key, wrapper);

        binding.Strategy.OnUnmounted(entity, _context, target);

        _pool.ReleaseStrategy(observerIndex);

        _logger.Log(LogLevel.Debug, nameof(ObserverStrategyManager),
            new LogMessageBuilder()
                .AddContext("entityName", entity.Name)
                .AddContext("targetName", target.Name)
                .AddContext("observerIndex", observerIndex)
                .Build("Observer strategy unmounted."));
    }

    internal void ReleaseStrategiesOnly()
    {
        foreach (var binding in _bindings)
            _pool.ReleaseStrategy(binding.ObserverIndex);
        _bindings.Clear();
    }

    internal void RecoverBindings(ISndEntity entity,
        IReadOnlyList<StrategyMetaData.ObserverBinding> bindings,
        Func<string, ISndEntity?> resolveTarget)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(bindings);

        foreach (var b in bindings)
        {
            if (string.IsNullOrWhiteSpace(b.Target))
                continue;

            var target = resolveTarget(b.Target);
            if (target is null)
            {
                _logger.Log(LogLevel.Debug, nameof(ObserverStrategyManager),
                    new LogMessageBuilder()
                        .AddContext("entityName", entity.Name)
                        .AddContext("targetName", b.Target)
                        .Build("Observer binding target not found, skipping recovery."));
                continue;
            }

            foreach (var index in b.ObserverIndices)
                Mount(entity, target, index);
        }
    }

    internal void TeardownOutgoingBindings(ISndEntity entity,
        Func<string, ISndEntity?> resolveTarget)
    {
        var snapshot = _bindings.ToArray();
        foreach (var binding in snapshot)
        {
            var target = resolveTarget(binding.TargetName);
            if (target is null)
            {
                _pool.ReleaseStrategy(binding.ObserverIndex);
                _bindings.Remove(binding);
                continue;
            }

            Unmount(entity, target, binding.ObserverIndex);
        }
    }

    internal void TeardownAllBindings(ISndEntity entity)
    {
        var snapshot = _bindings.ToArray();
        foreach (var binding in snapshot)
        {
            _bindings.Remove(binding);

            if (binding.TargetEntity is not null)
                binding.FullCleanup(entity, _context, _pool);
            else
                throw new InvalidOperationException(
                    $"Observer binding '{binding.ObserverIndex}' -> '{binding.TargetName}' " +
                    "has no TargetEntity reference. This is a framework invariant violation — " +
                    "all live bindings must store a TargetEntity reference.");
        }
    }

    internal bool HasBindingTargeting(string targetName)
    {
        return _bindings.Any(b => b.TargetName == targetName);
    }

    internal void RemoveAllBindingsTargeting(string targetName, ISndEntity observerEntity)
    {
        for (var i = _bindings.Count - 1; i >= 0; i--)
        {
            var binding = _bindings[i];
            if (binding.TargetName != targetName)
                continue;

            _bindings.RemoveAt(i);

            if (binding.TargetEntity is not null)
                binding.FullCleanup(observerEntity, _context, _pool);
            else
                throw new InvalidOperationException(
                    $"Observer binding '{binding.ObserverIndex}' -> '{binding.TargetName}' " +
                    "has no TargetEntity reference for cleanup. " +
                    "Bindings recovered from serialization require a scene host to resolve the target.");
        }
    }

    internal IReadOnlyList<ObserverBindingEntry> SnapshotBindings()
    {
        return _bindings.ToArray();
    }

    internal IReadOnlyList<StrategyMetaData.ObserverBinding> BuildObserverBindings()
    {
        if (_bindings.Count == 0)
            return Array.Empty<StrategyMetaData.ObserverBinding>();

        return _bindings
            .GroupBy(b => b.TargetName)
            .Select(g => new StrategyMetaData.ObserverBinding
            {
                Target = g.Key,
                ObserverIndices = g.Select(b => b.ObserverIndex).ToList()
            })
            .ToArray();
    }

    private int FindBindingIndex(string targetName, string observerIndex)
    {
        for (var i = 0; i < _bindings.Count; i++)
        {
            var b = _bindings[i];
            if (b.TargetName == targetName && b.ObserverIndex == observerIndex)
                return i;
        }

        return -1;
    }
}
