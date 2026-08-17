using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Runtime.Lifecycle;

namespace Origo.Core.Snd.Companions;

/// <summary>
///     State machine context that composes system, progress, and foreground
///     session blackboards and scene access for <see cref="SndContext" />.
/// </summary>
internal sealed class SndContextStateMachineContext(SndContext owner) : IStateMachineContext
{
    public IBlackboard SystemBlackboard => owner._systemRun.SystemBlackboard;

    public IBlackboard? ProgressBlackboard => owner._progressRun?.ProgressBlackboard;

    public IBlackboard? SessionBlackboard =>
        owner._progressRun?.SessionManager.ForegroundSession?.SessionBlackboard;

    public ISndSceneReadAccess SceneAccess =>
        owner._progressRun?.SessionManager.ForegroundSession is SessionRun fgSession
            ? fgSession.SceneHost
            : throw new InvalidOperationException(
                "SceneAccess unavailable without a foreground session.");

    public void EnqueueBusinessDeferred(Action action) =>
        owner.Runtime.EnqueueBusinessDeferred(action);

    public void FlushDeferredActionsForCurrentFrame() =>
        owner.Runtime.FlushEndOfFrameDeferred();

    public int GetPendingPersistenceRequestCount() =>
        System.Threading.Interlocked.CompareExchange(
            ref owner._pendingPersistenceRequests, 0, 0);
}
