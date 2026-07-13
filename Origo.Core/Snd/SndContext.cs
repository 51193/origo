using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Snd;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.DataSource;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Save;
using Origo.Core.Save.Meta;
using Origo.Core.Save.Storage;
using Origo.Core.Snd.Companions;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;

namespace Origo.Core.Snd;

public sealed class SndContext : ISndContext
{
    internal readonly SystemRun _systemRun;
    internal readonly List<ISaveMetaContributor> _saveMetaContributors = [];
    private readonly SndContextParameters _parameters;
    internal int _pendingPersistenceRequests;
    internal ProgressRun? _progressRun;
    private bool _workflowInProgress;

    public SndContext(SndContextParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        Runtime = parameters.Runtime;
        DataSourceIo = parameters.DataSourceIo;
        MetaAccess = parameters.MetaAccess;
        PathResolver = parameters.PathResolver;
        SaveRootPath = parameters.SaveRootPath;
        InitialSaveRootPath = parameters.InitialSaveRootPath;
        EntryConfigPath = parameters.EntryConfigPath;

        SavePathPolicy = parameters.SavePathPolicy ?? new DefaultSavePathPolicy();
        StorageService =
            parameters.StorageService ?? new DefaultSaveStorageService(MetaAccess, DataSourceIo, PathResolver, SaveRootPath, SavePathPolicy);
        InitialStorageService =
            parameters.InitialStorageService ??
            new DefaultSaveStorageService(MetaAccess, DataSourceIo, PathResolver, InitialSaveRootPath, SavePathPolicy);

        var systemParams = new SystemParameters(
            Runtime.Logger, MetaAccess, PathResolver, SaveRootPath, StorageService, SavePathPolicy,
            Runtime.GetAdapterSceneHost());
        var systemRuntime = new SystemRuntime(Runtime, systemParams);
        _systemRun = new SystemRun(systemRuntime);
        _parameters = parameters;

        Runtime.SetSessionManagerProvider(() => _progressRun?.SessionManager ?? EmptySessionManager.Instance);

        FileAccess = new SndContextFileAccess(DataSourceIo, MetaAccess, Runtime.SndWorld.ConverterRegistry);
        ArchiveFileAccess = new SndContextArchiveFileAccess(
            DataSourceIo, MetaAccess, Runtime.SndWorld.ConverterRegistry,
            PathResolver, SaveRootPath, SavePathPolicy);

        Blackboard = new SndContextBlackboardAccess(this);
        Deferred = new SndContextDeferredActions(this);
        Template = new SndContextTemplateAccess(this);
        ConsoleAccess = new SndContextConsoleAccess(this);
        StateMachines = new SndContextStateMachineAccess(this);
        Save = new SndContextSaveOperations(this);
        Lifecycle = new SndContextLifecycleOperations(this);
        StateMachineContext = new SndContextStateMachineContext(this);
    }

    public void Bootstrap()
    {
        _parameters.ConfigureConverters?.Invoke(Runtime.SndWorld.ConverterRegistry);

        if (_parameters.AutoDiscoverStrategies)
            OrigoAutoInitializer.DiscoverAndRegisterStrategies(
                Runtime.SndWorld, Runtime.Logger, _parameters.DiscoverySkipPrefixes);

        if (_parameters.SceneAliasMapPath is not null)
            Runtime.SndWorld.LoadSceneAliases(_parameters.SceneAliasMapPath, Runtime.Logger);

        if (_parameters.SndTemplateMapPath is not null)
            Runtime.SndWorld.LoadTemplates(_parameters.SndTemplateMapPath, Runtime.Logger);

        Lifecycle.RequestLoadMainMenuEntrySave();
    }

    internal OrigoRuntime Runtime { get; }
    internal IDataSourceIoGateway DataSourceIo { get; }
    internal IFileMetaAccess MetaAccess { get; }
    internal IPathResolver PathResolver { get; }
    public string SaveRootPath { get; }
    public string InitialSaveRootPath { get; }
    public string EntryConfigPath { get; }
    internal ISaveStorageService StorageService { get; }
    internal ISaveStorageService InitialStorageService { get; }
    internal ISavePathPolicy SavePathPolicy { get; }

    public ISndBlackboardAccess Blackboard { get; }
    public ISndDeferredActions Deferred { get; }
    public ISndTemplateAccess Template { get; }
    public ISndConsoleAccess ConsoleAccess { get; }
    public ISndStateMachineAccess StateMachines { get; }
    public ISndSaveOperations Save { get; }
    public ISndLifecycleOperations Lifecycle { get; }
    public ISndFileAccess FileAccess { get; }
    public ISndArchiveFileAccess ArchiveFileAccess { get; }
    public IStateMachineContext StateMachineContext { get; }

