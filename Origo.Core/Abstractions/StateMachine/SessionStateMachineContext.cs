using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Scene;

namespace Origo.Core.Abstractions.StateMachine;

/// <summary>
///     会话级状态机上下文适配器。将全局上下文（系统/流程黑板、延迟队列）
///     与当前会话的黑板和场景访问组合在一起，使每个 SessionRun 的状态机钩子
///     拿到的 <see cref="SessionBlackboard" /> 和 <see cref="SceneAccess" /> 都指向自身会话，
///     前后台会话不再有语义分差。
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

    /// <inheritdoc />
    public IBlackboard SystemBlackboard => _global.SystemBlackboard;

    /// <inheritdoc />
    public IBlackboard? ProgressBlackboard => _global.ProgressBlackboard;

    /// <inheritdoc />
    public IBlackboard? SessionBlackboard => _sessionBlackboard;

    /// <inheritdoc />
    public ISndSceneAccess SceneAccess { get; }

    /// <inheritdoc />
    public void EnqueueBusinessDeferred(Action action) => _global.EnqueueBusinessDeferred(action);
}
