namespace Origo.Core.Abstractions.Entity;

/// <summary>
///     Active strategy access interface. Provides dynamic add, remove, and
///     invoke capabilities for active strategies. Independent from
///     <see cref="ISndStrategyAccess" /> (passive entity strategies).
/// </summary>
public interface ISndActiveStrategyAccess
{
    /// <summary>Dynamically add an active strategy.</summary>
    void AddActiveStrategy(string index);

    /// <summary>Dynamically remove an active strategy.</summary>
    void RemoveActiveStrategy(string index);

    /// <summary>Invoke a strategy by index and get its return value. Throws if
    /// the index does not exist or is not an ActiveStrategyBase.</summary>
    object? InvokeStrategy(string strategyIndex, object? input = null);
}