    internal ProgressRun EnsureProgressRun()
    {
        return _progressRun ?? throw new InvalidOperationException(
            "No active ProgressRun. Call RequestLoadMainMenuEntrySave first.");
    }

    internal void SetProgressRun(ProgressRun? progressRun) => _progressRun = progressRun;

    internal void BeginWorkflow()
    {
        if (_workflowInProgress)
            throw new InvalidOperationException(
                "A lifecycle workflow (load/save/change-level) is already in progress. " +
                "Concurrent workflow operations are not supported.");
        _workflowInProgress = true;
    }

    internal void EndWorkflow() => _workflowInProgress = false;

    internal void ShutdownCurrentProgressAndScene()
    {
        _progressRun?.Dispose();
        _progressRun = null;
    }

    internal void EnqueueSystemDeferred(Action action) => Runtime.EnqueueSystemDeferred(action);

    internal void EnqueueTrackedSystemDeferred(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Interlocked.Increment(ref _pendingPersistenceRequests);
        EnqueueSystemDeferred(() =>
        {
            try
            {
                action();
            }
            finally
            {
                Interlocked.Decrement(ref _pendingPersistenceRequests);
            }
        });
    }

    internal ProgressRun LoadOrContinueStrict(string saveId)
    {
        return RunWorkflow(() =>
        {
            using var progressNode = StorageService.ReadProgressNodeFromSnapshot(saveId) ?? throw new InvalidOperationException($"Missing required progress.json in save '{saveId}'.");
            var progressDict = Runtime.SndWorld.ReadTypedDataMap(progressNode);

            if (!progressDict.TryGetValue(WellKnownKeys.SessionTopology, out var topologyData)
                || !topologyData.TryGetString(out var rawTopology)
                || string.IsNullOrWhiteSpace(rawTopology))
                throw new InvalidOperationException(
                    $"Cannot determine foreground level from '{WellKnownKeys.SessionTopology}' in progress for save '{saveId}'.");

            var activeLevelId = SessionTopologyCodec.ExtractForegroundLevelId(rawTopology);

            var payload = StorageService.ReadSavePayloadFromSnapshot(saveId, activeLevelId);
            StorageService.DeleteCurrentDirectory();
            StorageService.WriteSavePayloadToCurrent(payload);
            StorageService.RestoreExtraFilesFromSnapshot(saveId);

            var progressRun = CreateProgressRun(saveId);
            SetProgressRun(progressRun);
            progressRun.LoadFromPayload(payload);
            _systemRun.SetActiveSaveSlot(saveId);
            return progressRun;
        });
    }

    internal void ExecuteLoadInitialSaveNow()
    {
        RunWorkflow(() =>
        {
            var payload = InitialStorageService.ReadSavePayloadFromSnapshot(
                SndDefaults.InitialSaveId,
                _parameters.InitialLevelId);
            payload.SaveId = SndDefaults.InitialSaveId;

            StorageService.DeleteCurrentDirectory();
            StorageService.WriteSavePayloadToCurrent(payload);
            StorageService.RestoreExtraFilesFromSnapshot(SndDefaults.InitialSaveId);

            var progressRun = CreateProgressRun(SndDefaults.InitialSaveId);
            SetProgressRun(progressRun);
            progressRun.LoadFromPayload(payload);
            _systemRun.SystemBlackboard.SetValue(WellKnownKeys.ActiveSaveId, string.Empty);
        });
    }

    internal void ExecuteLoadMainMenuEntrySaveNow()
    {
        RunWorkflow(() =>
        {
            StorageService.DeleteCurrentDirectory();

            var progressRun = CreateProgressRun(SndDefaults.InitialSaveId);
            SetProgressRun(progressRun);
            progressRun.LoadAndMountForeground(SndDefaults.MainMenuLevelId);

            OrigoAutoInitializer.LoadAndSpawnFromFile(
                EntryConfigPath,
                Runtime.SndWorld,
                progressRun.SessionManager.ForegroundSession!,
                DataSourceIo,
                Runtime.Logger);
        });
    }

    private ProgressRun CreateProgressRun(string saveId)
    {
        return new ProgressRun(
            _systemRun.Runtime,
            new ProgressParameters(saveId),
            StateMachineContext,
            this);
    }

    private void RunWorkflow(Action body)
    {
        BeginWorkflow();
        try
        {
            Runtime.ResetConsoleState();
            ShutdownCurrentProgressAndScene();
            body();
        }
        finally
        {
            EndWorkflow();
        }
    }

    private T RunWorkflow<T>(Func<T> body)
    {
        BeginWorkflow();
        try
        {
            Runtime.ResetConsoleState();
            ShutdownCurrentProgressAndScene();
            return body();
        }
        finally
        {
            EndWorkflow();
        }
    }
}
