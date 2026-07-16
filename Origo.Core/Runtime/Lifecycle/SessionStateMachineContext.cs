using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.StateMachine;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     Session-level state machine context adapter. Composes the global context (system/progress blackboard,
///     deferred queue) with the current session's blackboard and scene access, so that
///     <see cref="IStateMachineContext.SessionBlackboard" /> and <see cref="IStateMachineContext.SceneAccess" />
///     presented to each SessionRun's state machine hooks both point to the owning session,
///     providing consistent semantics across foreground and background sessions.
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
