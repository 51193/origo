namespace Origo.Core.Abstractions.Entity;

/// <summary>
///     策略管理能力，从 <see cref="ISndEntity" /> 中拆分，遵循接口隔离原则。
/// </summary>
public interface ISndStrategyAccess
{
    void AddStrategy(string index);

    void RemoveStrategy(string index);
}
