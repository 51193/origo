using System;

namespace Origo.Core.Abstractions.Snd;

/// <summary>
///     提供延迟动作队列的访问。
///     由 <see cref="Origo.Core.Snd.ISndContext" /> 和
///     <see cref="Origo.Core.Abstractions.StateMachine.IStateMachineContext" /> 共同消费。
/// </summary>
public interface ISndDeferredActions
{
    /// <summary>将业务逻辑延迟动作加入队列，在当前帧末尾统一执行。策略钩子中推荐使用此方法排队副作用。</summary>
    void EnqueueBusinessDeferred(Action action);

    /// <summary>执行当前帧的所有延迟动作（业务队列先于系统队列）。由引擎适配层每帧调用一次，策略不应直接调用。</summary>
    void FlushDeferredActionsForCurrentFrame();

    /// <summary>获取当前待执行的持久化请求计数，可用于等待异步存档完成。</summary>
    int GetPendingPersistenceRequestCount();
}