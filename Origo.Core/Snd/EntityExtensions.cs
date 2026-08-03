using System;
using Origo.Core.Abstractions.Entity;

namespace Origo.Core.Snd;

/// <summary>
///     Extension methods for SND entity identity comparisons.
/// </summary>
public static class EntityExtensions
{
    /// <summary>
    ///     Determines whether two entity references denote the same entity.
    ///     <para>
    ///         Reference equality alone is unreliable: strategies receive the
    ///         inner <c>SndEntity</c> while <c>ISessionRun.GetEntities()</c>
    ///         may return adapter wrappers (e.g. <c>GodotSndEntity</c>) around
    ///         the same entity. The comparison therefore falls back to the
    ///         entity name within the same owning session. Names are unique
    ///         within a session by framework contract (lookup, observer
    ///         topology, and save recovery all key on names).
    ///     </para>
    ///     <para>
    ///         When both entities have no owning session (unbound stubs, e.g.
    ///         <c>StubSndEntity</c> created before session binding), the
    ///         comparison degenerates to name equality — containers that
    ///         produce unbound entities enforce unique names (see
    ///         <c>LevelBuilder</c>), so same-name unbound entities denote the
    ///         same entity in practice.
    ///     </para>
    /// </summary>
    public static bool IsSameEntityAs(this ISndEntity a, ISndEntity b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        return ReferenceEquals(a, b)
               || (string.Equals(a.Name, b.Name, StringComparison.Ordinal)
                   && ReferenceEquals(a.OwningSession, b.OwningSession));
    }
}
