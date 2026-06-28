using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Logging;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd.Strategy;

/// <summary>
///     per-scene-host 的观察者绑定拓扑：以双向索引（_incoming / _outgoing）集中管理
///     "谁观察谁"的有向图。跨实体的接线、拆线、序列化与读档恢复均在本类闭环，
///     实体无需反向暴露内部观察者状态。
///     <para>
///         数据变更信号源由 target 实体的 <see cref="ISndEntityRawSubscription" /> 驱动；
///         拓扑负责绑定生命周期的编排——挂载/卸载钩子分发、读写档恢复、死亡时拆线——
///         但不持有或代理数据订阅本身。
///     </para>
/// </summary>
internal sealed class ObserverTopology
{
    private readonly Dictionary<string, HashSet<string>> _incoming = new(StringComparer.Ordinal);
    private readonly ILogger _logger;
    private readonly Dictionary<string, List<ObserverBindingEntry>> _outgoing = new(StringComparer.Ordinal);
    private readonly SndStrategyPool _pool;
    private ISndContext? _context;

    public ObserverTopology(SndStrategyPool pool, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(logger);
        _pool = pool;
        _logger = logger;
    }

    internal void BindContext(ISndContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    internal void Mount(ISndEntity observer, ISndEntity target, string observerIndex)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(target);
        if (string.IsNullOrWhiteSpace(observerIndex))
            throw new ArgumentException("Observer strategy index cannot be null or whitespace.", nameof(observerIndex));
        var ctx = RequireContext();

        var strategy = _pool.GetStrategy<ObserverStrategyBase>(observerIndex);
        ObserverBindingEntry? entry = null;
        var added = false;
        try
        {
            var keys = ObserverStrategyMetadata.GetDataKeys(strategy.GetType());
            entry = new ObserverBindingEntry
            {
                ObserverName = observer.Name,
                TargetName = target.Name,
                ObserverIndex = observerIndex,
                Strategy = strategy,
                DataKeys = keys,
                TargetEntity = target
            };

            foreach (var key in keys)
            {
                var observerCapture = observer;
                var strategyCapture = strategy;
                var targetCapture = target;
                Action<ISndEntity, TypedData, TypedData> wrappedCb = (t, o, n) =>
                    strategyCapture.OnDataChanged(observerCapture, ctx, targetCapture, key, o, n);
                ((ISndEntityRawSubscription)target).SubscribeDataRaw(key, wrappedCb, null);
                entry.DataWrappers[key] = wrappedCb;
            }

            AddBinding(entry);
            added = true;

            strategy.OnMounted(observer, ctx, target);

            _logger.Log(LogLevel.Debug, nameof(ObserverTopology),
                new LogMessageBuilder()
                    .AddContext("observerName", observer.Name)
                    .AddContext("targetName", target.Name)
                    .AddContext("observerIndex", observerIndex)
                    .Build("Observer strategy mounted."));
        }
        catch
        {
            if (entry is not null)
            {
                foreach (var (key, wrapper) in entry.DataWrappers)
                    ((ISndEntityRawSubscription)target).UnsubscribeDataRaw(key, wrapper);
                if (added)
                    RemoveBinding(entry);
            }

            _pool.ReleaseStrategy(observerIndex);
            throw;
        }
    }

    internal void Unmount(ISndEntity observer, ISndEntity target, string observerIndex)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(target);
        var ctx = RequireContext();

        var binding = FindBinding(observer.Name, target.Name, observerIndex);
        if (binding is null) return;

        RemoveBinding(binding);

        foreach (var (key, wrapper) in binding.DataWrappers)
            ((ISndEntityRawSubscription)target).UnsubscribeDataRaw(key, wrapper);

        binding.Strategy.OnUnmounted(observer, ctx, target);

        _pool.ReleaseStrategy(observerIndex);

