namespace Origo.Core.Abstractions.Entity;

/// <summary>
///     主动策略的访问接口。提供动态添加、移除和主动调用主动策略的能力。
///     与 <see cref="ISndStrategyAccess" />（实体被动策略）独立。
/// </summary>
public interface ISndActiveStrategyAccess
{
    /// <summary>动态添加主动策略。</summary>
    void AddActiveStrategy(string index);

    /// <summary>动态移除主动策略。</summary>
    void RemoveActiveStrategy(string index);

    /// <summary>按索引主动调用策略并获取返回值。若 index 对应的策略不存在或不是 ActiveStrategyBase 则抛异常。</summary>
    object? InvokeStrategy(string strategyIndex, object? input = null);
}