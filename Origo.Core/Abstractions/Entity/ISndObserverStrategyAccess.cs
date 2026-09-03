namespace Origo.Core.Abstractions.Entity;

/// <summary>Observer strategy lifecycle operations on an SND entity.</summary>
public interface ISndObserverStrategyAccess
{
    /// <summary>
    ///     Mounts an observer strategy from this entity to itself.
    ///     <paramref name="targetName"/> must equal this entity's
    ///     <see cref="ISndEntity.Name"/>. Cross-entity mounts must resolve the
    ///     target through <c>OwningSession.FindByName</c> and use the
    ///     <see cref="MountObserverStrategy(ISndEntity, string)"/> overload.
    /// </summary>
    void MountObserverStrategy(string targetName, string observerIndex);

    /// <summary>
    ///     Unmounts an observer strategy from this entity itself.
    ///     <paramref name="targetName"/> must equal this entity's
    ///     <see cref="ISndEntity.Name"/>. Cross-entity unmounts must resolve
    ///     the target through <c>OwningSession.FindByName</c> and use the
    ///     <see cref="UnmountObserverStrategy(ISndEntity, string)"/> overload.
    /// </summary>
    void UnmountObserverStrategy(string targetName, string observerIndex);

    /// <summary>Mounts an observer strategy on an already resolved target entity.</summary>
    void MountObserverStrategy(ISndEntity target, string observerIndex);

    /// <summary>Unmounts an observer strategy from an already resolved target entity.</summary>
    void UnmountObserverStrategy(ISndEntity target, string observerIndex);
}
