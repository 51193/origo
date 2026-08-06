using System.Collections.Generic;

namespace Origo.Core.Abstractions.StateMachine;

/// <summary>
///     A string-stack state machine. The stack stores only strings;
///     Push/Pop semantics are implemented by strategy hooks.
/// </summary>
public interface IStateMachine
{
    /// <summary>The logical key of this state machine in the container.</summary>
    string MachineKey { get; }

    /// <summary>The index of the push strategy in the strategy pool.</summary>
    string PushStrategyIndex { get; }

    /// <summary>The index of the pop strategy in the strategy pool.</summary>
    string PopStrategyIndex { get; }

    /// <summary>Runtime push: pushes the value to the top of the stack, then invokes the push strategy's runtime hook.</summary>
    void Push(string value);

    /// <summary>
    ///     Runtime pop: triggers the pop strategy's <c>BeforeRemove</c> semantics.
    /// </summary>
    bool TryPopRuntime(out string? popped);

    /// <summary>
    ///     Quit-time pop: triggers the pop strategy's <c>BeforeQuit</c> semantics.
    /// </summary>
    bool TryPopOnQuit(out string? popped);

    /// <summary>Peeks at the top element without popping; <c>found</c> is <c>false</c> when the stack is empty.</summary>
    (bool found, string? top) Peek();

    /// <summary>
    ///     String snapshot from stack bottom to top.
    /// </summary>
    IReadOnlyList<string> Snapshot();

    /// <summary>
    ///     After loading from a save, invokes Push strategy <c>AfterLoad</c>
    ///     in stack-push order once scene construction is complete.
    /// </summary>
    void FlushAfterLoad();

    /// <summary>
    ///     Restore stack contents from a save without triggering any strategy hooks.
    ///     Internal: reserved for the framework's deserialization path
    ///     (<see cref="Origo.Core.Runtime.StateMachine.StateMachineContainer" />);
    ///     business code must use <see cref="Push" /> to modify the stack.
    /// </summary>
    internal void RestoreStackWithoutHooks(IReadOnlyList<string> stackBottomToTop);
}
