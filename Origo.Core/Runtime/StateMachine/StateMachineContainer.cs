using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.DataSource;
using Origo.Core.Snd.Strategy;
using Origo.Core.StateMachine;

namespace Origo.Core.Runtime.StateMachine;

/// <summary>
///     按字符串 key 管理多个 <see cref="StackStateMachine" />，生命周期与策略池引用计数对齐。
///     依赖 <see cref="IStateMachineContext" /> 而非具体上下文类型，确保前后台可共用同一状态机语义。
/// </summary>
public sealed class StateMachineContainer
{
    private readonly IStateMachineContext _ctx;
    private readonly List<string> _machineOrder = new();
    private readonly Dictionary<string, StackStateMachine> _machines = new(StringComparer.Ordinal);
    private readonly SndStrategyPool _pool;

    internal StateMachineContainer(SndStrategyPool pool, IStateMachineContext ctx)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(ctx);
        _pool = pool;
        _ctx = ctx;
    }

    /// <summary>按 key 创建或获取一个 <see cref="StackStateMachine" />。若 key 已存在但策略索引不同，则抛异常。</summary>
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

    /// <summary>按 key 查找已有的状态机实例。</summary>
    public bool TryGet(string machineKey, out StackStateMachine? machine) =>
        _machines.TryGetValue(machineKey, out machine);

    /// <summary>按 key 移除并释放一个状态机。</summary>
    public void Remove(string machineKey)
    {
        if (!_machines.TryGetValue(machineKey, out var sm)) return;
        sm.Dispose();
        _machines.Remove(machineKey);
        _machineOrder.Remove(machineKey);
    }

    /// <summary>释放所有状态机并清空容器。</summary>
    public void Clear()
    {
        foreach (var sm in _machines.Values)
            sm.Dispose();

        _machines.Clear();
        _machineOrder.Clear();
    }

    /// <summary>读档恢复后，按插入顺序对所有状态机执行 <see cref="StackStateMachine.FlushAfterLoad" />。</summary>
    public void FlushAllAfterLoad()
    {
        foreach (var sm in EnumerateMachinesInInsertionOrder())
            sm.FlushAfterLoad();
    }

    /// <summary>运行时逐个弹空所有状态机栈。</summary>
    public void PopAllRuntime()
    {
        foreach (var sm in EnumerateMachinesInInsertionOrder())
            while (sm.TryPopRuntime(out _))
            {
                // Let-it-crash: any hook/state error should surface immediately.
            }
    }

    /// <summary>退出流程逐个弹空所有状态机栈。</summary>
    public void PopAllOnQuit()
    {
        foreach (var sm in EnumerateMachinesInInsertionOrder())
            while (sm.TryPopOnQuit(out _))
            {
                // Let-it-crash: any hook/state error should surface immediately.
            }
    }

    /// <summary>将所有状态机序列化为 DataSource 节点。</summary>
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
                Stack = sm.Snapshot().ToList()
            });
        }

        return registry.Write(payload);
    }

    /// <summary>从 DataSource 节点恢复所有状态机（不触发钩子），配合 <see cref="FlushAllAfterLoad" /> 使用。</summary>
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
                sm.RestoreStackWithoutHooks(entry.Stack);
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
}
