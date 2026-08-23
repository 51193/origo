using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.Snd;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.DataSource;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save.Meta;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Tests;

/// <summary>
///     用于纯运行时单测场景的空上下文实现。
///     变更操作（存读档、关卡切换等）显式失败以满足 fail-fast 原则。
///     静态成员是空对象模式的正常设计——忽略 CA1822 警告。
/// </summary>
#pragma warning disable CA1822
#pragma warning disable IDE0060
public sealed class NullSndContext : ISndContext, ISndBlackboardAccess, ISndDeferredActions,
    ISndTemplateAccess, ISndConsoleAccess, ISndStateMachineAccess, ISndSaveOperations,
    ISndLifecycleOperations, IStateMachineContext
{
    public static readonly NullSndContext Instance = new();
    private static readonly IBlackboard _emptyBlackboard = new Blackboard.Blackboard();

    private NullSndContext()
    {
    }

    public void Bootstrap()
    {
    }

    public string SaveRootPath => string.Empty;
    public string InitialSaveRootPath => string.Empty;
    public string EntryConfigPath => string.Empty;

    public ISndBlackboardAccess Blackboard => this;
    public ISndDeferredActions Deferred => this;
    public ISndTemplateAccess Template => this;
    public ISndConsoleAccess ConsoleAccess => this;
    public ISndStateMachineAccess StateMachines => this;
    public ISndSaveOperations Save => this;
    public ISndLifecycleOperations Lifecycle => this;
    public ISndFileAccess FileAccess => NullFileAccess.Instance;
    public ISndArchiveFileAccess ArchiveFileAccess => NullArchiveFileAccess.Instance;
    public IStateMachineContext StateMachineContext => this;

    public IBlackboard SystemBlackboard => _emptyBlackboard;
    public IBlackboard? ProgressBlackboard => null;

    public void EnqueueBusinessDeferred(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
    }

    public void FlushDeferredActionsForCurrentFrame()
    {
    }

    public int GetPendingPersistenceRequestCount() => 0;

    public SndMetaData CloneTemplate(string templateKey, string? overrideName = null) =>
        throw new InvalidOperationException("NullSndContext does not support templates.");

    public IReadOnlyList<SndMetaData> ResolveMetaListFromJsonArray(DataSourceNode root) =>
        throw new InvalidOperationException("NullSndContext does not support templates.");

    public IReadOnlyList<SndMetaData> LoadMetaListFromFile(string filePath) =>
        throw new InvalidOperationException("NullSndContext does not support templates.");

    public void LoadTemplates(string mapFilePath) =>
        throw new InvalidOperationException("NullSndContext does not support templates.");

    public void LoadSceneAliases(string mapFilePath) =>
        throw new InvalidOperationException("NullSndContext does not support templates.");

    public bool TrySubmitConsoleCommand(string commandLine) => false;

    public long SubscribeConsoleOutput(Action<string> onLine) => 0;

    public void UnsubscribeConsoleOutput(long subscriptionId)
    {
    }

    public IStateMachineContainer? GetProgressStateMachines() => null;

    public IReadOnlyList<string> ListSaves() => [];

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

    ISndSceneReadAccess IStateMachineContext.SceneAccess =>
        throw new InvalidOperationException("NullSndContext does not support scene access.");

    IBlackboard? IStateMachineContext.SessionBlackboard => null;

    private sealed class NullFileAccess : ISndFileAccess
    {
        public static readonly NullFileAccess Instance = new();

        public DataSourceNode ReadFile(string path) =>
            throw new InvalidOperationException("NullSndContext does not support file access.");

        public void WriteFile(string path, DataSourceNode node, bool overwrite) =>
            throw new InvalidOperationException("NullSndContext does not support file access.");

        public bool FileExists(string path) => false;

        public T ReadObject<T>(string path) =>
            throw new InvalidOperationException("NullSndContext does not support file access.");

        public void WriteObject<T>(string path, T value, bool overwrite) =>
            throw new InvalidOperationException("NullSndContext does not support file access.");
    }

    private sealed class NullArchiveFileAccess : ISndArchiveFileAccess
    {
        public static readonly NullArchiveFileAccess Instance = new();

        public DataSourceNode ReadFile(string relativePath) =>
            throw new InvalidOperationException("NullSndContext does not support archive file access.");

        public void WriteFile(string relativePath, DataSourceNode node, bool overwrite) =>
            throw new InvalidOperationException("NullSndContext does not support archive file access.");

        public bool FileExists(string relativePath) => false;

        public T ReadObject<T>(string relativePath) =>
            throw new InvalidOperationException("NullSndContext does not support archive file access.");

        public void WriteObject<T>(string relativePath, T value, bool overwrite) =>
            throw new InvalidOperationException("NullSndContext does not support archive file access.");

        public void DeleteFile(string relativePath) =>
            throw new InvalidOperationException("NullSndContext does not support archive file access.");
    }
}
