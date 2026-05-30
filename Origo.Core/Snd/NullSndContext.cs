using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd;

/// <summary>
///     用于纯运行时单测场景的空上下文实现。
///     变更操作（存读档、关卡切换等）显式失败以满足 §2.1 显式失败优先。
/// </summary>
internal sealed class NullSndContext : ISndContext
{
    internal static readonly NullSndContext Instance = new();
    private static readonly IBlackboard EmptyBlackboard = new Blackboard.Blackboard();

    private NullSndContext()
    {
    }

    public IBlackboard SystemBlackboard => EmptyBlackboard;
    public IBlackboard? ProgressBlackboard => null;
    public ISessionManager SessionManager => EmptySessionManager.Instance;
    public ISessionRun? CurrentSession => null;
    public bool IsFrontSession => false;

    public void EnqueueBusinessDeferred(Action action) => action();

    public void FlushDeferredActionsForCurrentFrame()
    {
    }

    public int GetPendingPersistenceRequestCount() => 0;

    public SndMetaData CloneTemplate(string templateKey, string? overrideName = null) =>
        throw new InvalidOperationException("NullSndContext does not support templates.");

    public bool TrySubmitConsoleCommand(string commandLine) => false;

    public void ProcessConsolePending()
    {
    }

    public long SubscribeConsoleOutput(Action<string> onLine) => 0;

    public void UnsubscribeConsoleOutput(long subscriptionId)
    {
    }

    public StateMachineContainer? GetProgressStateMachines() => null;

    public IReadOnlyList<string> ListSaves() => Array.Empty<string>();

    public void RequestLoadGame(string saveId) =>
        throw new InvalidOperationException("NullSndContext does not support load operations.");

    public void RequestSaveGame(string newSaveId) =>
        throw new InvalidOperationException("NullSndContext does not support save operations.");

    public string RequestSaveGameAuto(string? newSaveId = null) =>
        throw new InvalidOperationException("NullSndContext does not support save operations.");

    public void SetContinueTarget(string saveId) =>
        throw new InvalidOperationException("NullSndContext does not support continue target operations.");

    public void RequestSwitchForegroundLevel(string newLevelId) =>
        throw new InvalidOperationException("NullSndContext does not support level switching.");

    public void RequestClearEntities()
    {
        throw new InvalidOperationException("NullSndContext does not support clear operations.");
    }

    public void RequestKillEntity(string entityName)
    {
        throw new InvalidOperationException("NullSndContext does not support entity operations.");
    }

    public bool HasContinueData() => false;

    public bool RequestContinueGame() => false;

    public void RequestLoadInitialSave() =>
        throw new InvalidOperationException("NullSndContext does not support load operations.");

    public void RequestLoadMainMenuEntrySave() =>
        throw new InvalidOperationException("NullSndContext does not support load operations.");
}
