namespace Origo.Core.Abstractions.Entity;

public interface ISndObserverStrategyAccess
{
    void MountObserverStrategy(string targetName, string observerIndex);

    void UnmountObserverStrategy(string targetName, string observerIndex);

    void MountObserverStrategy(ISndEntity target, string observerIndex);

    void UnmountObserverStrategy(ISndEntity target, string observerIndex);
}