        _logger.Log(LogLevel.Debug, nameof(ObserverTopology),
            new LogMessageBuilder()
                .AddContext("observerName", observer.Name)
                .AddContext("targetName", target.Name)
                .AddContext("observerIndex", observerIndex)
                .Build("Observer strategy unmounted."));
    }

    /// <summary>
    ///     释放某观察者持有的全部观察者策略引用并清空其出边（不触发 OnUnmounted、不退订）。
    ///     对应实体整体销毁流程中的 <c>ReleaseStrategiesOnly</c> 阶段。
    /// </summary>
    internal void ReleaseStrategiesFor(ISndEntity observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        if (!_outgoing.TryGetValue(observer.Name, out var list))
            return;

        foreach (var binding in list)
            _pool.ReleaseStrategy(binding.ObserverIndex);

        RemoveAllOutgoing(observer.Name);
    }

    internal void RecoverBindingsFor(ISndEntity observer,
        IReadOnlyList<StrategyMetaData.ObserverBinding> bindings,
        Func<string, ISndEntity?> resolveTarget)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(bindings);

        foreach (var b in bindings)
        {
            if (string.IsNullOrWhiteSpace(b.Target))
                continue;

            var target = resolveTarget(b.Target);
            if (target is null)
            {
                _logger.Log(LogLevel.Debug, nameof(ObserverTopology),
                    new LogMessageBuilder()
                        .AddContext("observerName", observer.Name)
                        .AddContext("targetName", b.Target)
                        .Build("Observer binding target not found, skipping recovery."));
                continue;
            }

            foreach (var index in b.ObserverIndices)
                Mount(observer, target, index);
        }
    }

    internal void TeardownOutgoingFor(ISndEntity observer,
        Func<string, ISndEntity?> resolveTarget)
    {
        ArgumentNullException.ThrowIfNull(observer);
        if (!_outgoing.TryGetValue(observer.Name, out var list))
            return;

        foreach (var binding in list.ToArray())
        {
            var target = resolveTarget(binding.TargetName);
            if (target is null)
            {
                _pool.ReleaseStrategy(binding.ObserverIndex);
                RemoveBinding(binding);
                continue;
            }

            Unmount(observer, target, binding.ObserverIndex);
        }
    }

    internal void TeardownAllBindingsFor(ISndEntity observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        if (!_outgoing.TryGetValue(observer.Name, out var list))
            return;

        var ctx = RequireContext();
        foreach (var binding in list.ToArray())
        {
            RemoveBinding(binding);

            if (binding.TargetEntity is not null)
                binding.FullCleanup(observer, ctx, _pool);
            else
                throw new InvalidOperationException(
                    $"Observer binding '{binding.ObserverIndex}' -> '{binding.TargetName}' " +
                    "has no TargetEntity reference. This is a framework invariant violation — " +
                    "all live bindings must store a TargetEntity reference.");
        }
    }

    internal bool HasBindingTargetingFrom(string observerName, string targetName)
    {
        return _outgoing.TryGetValue(observerName, out var list)
               && list.Any(b => string.Equals(b.TargetName, targetName, StringComparison.Ordinal));
    }

    internal void RemoveBindingsTargetingFor(ISndEntity observer, string targetName)
    {
        ArgumentNullException.ThrowIfNull(observer);
        if (!_outgoing.TryGetValue(observer.Name, out var list))
            return;

        var ctx = RequireContext();
        foreach (var binding in list.ToArray())
        {
            if (!string.Equals(binding.TargetName, targetName, StringComparison.Ordinal))
                continue;

            RemoveBinding(binding);

            if (binding.TargetEntity is not null)
                binding.FullCleanup(observer, ctx, _pool);
            else
                throw new InvalidOperationException(
                    $"Observer binding '{binding.ObserverIndex}' -> '{binding.TargetName}' " +
                    "has no TargetEntity reference for cleanup. " +
                    "Bindings recovered from serialization require a scene host to resolve the target.");
        }
    }

    internal IReadOnlyList<StrategyMetaData.ObserverBinding> BuildBindingsFor(string observerName)
    {
        if (!_outgoing.TryGetValue(observerName, out var list) || list.Count == 0)
            return Array.Empty<StrategyMetaData.ObserverBinding>();

        return list
            .GroupBy(b => b.TargetName, StringComparer.Ordinal)
            .Select(g => new StrategyMetaData.ObserverBinding
            {
                Target = g.Key,
                ObserverIndices = g.Select(b => b.ObserverIndex).ToList()
            })
            .ToArray();
    }

    private ISndContext RequireContext()
    {
        return _context
               ?? throw new InvalidOperationException(
                   "ObserverTopology context is not bound. The scene host must call BindContext before mounting observers.");
    }

    private ObserverBindingEntry? FindBinding(string observerName, string targetName, string observerIndex)
    {
        if (!_outgoing.TryGetValue(observerName, out var list))
            return null;

        foreach (var b in list)
            if (string.Equals(b.TargetName, targetName, StringComparison.Ordinal)
                && string.Equals(b.ObserverIndex, observerIndex, StringComparison.Ordinal))
                return b;

        return null;
    }

    private void AddBinding(ObserverBindingEntry entry)
    {
        if (!_outgoing.TryGetValue(entry.ObserverName, out var list))
            _outgoing[entry.ObserverName] = list = new List<ObserverBindingEntry>();
        list.Add(entry);

        if (!_incoming.TryGetValue(entry.TargetName, out var observers))
            _incoming[entry.TargetName] = observers = new HashSet<string>(StringComparer.Ordinal);
        observers.Add(entry.ObserverName);
    }

    private void RemoveBinding(ObserverBindingEntry entry)
    {
        if (!_outgoing.TryGetValue(entry.ObserverName, out var list))
            return;

        list.Remove(entry);
        if (list.Count == 0)
            _outgoing.Remove(entry.ObserverName);

        var stillTargets = list.Any(b => string.Equals(b.TargetName, entry.TargetName, StringComparison.Ordinal));
        if (!stillTargets && _incoming.TryGetValue(entry.TargetName, out var observers))
        {
            observers.Remove(entry.ObserverName);
            if (observers.Count == 0)
                _incoming.Remove(entry.TargetName);
        }
    }

    private void RemoveAllOutgoing(string observerName)
    {
        if (!_outgoing.TryGetValue(observerName, out var list))
            return;

        var targets = list.Select(b => b.TargetName).Distinct(StringComparer.Ordinal).ToArray();
        _outgoing.Remove(observerName);

        foreach (var target in targets)
            if (_incoming.TryGetValue(target, out var observers))
            {
                observers.Remove(observerName);
                if (observers.Count == 0)
                    _incoming.Remove(target);
            }
    }
}
