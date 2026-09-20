namespace Origo.Core.Abstractions.Entity;

/// <summary>
///     Strategy management capability extracted from <see cref="ISndEntity" />,
///     following the interface segregation principle.
/// </summary>
public interface ISndStrategyAccess
{
    /// <summary>Mounts the passive strategy with the given index onto the entity, firing its <c>AfterAdd</c> hook.</summary>
    void AddStrategy(string index);

    /// <summary>Unmounts the passive strategy with the given index; throws when the index is not mounted.</summary>
    void RemoveStrategy(string index);
}
