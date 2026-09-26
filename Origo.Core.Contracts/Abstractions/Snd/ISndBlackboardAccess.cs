using Origo.Core.Abstractions.Blackboard;

namespace Origo.Core.Abstractions.Snd;

/// <summary>
///     Access to the system-level and progress-level blackboards. The
///     returned <see cref="IBlackboard" /> instances are mutable; "access"
///     here refers to the capability facet, not a read-only view.
///     Consumed by both <see cref="Origo.Core.Snd.ISndContext" /> and
///     <see cref="Origo.Core.Abstractions.StateMachine.IStateMachineContext" />.
/// </summary>
public interface ISndBlackboardAccess
{
    /// <summary>System-level blackboard, whose lifetime matches the process.</summary>
    IBlackboard SystemBlackboard { get; }

    /// <summary>Current progress-level blackboard (save-slot scope); null when no progress run is active.</summary>
    IBlackboard? ProgressBlackboard { get; }
}
