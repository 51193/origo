using Origo.Core.Abstractions.StateMachine;

namespace Origo.Core.Abstractions.Snd;

/// <summary>
///     Provides access to progress-level string-stack state machines.
/// </summary>
public interface ISndStateMachineAccess
{
    /// <summary>Progress-level string-stack state machine container; null when no progress run is active.</summary>
    IStateMachineContainer? GetProgressStateMachines();
}
