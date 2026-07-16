namespace Origo.Core.Abstractions.Entity;

/// <summary>
///     Strategy management capability extracted from <see cref="ISndEntity" />,
///     following the interface segregation principle.
/// </summary>
public interface ISndStrategyAccess
{
    void AddStrategy(string index);

    void RemoveStrategy(string index);
}
