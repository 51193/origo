using System;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;

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
    ///         entity name within the same owning session. Lookup, observer
    ///         topology, and save recovery all key on names, so name equality
    ///         within a session is the intended identity criterion.
    ///     </para>
    ///     <para>
    ///         When both entities have no owning session (for example
    ///         unbound test doubles or offline-built entities), the
    ///         comparison degenerates to name equality — containers that
    ///         produce unbound entities enforce unique names, so same-name
    ///         unbound entities denote the same entity in practice.
    ///     </para>
    /// </summary>
    public static bool IsSameEntityAs(this ISndEntity a, ISndEntity b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (ReferenceEquals(a, b))
            return true;

        if (!string.Equals(a.Name, b.Name, StringComparison.Ordinal))
            return false;

        static ISessionRun? SessionOf(ISndEntity e)
        {
            try
            {
                return e.OwningSession;
            }
            catch (InvalidOperationException)
            {
                // Concrete entities throw before session binding; treat an
                // unbound entity as having no owning session.
                return null;
            }
        }

        var aSession = SessionOf(a);
        var bSession = SessionOf(b);

        // Both unbound: degenerate to name equality (unbound containers
        // enforce unique names).
        if (aSession is null && bSession is null)
            return true;

        return ReferenceEquals(aSession, bSession);
    }
}
