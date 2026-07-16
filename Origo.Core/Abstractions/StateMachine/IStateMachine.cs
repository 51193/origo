using System.Collections.Generic;

namespace Origo.Core.Abstractions.StateMachine;

/// <summary>
///     A string-stack state machine. The stack stores only strings;
///     Push/Pop semantics are implemented by strategy hooks.
/// </summary>
public interface IStateMachine
{
    string MachineKey { get; }

    string PushStrategyIndex { get; }

    string PopStrategyIndex { get; }

    void Push(string value);

    /// <summary>
    ///     Runtime pop: triggers the pop strategy's <c>BeforeRemove</c> semantics.
    /// </summary>
    bool TryPopRuntime(out string? popped);

    /// <summary>
    ///     Quit-time pop: triggers the pop strategy's <c>BeforeQuit</c> semantics.
    /// </summary>
    bool TryPopOnQuit(out string? popped);

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
    /// </summary>
    void RestoreStackWithoutHooks(IReadOnlyList<string> stackBottomToTop);
}
