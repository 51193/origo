using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd;

/// <summary>
///     会话级上下文：复用全局能力，但将 CurrentSession 绑定到当前 SessionRun。
///     用于实体策略执行，确保策略能获知“自己属于哪个会话”。
/// </summary>
internal sealed class SessionSndContext : ISndContext
{
    private readonly ISndContext _global;

    internal SessionSndContext(ISndContext global, ISessionRun currentSession)
    {
        ArgumentNullException.ThrowIfNull(global);
        ArgumentNullException.ThrowIfNull(currentSession);
        _global = global;
        CurrentSession = currentSession;
    }

    public IBlackboard SystemBlackboard => _global.SystemBlackboard;
    public IBlackboard? ProgressBlackboard => _global.ProgressBlackboard;
    public ISessionManager SessionManager => _global.SessionManager;
    public ISessionRun? CurrentSession { get; }
    public bool IsFrontSession => CurrentSession?.IsFrontSession ?? false;

    public void EnqueueBusinessDeferred(Action action) => _global.EnqueueBusinessDeferred(action);

    public void FlushDeferredActionsForCurrentFrame() => _global.FlushDeferredActionsForCurrentFrame();

    public int GetPendingPersistenceRequestCount() => _global.GetPendingPersistenceRequestCount();

    public SndMetaData CloneTemplate(string templateKey, string? overrideName = null) =>
        _global.CloneTemplate(templateKey, overrideName);

    public bool TrySubmitConsoleCommand(string commandLine) => _global.TrySubmitConsoleCommand(commandLine);

    public void ProcessConsolePending() => _global.ProcessConsolePending();

    public long SubscribeConsoleOutput(Action<string> onLine) => _global.SubscribeConsoleOutput(onLine);

    public void UnsubscribeConsoleOutput(long subscriptionId) => _global.UnsubscribeConsoleOutput(subscriptionId);

    public StateMachineContainer? GetProgressStateMachines() => _global.GetProgressStateMachines();

    public IReadOnlyList<string> ListSaves() => _global.ListSaves();

    public void RequestLoadGame(string saveId) => _global.RequestLoadGame(saveId);

    public void RequestSaveGame(string newSaveId) => _global.RequestSaveGame(newSaveId);

    public string RequestSaveGameAuto(string? newSaveId = null) => _global.RequestSaveGameAuto(newSaveId);

    public void SetContinueTarget(string saveId) => _global.SetContinueTarget(saveId);

    public void RequestSwitchForegroundLevel(string newLevelId) => _global.RequestSwitchForegroundLevel(newLevelId);

    public void RequestClearEntities()
    {
        _global.RequestClearEntities();
    }

    public void RequestKillEntity(string entityName)
    {
        _global.RequestKillEntity(entityName);
    }

    public bool HasContinueData() => _global.HasContinueData();

    public bool RequestContinueGame() => _global.RequestContinueGame();

    public void RequestLoadInitialSave() => _global.RequestLoadInitialSave();

    public void RequestLoadMainMenuEntrySave() => _global.RequestLoadMainMenuEntrySave();
}
