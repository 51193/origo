using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.StateMachine;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     会话级状态机上下文适配器。将全局上下文（系统/流程黑板、延迟队列）
///     与当前会话的黑板和场景访问组合在一起，使每个 SessionRun 的状态机钩子
///     拿到的 <see cref="IStateMachineContext.SessionBlackboard" /> 和 <see cref="IStateMachineContext.SceneAccess" /> 都指向自身会话，
///     前后台会话语义一致。
/// </summary>
internal sealed class SessionStateMachineContext : IStateMachineContext
{
    private readonly IStateMachineContext _global;
    private readonly IBlackboard _sessionBlackboard;

    public SessionStateMachineContext(
        IStateMachineContext global,
        IBlackboard sessionBlackboard,
        ISndSceneAccess sceneAccess)
    {
        ArgumentNullException.ThrowIfNull(global);
        ArgumentNullException.ThrowIfNull(sessionBlackboard);
        ArgumentNullException.ThrowIfNull(sceneAccess);
        _global = global;
        _sessionBlackboard = sessionBlackboard;
        SceneAccess = sceneAccess;
    }

    public IBlackboard SystemBlackboard => _global.SystemBlackboard;

    public IBlackboard? ProgressBlackboard => _global.ProgressBlackboard;

    public IBlackboard? SessionBlackboard => _sessionBlackboard;

    public ISndSceneAccess SceneAccess { get; }

    public void EnqueueBusinessDeferred(Action action) => _global.EnqueueBusinessDeferred(action);

    public void FlushDeferredActionsForCurrentFrame() => _global.FlushDeferredActionsForCurrentFrame();

    public int GetPendingPersistenceRequestCount() => _global.GetPendingPersistenceRequestCount();
}
