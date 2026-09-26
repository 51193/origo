using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Linq;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.DataSource;
using Origo.Core.Snd.Strategy;
using Origo.Core.StateMachine;

namespace Origo.Core.Runtime.StateMachine;

/// <summary>
///     Manages multiple <see cref="StackStateMachine" /> instances by string key, with lifecycle aligned
///     to strategy pool reference counts. Depends on <see cref="IStateMachineContext" /> rather than a
///     concrete context type, ensuring frontend and backend can share the same state machine semantics.
/// </summary>
internal sealed class StateMachineContainer : IStateMachineContainer
{
    private readonly IStateMachineContext _ctx;
    private readonly List<string> _machineOrder = [];
    private readonly Dictionary<string, StackStateMachine> _machines = new(StringComparer.Ordinal);
    private readonly SndStrategyPool _pool;

    internal StateMachineContainer(SndStrategyPool pool, IStateMachineContext ctx)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(ctx);
        _pool = pool;
        _ctx = ctx;
    }

    /// <summary>Creates or retrieves a <see cref="StackStateMachine" /> by key. Throws if the key already exists with different strategy indices.</summary>
    public StackStateMachine CreateOrGet(string machineKey, string pushStrategyIndex, string popStrategyIndex)
    {
        if (string.IsNullOrWhiteSpace(machineKey))
            throw new ArgumentException("Machine key cannot be null or whitespace.", nameof(machineKey));
        if (string.IsNullOrWhiteSpace(pushStrategyIndex))
            throw new ArgumentException("Push strategy index cannot be null or whitespace.",
                nameof(pushStrategyIndex));
        if (string.IsNullOrWhiteSpace(popStrategyIndex))
            throw new ArgumentException("Pop strategy index cannot be null or whitespace.", nameof(popStrategyIndex));

        if (_machines.TryGetValue(machineKey, out var existing))
        {
            if (existing.PushStrategyIndex != pushStrategyIndex || existing.PopStrategyIndex != popStrategyIndex)
                throw new InvalidOperationException(
                    $"State machine '{machineKey}' already exists with different strategy indices.");

            return existing;
        }

        var sm = new StackStateMachine(machineKey, pushStrategyIndex, popStrategyIndex, _pool, _ctx);
        _machines[machineKey] = sm;
        _machineOrder.Add(machineKey);
        return sm;
    }

    /// <summary>Looks up an existing state machine instance by key.</summary>
    public bool TryGet(string machineKey, out StackStateMachine? machine) =>
        _machines.TryGetValue(machineKey, out machine);

    /// <summary>
    ///     Removes and disposes a state machine by key. Removing a machine
    ///     that is not present throws (fail-fast, consistent with the
    ///     strategy managers' remove contracts).
    /// </summary>
    public void Remove(string machineKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(machineKey);
        if (!_machines.TryGetValue(machineKey, out var sm))
            throw new InvalidOperationException(
                $"State machine '{machineKey}' does not exist in the container.");
        sm.Dispose();
        _machines.Remove(machineKey);
        _machineOrder.Remove(machineKey);
    }

    /// <summary>
    ///     Disposes all state machines and clears the container. Every machine
    ///     is disposed independently; the first failure propagates after all
    ///     machines are released and the dictionaries are cleared.
    /// </summary>
    public void Clear()
    {
        Exception? firstFailure = null;
        foreach (var sm in _machines.Values)
        {
            try
            {
                sm.Dispose();
            }
            catch (Exception ex)
            {
                firstFailure ??= ex;
            }
        }

        _machines.Clear();
        _machineOrder.Clear();

        if (firstFailure is not null)
            ExceptionDispatchInfo.Capture(firstFailure).Throw();
    }

    /// <summary>Executes <see cref="StackStateMachine.FlushAfterLoad" /> for all state machines in insertion order after loading.</summary>
    public void FlushAllAfterLoad()
        => ForEachMachine(sm => sm.FlushAfterLoad());

    /// <summary>Pops all state machine stacks at runtime, one by one.</summary>
    public void PopAllRuntime()
        => ForEachMachine(sm => { while (sm.TryPopRuntime(out _)) { } });

    /// <summary>Pops all state machine stacks during the quit process, one by one.</summary>
    public void PopAllOnQuit()
        => ForEachMachine(sm => { while (sm.TryPopOnQuit(out _)) { } });

    private void ForEachMachine(Action<StackStateMachine> action)
    {
        foreach (var sm in EnumerateMachinesInInsertionOrder())
            action(sm);
    }

    /// <summary>Serializes all state machines to a DataSource node.</summary>
    public DataSourceNode SerializeToNode(DataSourceConverterRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        var payload = new StateMachineContainerPayload();
        foreach (var key in _machineOrder)
        {
            if (!_machines.TryGetValue(key, out var sm))
                throw new InvalidOperationException($"StateMachineContainer order contains missing key '{key}'.");
            payload.Machines.Add(new StateMachineEntryPayload
            {
                Key = key,
                PushIndex = sm.PushStrategyIndex,
                PopIndex = sm.PopStrategyIndex,
                Stack = [.. sm.Snapshot()]
            });
        }

        return registry.Write(payload);
    }

    /// <summary>Restores all state machines from a DataSource node (without triggering hooks). Use together with <see cref="FlushAllAfterLoad" />.</summary>
    public void DeserializeFromNode(DataSourceNode serializedNode,
        DataSourceConverterRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(serializedNode);
        ArgumentNullException.ThrowIfNull(registry);
        var payload = registry.Read<StateMachineContainerPayload>(serializedNode);
        if (payload?.Machines is null)
            throw new InvalidOperationException("StateMachineContainer payload.machines is required.");

        var (newOrder, newMachines) = ValidateAndCreateMachines(payload);
        AtomicSwapAndDisposeOld(newOrder, newMachines);
    }

    private (List<string> order, Dictionary<string, StackStateMachine> machines)
        ValidateAndCreateMachines(StateMachineContainerPayload payload)
    {
        var newOrder = new List<string>(payload.Machines.Count);
        var newMachines = new Dictionary<string, StackStateMachine>(payload.Machines.Count, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            foreach (var entry in payload.Machines)
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                    throw new InvalidOperationException("StateMachineEntry key is required.");
                if (!seen.Add(entry.Key))
                    throw new InvalidOperationException($"Duplicate state machine key '{entry.Key}' in payload.");
                if (string.IsNullOrWhiteSpace(entry.PushIndex))
                    throw new InvalidOperationException($"StateMachineEntry '{entry.Key}' missing push index.");
                if (string.IsNullOrWhiteSpace(entry.PopIndex))
                    throw new InvalidOperationException($"StateMachineEntry '{entry.Key}' missing pop index.");
                if (entry.Stack is null)
                    throw new InvalidOperationException($"StateMachineEntry '{entry.Key}' stack is required.");

                var sm = new StackStateMachine(entry.Key, entry.PushIndex, entry.PopIndex, _pool, _ctx);
                ((IStateMachine)sm).RestoreStackWithoutHooks(entry.Stack);
                newMachines[entry.Key] = sm;
                newOrder.Add(entry.Key);
            }
        }
        catch
        {
            foreach (var sm in newMachines.Values)
                sm.Dispose();
            throw;
        }

        return (newOrder, newMachines);
    }

    private void AtomicSwapAndDisposeOld(
        List<string> newOrder,
        Dictionary<string, StackStateMachine> newMachines)
    {
        var oldMachines = new Dictionary<string, StackStateMachine>(_machines, StringComparer.Ordinal);
        _machines.Clear();
        _machineOrder.Clear();

        foreach (var key in newOrder)
        {
            _machineOrder.Add(key);
            _machines[key] = newMachines[key];
        }

        foreach (var sm in oldMachines.Values)
            sm.Dispose();
    }

    private IEnumerable<StackStateMachine> EnumerateMachinesInInsertionOrder()
    {
        foreach (var key in _machineOrder)
        {
            if (!_machines.TryGetValue(key, out var sm))
                throw new InvalidOperationException($"StateMachineContainer order contains missing key '{key}'.");
            yield return sm;
        }
    }

    IStateMachine IStateMachineContainer.CreateOrGet(string machineKey, string pushStrategyIndex, string popStrategyIndex)
        => CreateOrGet(machineKey, pushStrategyIndex, popStrategyIndex);

    bool IStateMachineContainer.TryGet(string machineKey, out IStateMachine? machine)
    {
        var result = _machines.TryGetValue(machineKey, out var sm);
        machine = sm;
        return result;
    }
}
