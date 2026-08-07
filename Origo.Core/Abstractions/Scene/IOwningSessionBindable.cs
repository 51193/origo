using Origo.Core.Abstractions.Lifecycle;

namespace Origo.Core.Abstractions.Scene;

/// <summary>
///     Scene hosts can use this interface to bind the owning session at
///     session construction time, so that subsequent entity creation
///     automatically binds <see cref="ISndEntity.OwningSession" />.
/// </summary>
public interface IOwningSessionBindable
{
    /// <summary>Binds the session that owns this host.</summary>
    /// <param name="session">The owning session.</param>
    void SetOwningSession(ISessionRun session);
}
