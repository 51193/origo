namespace Origo.Core.Abstractions.Entity;

public interface ISndEntity : ISndDataAccess, ISndNodeAccess, ISndStrategyAccess, ISndActiveStrategyAccess,
    ISndObserverStrategyAccess
{
    string Name { get; }

    bool IsPendingKill { get; }
}
