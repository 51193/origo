using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Snd;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.DataSource;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save.Meta;
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

    public IStateMachineContainer? GetProgressStateMachines() => null;

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

    public bool HasContinueData() => false;

    public bool RequestContinueGame() => false;

    public void RequestLoadInitialSave() =>
        throw new InvalidOperationException("NullSndContext does not support load operations.");

    public void RequestLoadMainMenuEntrySave() =>
        throw new InvalidOperationException("NullSndContext does not support load operations.");

    public void RegisterSaveMetaContributor(ISaveMetaContributor contributor) =>
        throw new InvalidOperationException("NullSndContext does not support save meta registration.");

    public void RegisterSaveMetaContributor(Func<SaveMetaBuildContext, IReadOnlyDictionary<string, string>> contribute) =>
        throw new InvalidOperationException("NullSndContext does not support save meta registration.");

    DataSourceNode ISndFileAccess.ReadFile(string path) =>
        throw new InvalidOperationException("NullSndContext does not support file access.");

    void ISndFileAccess.WriteFile(string path, DataSourceNode node, bool overwrite) =>
        throw new InvalidOperationException("NullSndContext does not support file access.");

    bool ISndFileAccess.FileExists(string path) => false;

    T ISndFileAccess.ReadObject<T>(string path) =>
        throw new InvalidOperationException("NullSndContext does not support file access.");

    void ISndFileAccess.WriteObject<T>(string path, T value, bool overwrite) =>
        throw new InvalidOperationException("NullSndContext does not support file access.");

    DataSourceNode ISndArchiveFileAccess.ReadFile(string relativePath) =>
        throw new InvalidOperationException("NullSndContext does not support archive file access.");

    void ISndArchiveFileAccess.WriteFile(string relativePath, DataSourceNode node, bool overwrite) =>
        throw new InvalidOperationException("NullSndContext does not support archive file access.");

    bool ISndArchiveFileAccess.FileExists(string relativePath) => false;

    T ISndArchiveFileAccess.ReadObject<T>(string relativePath) =>
        throw new InvalidOperationException("NullSndContext does not support archive file access.");

    void ISndArchiveFileAccess.WriteObject<T>(string relativePath, T value, bool overwrite) =>
        throw new InvalidOperationException("NullSndContext does not support archive file access.");

    void ISndArchiveFileAccess.DeleteFile(string relativePath) =>
        throw new InvalidOperationException("NullSndContext does not support archive file access.");
}
