using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Logging;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd.Strategy;

/// <summary>
///     Per-scene-host observer binding topology: centrally manages the "who observes whom"
///     directed graph via bidirectional indices (_incoming / _outgoing). Cross-entity wiring,
///     unwiring, serialization, and load-time recovery are all handled within this class in a
///     self-contained manner, so entities do not need to expose their internal observer state.
///     <para>
///         Data change signal sources are driven by the target entity's
///         <see cref="ISndEntityRawSubscription" />; the topology handles binding lifecycle
///         orchestration — mount/unmount hook dispatch, save/load recovery, teardown on death —
///         but does not hold or proxy the data subscriptions themselves.
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

    /// <summary>
    ///     True when a context has been bound via <see cref="BindContext" />.
    ///     Used by startup orchestration to fail early when the scene host
    ///     is not fully wired before <c>SndContext.Bootstrap</c> runs.
    /// </summary>
    internal bool IsContextBound => _context is not null;

    internal void Mount(ISndEntity observer, ISndEntity target, string observerIndex)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(target);
        if (string.IsNullOrWhiteSpace(observerIndex))
            throw new ArgumentException("Observer strategy index cannot be null or whitespace.", nameof(observerIndex));
        var ctx = RequireContext();

        if (observer.IsPendingKill)
            throw new InvalidOperationException(
                $"Observer entity '{observer.Name}' is pending kill and cannot mount observer strategies.");
        if (target.IsPendingKill)
            throw new InvalidOperationException(
                $"Target entity '{target.Name}' is pending kill; observer strategies cannot be mounted on it.");

        // Observer bindings are scoped to a single scene host (one session).
        // Cross-session mounts would leak subscriptions that teardown cannot
        // resolve; reject them up front. Entities that are not session-bound
        // yet (offline construction, unit tests) skip this check.
        var observerSession = TryGetOwningSession(observer);
        var targetSession = TryGetOwningSession(target);
        if (observerSession is not null && targetSession is not null
            && !ReferenceEquals(observerSession, targetSession))
            throw new InvalidOperationException(
                $"Observer '{observer.Name}' and target '{target.Name}' belong to different sessions; " +
                "observer bindings are scoped to a single scene host.");

        // Mounting the same (observer, target, index) twice would double the
        // data subscription, double-fire OnMounted/OnDataChanged, and double
        // the pool reference — reject it up front (fail-fast), consistent with
        // the passive/active strategy managers' duplicate-mount rejection.
        if (FindBinding(observer.Name, target.Name, observerIndex) is not null)
            throw new InvalidOperationException(
                $"Observer strategy '{observerIndex}' is already mounted from '{observer.Name}' to '{target.Name}'.");

        ObserverStrategyBase? strategy = null;
        var acquired = false;
        ObserverBindingEntry? entry = null;
        var added = false;
        try
        {
            strategy = _pool.GetStrategy<ObserverStrategyBase>(observerIndex);
            acquired = true;

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
                void wrappedCb(ISndEntity t, TypedData o, TypedData n) =>
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
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Warning, nameof(ObserverTopology),
                new LogMessageBuilder()
                    .AddContext("observerName", observer.Name)
                    .AddContext("targetName", target.Name)
                    .AddContext("observerIndex", observerIndex)
                    .Build($"Observer mount failed, rolling back: {ex.Message}"));

            if (entry is not null)
            {
                foreach (var (key, wrapper) in entry.DataWrappers)
                    ((ISndEntityRawSubscription)target).UnsubscribeDataRaw(key, wrapper);
                if (added)
                    RemoveBinding(entry);
            }

            if (acquired)
                _pool.ReleaseStrategy(observerIndex);
            throw;
        }
    }

    internal void Unmount(ISndEntity observer, ISndEntity target, string observerIndex)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(target);
        var ctx = RequireContext();

        var binding = FindBinding(observer.Name, target.Name, observerIndex)
            ?? throw new InvalidOperationException(
                $"Observer strategy '{observerIndex}' is not mounted from '{observer.Name}' to '{target.Name}'.");

        RemoveBinding(binding);

        try
        {
            foreach (var (key, wrapper) in binding.DataWrappers)
                ((ISndEntityRawSubscription)target).UnsubscribeDataRaw(key, wrapper);

            binding.Strategy.OnUnmounted(observer, ctx, target);
        }
        finally
        {
            // The pool reference must be released even when unsubscription or
            // OnUnmounted throws; the binding is already removed from the
            // topology, so nothing else would release it.
            _pool.ReleaseStrategy(observerIndex);
        }

        _logger.Log(LogLevel.Debug, nameof(ObserverTopology),
            new LogMessageBuilder()
                .AddContext("observerName", observer.Name)
                .AddContext("targetName", target.Name)
                .AddContext("observerIndex", observerIndex)
                .Build("Observer strategy unmounted."));
    }

    /// <summary>
    ///     Releases all observer strategy references held by a given observer and clears its
    ///     outgoing edges (without triggering OnUnmounted, without unsubscribing). Corresponds to
    ///     the <c>ReleaseStrategiesOnly</c> phase in the entity's overall destruction flow.
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
                throw new InvalidOperationException(
                    $"Observer binding for '{observer.Name}' has an empty target. " +
                    "The save topology is inconsistent and cannot be recovered.");

            // A binding whose target cannot be resolved means the saved
            // topology references an entity that does not exist in the
            // recovered scene — inconsistent save data. Fail fast instead
            // of silently dropping the binding.
            var target = resolveTarget(b.Target)
                ?? throw new InvalidOperationException(
                    $"Observer binding for '{observer.Name}' targets '{b.Target}', " +
                    "but no entity with that name exists in the recovered scene. " +
                    "The save topology is inconsistent and cannot be recovered.");

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
                // The target no longer exists in the scene (it was removed
                // without going through teardown, or the scene is partially
                // deserialized). The binding cannot be unmounted normally;
                // release the pool reference and drop the record. Logged so
                // the silent drop stays observable.
                _logger.Log(LogLevel.Debug, nameof(ObserverTopology),
                    new LogMessageBuilder()
                        .AddContext("observerName", observer.Name)
                        .AddContext("targetName", binding.TargetName)
                        .AddContext("observerIndex", binding.ObserverIndex)
                        .Build("Observer binding removed because its target is no longer resolvable."));
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

    /// <summary>
    ///     Returns the names of all observers that currently hold a binding
    ///     targeting the given entity name. O(1) lookup through the incoming
    ///     index maintained by <see cref="AddBinding" /> and
    ///     <see cref="RemoveBinding" />. The returned snapshot is safe to
    ///     iterate while bindings are being removed.
    /// </summary>
    internal IReadOnlyList<string> GetObserverNamesTargeting(string targetName)
    {
        ArgumentNullException.ThrowIfNull(targetName);
        return _incoming.TryGetValue(targetName, out var observers) ? [.. observers] : [];
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
            return [];

        return [.. list
            .GroupBy(b => b.TargetName, StringComparer.Ordinal)
            .Select(g => new StrategyMetaData.ObserverBinding
            {
                Target = g.Key,
                ObserverIndices = [.. g.Select(b => b.ObserverIndex)]
            })];
    }

    private ISndContext RequireContext()
    {
        return _context
               ?? throw new InvalidOperationException(
                   "ObserverTopology context is not bound. The scene host must call BindContext before mounting observers.");
    }

    private static ISessionRun? TryGetOwningSession(ISndEntity entity)
    {
        try
        {
            return entity.OwningSession;
        }
        catch (InvalidOperationException)
        {
            // Entities created before session binding throw from
            // OwningSession; treat them as unbound.
            return null;
        }
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
            _outgoing[entry.ObserverName] = list = [];
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
