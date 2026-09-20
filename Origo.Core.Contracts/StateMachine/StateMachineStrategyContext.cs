using System;

namespace Origo.Core.StateMachine;

/// <summary>
///     Stack context for a single state machine strategy callback: snapshots of the top of the stack
///     before and after a push, pop, or post-load flush.
/// </summary>
public readonly struct StateMachineStrategyContext
{
    /// <summary>
    ///     Creates a context capturing the stack top before and after an operation.
    /// </summary>
    public StateMachineStrategyContext(string machineKey, string? beforeTop, string? afterTop)
    {
        ArgumentNullException.ThrowIfNull(machineKey);
        MachineKey = machineKey;
        BeforeTop = beforeTop;
        AfterTop = afterTop;
    }

    /// <summary>The logical key of the state machine in the container.</summary>
    public string MachineKey { get; }

    /// <summary>The top of the stack before the operation; null when the stack is empty.</summary>
    public string? BeforeTop { get; }

    /// <summary>The top of the stack after the operation; null if the stack was emptied.</summary>
    public string? AfterTop { get; }
}
