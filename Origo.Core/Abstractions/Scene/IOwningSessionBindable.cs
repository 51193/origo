using Origo.Core.Abstractions.Lifecycle;

namespace Origo.Core.Abstractions.Scene;

/// <summary>
///     Scene hosts can use this interface to bind the owning session at
///     session construction time, so that subsequent entity creation
///     automatically binds <see cref="ISndEntity.OwningSession" />.
/// </summary>
public interface IOwningSessionBindable
{
    void SetOwningSession(ISessionRun session);
}
