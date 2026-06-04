namespace Origo.Core.Abstractions.StateMachine;

/// <summary>
///     管理多个 <see cref="IStateMachine" /> 的容器，按 key 创建/查找/移除。
///     策略层通过此接口创建会话级状态机，不依赖具体容器实现。
/// </summary>
public interface IStateMachineContainer
{
    /// <summary>
    ///     按 key 创建或获取一个状态机。若 key 已存在但策略索引不同则抛异常。
    /// </summary>
    IStateMachine CreateOrGet(string machineKey, string pushStrategyIndex, string popStrategyIndex);

    /// <summary>按 key 查找已有状态机。</summary>
    bool TryGet(string machineKey, out IStateMachine? machine);

    /// <summary>按 key 移除并释放状态机。</summary>
    void Remove(string machineKey);

    /// <summary>释放所有状态机并清空容器。</summary>
    void Clear();
}
