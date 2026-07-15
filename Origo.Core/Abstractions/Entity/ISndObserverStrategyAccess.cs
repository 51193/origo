namespace Origo.Core.Abstractions.Entity;

/// <summary>Observer strategy lifecycle operations on an SND entity.</summary>
public interface ISndObserverStrategyAccess
{
    /// <summary>Mount an observer strategy on a target entity by name.</summary>
    void MountObserverStrategy(string targetName, string observerIndex);

    /// <summary>Unmount an observer strategy from a target entity by name.</summary>
    void UnmountObserverStrategy(string targetName, string observerIndex);

    /// <summary>Mount an observer strategy on a target entity by reference.</summary>
    void MountObserverStrategy(ISndEntity target, string observerIndex);

    /// <summary>Unmount an observer strategy from a target entity by reference.</summary>
    void UnmountObserverStrategy(ISndEntity target, string observerIndex);
}
