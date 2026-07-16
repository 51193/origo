using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Snd.Strategy;

namespace Origo.Core.StateMachine;

/// <summary>
///     A string-stack state machine: runtime <see cref="Push" /> invokes the push strategy's <see cref="StateMachineStrategyBase.OnPushRuntime" />;
///     post-load flush invokes <see cref="StateMachineStrategyBase.OnPushAfterLoad" />;
///     runtime pop invokes the Pop strategy's <see cref="StateMachineStrategyBase.OnPopRuntime" />;
///     quit-time staged pop invokes <see cref="StateMachineStrategyBase.OnPopBeforeQuit" />.
/// </summary>
public sealed class StackStateMachine : IStateMachine, IDisposable
{
    private readonly IStateMachineContext _ctx;
    private readonly SndStrategyPool _pool;
    private readonly StateMachineStrategyBase _popStrategy;
    private readonly StateMachineStrategyBase _pushStrategy;
    private readonly List<string> _stack = [];
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

    /// <summary>Releases the pool references for the Push and Pop strategies.</summary>
    public void Dispose()
    {
        if (_disposed) return;

        _pool.ReleaseStrategy(PushStrategyIndex);
        _pool.ReleaseStrategy(PopStrategyIndex);
        _disposed = true;
    }

    /// <summary>The logical key of this state machine in the container.</summary>
    public string MachineKey { get; }

    /// <summary>The index of the push strategy in the strategy pool.</summary>
    public string PushStrategyIndex { get; }

    /// <summary>The index of the pop strategy in the strategy pool.</summary>
    public string PopStrategyIndex { get; }

    /// <summary>Runtime push: pushes the value to the top of the stack, then invokes the push strategy's <see cref="StateMachineStrategyBase.OnPushRuntime" />.</summary>
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

    /// <summary>Runtime pop: invokes the Pop strategy's <see cref="StateMachineStrategyBase.OnPopRuntime" />, then removes the top of the stack.</summary>
    public bool TryPopRuntime(out string? popped)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return TryPopCore(out popped, (c) => _popStrategy.OnPopRuntime(c, _ctx));
    }

    /// <summary>Quit-time pop: invokes the Pop strategy's <see cref="StateMachineStrategyBase.OnPopBeforeQuit" />, then removes the top of the stack.</summary>
    public bool TryPopOnQuit(out string? popped)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return TryPopCore(out popped, (c) => _popStrategy.OnPopBeforeQuit(c, _ctx));
    }

    private bool TryPopCore(
        out string? popped,
        Action<StateMachineStrategyContext> popAction)
    {
        popped = null;
        if (_stack.Count == 0) return false;

        var beforeTop = PeekTopOrNull();
        var willPop = _stack[^1];
        var afterTop = _stack.Count > 1 ? _stack[^2] : null;

        var context = new StateMachineStrategyContext(MachineKey, beforeTop, afterTop);
        popAction(context);

        _stack.RemoveAt(_stack.Count - 1);
        popped = willPop;
        return true;
    }

    /// <summary>Peeks at the top element without popping. found is false when the stack is empty.</summary>
    public (bool found, string? top) Peek()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_stack.Count == 0) return (false, null);
        return (true, _stack[^1]);
    }

    /// <summary>Returns a read-only snapshot of the stack (from bottom to top).</summary>
    public IReadOnlyList<string> Snapshot()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return [.. _stack];
    }

    /// <summary>Restores stack contents from a snapshot without triggering any strategy hooks. Use together with <see cref="FlushAfterLoad" />.</summary>
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

    /// <summary>After loading, invokes the push strategy's <see cref="StateMachineStrategyBase.OnPushAfterLoad" /> for each layer in bottom-to-top order.</summary>
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
