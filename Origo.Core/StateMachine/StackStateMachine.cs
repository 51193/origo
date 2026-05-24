using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Snd.Strategy;

namespace Origo.Core.StateMachine;

/// <summary>
///     字符串栈状态机：运行时 <see cref="Push" /> 调用 Push 策略的 <see cref="StateMachineStrategyBase.OnPushRuntime" />；
///     读档刷新调用 <see cref="StateMachineStrategyBase.OnPushAfterLoad" />；
///     运行时出栈调用 Pop 策略的 <see cref="StateMachineStrategyBase.OnPopRuntime" />；
///     退出逐级出栈调用 <see cref="StateMachineStrategyBase.OnPopBeforeQuit" />。
/// </summary>
public sealed class StackStateMachine : IStateMachine, IDisposable
{
    private readonly IStateMachineContext _ctx;
    private readonly SndStrategyPool _pool;
    private readonly StateMachineStrategyBase _popStrategy;
    private readonly StateMachineStrategyBase _pushStrategy;
    private readonly List<string> _stack = new();
    private bool _disposed;

    internal StackStateMachine(
        string machineKey,
        string pushStrategyIndex,
        string popStrategyIndex,
        SndStrategyPool pool,
        IStateMachineContext ctx)
    {
        ArgumentNullException.ThrowIfNull(machineKey);
        ArgumentNullException.ThrowIfNull(pushStrategyIndex);
        ArgumentNullException.ThrowIfNull(popStrategyIndex);
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(ctx);
        MachineKey = machineKey;
        PushStrategyIndex = pushStrategyIndex;
        PopStrategyIndex = popStrategyIndex;
        _pool = pool;
        _ctx = ctx;

        _pushStrategy = _pool.GetStrategy<StateMachineStrategyBase>(PushStrategyIndex);
        try
        {
            _popStrategy = _pool.GetStrategy<StateMachineStrategyBase>(PopStrategyIndex);
        }
        catch
        {
            _pool.ReleaseStrategy(PushStrategyIndex);
            throw;
        }
    }

    /// <summary>释放 Push/Pop 策略的池引用。</summary>
    public void Dispose()
    {
        if (_disposed) return;

        _pool.ReleaseStrategy(PushStrategyIndex);
        _pool.ReleaseStrategy(PopStrategyIndex);
        _disposed = true;
    }

    /// <summary>此状态机在容器中的逻辑键。</summary>
    public string MachineKey { get; }

    /// <summary>入栈策略在策略池中的索引。</summary>
    public string PushStrategyIndex { get; }

    /// <summary>出栈策略在策略池中的索引。</summary>
    public string PopStrategyIndex { get; }

    /// <summary>运行时入栈：将值压入栈顶，然后调用 Push 策略的 <see cref="StateMachineStrategyBase.OnPushRuntime" />。</summary>
    public void Push(string value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("State machine stack value cannot be null/empty.", nameof(value));

        var beforeTop = PeekTopOrNull();
        _stack.Add(value);
        var afterTop = PeekTopOrNull();

        var context = new StateMachineStrategyContext(MachineKey, beforeTop, afterTop);
        _pushStrategy.OnPushRuntime(context, _ctx);
    }

    /// <summary>运行时出栈：调用 Pop 策略的 <see cref="StateMachineStrategyBase.OnPopRuntime" />，然后移除栈顶。</summary>
    public bool TryPopRuntime(out string? popped)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        popped = null;
        if (_stack.Count == 0) return false;

        var beforeTop = PeekTopOrNull();
        var willPop = _stack[^1];
        var afterTop = _stack.Count > 1 ? _stack[^2] : null;

        var context = new StateMachineStrategyContext(MachineKey, beforeTop, afterTop);
        _popStrategy.OnPopRuntime(context, _ctx);

        _stack.RemoveAt(_stack.Count - 1);
        popped = willPop;
        return true;
    }

    /// <summary>退出流程出栈：调用 Pop 策略的 <see cref="StateMachineStrategyBase.OnPopBeforeQuit" />，然后移除栈顶。</summary>
    public bool TryPopOnQuit(out string? popped)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        popped = null;
        if (_stack.Count == 0) return false;

        var beforeTop = PeekTopOrNull();
        var willPop = _stack[^1];
        var afterTop = _stack.Count > 1 ? _stack[^2] : null;

        var context = new StateMachineStrategyContext(MachineKey, beforeTop, afterTop);
        _popStrategy.OnPopBeforeQuit(context, _ctx);

        _stack.RemoveAt(_stack.Count - 1);
        popped = willPop;
        return true;
    }

    /// <summary>查看栈顶元素，不弹出。空栈时 found 为 false。</summary>
    public (bool found, string? top) Peek()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_stack.Count == 0) return (false, null);
        return (true, _stack[^1]);
    }

    /// <summary>返回栈的只读快照（从栈底到栈顶）。</summary>
    public IReadOnlyList<string> Snapshot()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _stack.ToArray();
    }

    /// <summary>从快照恢复栈内容，不触发任何策略钩子。配合 <see cref="FlushAfterLoad" /> 使用。</summary>
    public void RestoreStackWithoutHooks(IReadOnlyList<string> stackBottomToTop)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(stackBottomToTop);

        _stack.Clear();
        foreach (var v in stackBottomToTop)
        {
            if (string.IsNullOrWhiteSpace(v))
                throw new InvalidOperationException("State machine snapshot contains null/empty value.");
            _stack.Add(v);
        }
    }

    /// <summary>读档恢复后，按从栈底到栈顶顺序对每层调用 Push 策略的 <see cref="StateMachineStrategyBase.OnPushAfterLoad" />。</summary>
    public void FlushAfterLoad()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        for (var i = 0; i < _stack.Count; i++)
        {
            var beforeTop = i == 0 ? null : _stack[i - 1];
            var afterTop = _stack[i];
            var context = new StateMachineStrategyContext(MachineKey, beforeTop, afterTop);
            _pushStrategy.OnPushAfterLoad(context, _ctx);
        }
    }

    private string? PeekTopOrNull() => _stack.Count == 0 ? null : _stack[^1];
}
