using System;

namespace Origo.Core.Abstractions.Runtime;

/// <summary>
///     Abstract scheduling interface, driven by the host environment
///     for per-frame or per-cycle execution.
/// </summary>
internal interface IScheduler
{
    /// <summary>
    ///     Schedule an action for execution in the current frame or a
    ///     later phase; the specific policy is implementation-defined.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    void Enqueue(Action action);

    /// <summary>
    ///     Execute queued actions; returns the number of actions executed
    ///     this cycle.
    /// </summary>
    int Tick();

    /// <summary>
    ///     Clear the pending action queue without executing.
    /// </summary>
    void Clear();
}
