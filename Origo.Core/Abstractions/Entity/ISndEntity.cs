using Origo.Core.Abstractions.Lifecycle;

namespace Origo.Core.Abstractions.Entity;

/// <summary>
///     Root interface for an SND entity, composing all per-entity role interfaces
///     (data, node, strategy, active strategy, observer strategy).
/// </summary>
public interface ISndEntity : ISndDataAccess, ISndNodeAccess, ISndStrategyAccess, ISndActiveStrategyAccess,
    ISndObserverStrategyAccess
{
    /// <summary>
    ///     Unique stable name of this entity within its session. Uniqueness is
    ///     enforced by spawn and load orchestration; lookup, observer
    ///     topology, save recovery, and
    ///     <see cref="Snd.EntityExtensions.IsSameEntityAs" /> key on the name.
    /// </summary>
    string Name { get; }

    /// <summary>Whether this entity has been marked for removal at the end of the current frame.</summary>
    bool IsPendingKill { get; }

    /// <summary>
    ///     The session this entity belongs to. Injected by the session host at creation
    ///     and never null during runtime. Strategies use this to access peer entities
    ///     and session-level capabilities.
    /// </summary>
    ISessionRun OwningSession { get; }
}
