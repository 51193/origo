using System;

namespace Origo.Core.Abstractions.Snd;

/// <summary>
///     Deferred action queue access.
///     Consumed by both <see cref="Origo.Core.Snd.ISndContext" /> and
///     <see cref="Origo.Core.Abstractions.StateMachine.IStateMachineContext" />.
/// </summary>
public interface ISndDeferredActions
{
    /// <summary>Enqueue a business-logic deferred action for execution at the end of the current frame.
    /// This is the recommended way to schedule side-effects from strategy hooks.</summary>
    void EnqueueBusinessDeferred(Action action);

    /// <summary>Execute all deferred actions for the current frame (business queue before system queue).
    /// Called once per frame by the engine adapter layer; strategies should not call this directly.</summary>
    void FlushDeferredActionsForCurrentFrame();

    /// <summary>Get the current count of pending persistence requests, useful for awaiting async save completion.</summary>
    int GetPendingPersistenceRequestCount();
}
