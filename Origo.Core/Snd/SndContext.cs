using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Snd;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.DataSource;
using Origo.Core.Logging;
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

/// <summary>
///     Unified SND runtime context. Serves as the primary facade through which
///     strategies interact with the framework: blackboard access, deferred
///     actions, template resolution, console I/O, state machines, file I/O,
///     save/load operations, and lifecycle orchestration.
///     <para>
///         Owns the <see cref="SystemRun" /> for system-level state and
///         manages <see cref="ProgressRun" /> lifecycle transitions (create,
///         load, save, dispose). Exposes ten capability facets as companion
///         objects, each implementing a dedicated <c>ISnd*</c> interface.
///     </para>
/// </summary>
public sealed class SndContext : ISndContext
{
    internal readonly SystemRun _systemRun;
    internal readonly List<ISaveMetaContributor> _saveMetaContributors = [];
    private bool _bootstrapped;
    private readonly SndContextParameters _parameters;
    internal int _pendingPersistenceRequests;
    internal ProgressRun? _progressRun;
    private bool _workflowInProgress;

    /// <summary>
    ///     Constructs the SND context and initializes all companion objects,
    ///     the system runtime, and storage services.
    /// </summary>
    /// <param name="parameters">
    ///     Configuration parameters including runtime, data source I/O,
    ///     file metadata access, path resolver, save paths, storage services,
    ///     and optional bootstrap hooks.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown if <paramref name="parameters" /> is null.
    /// </exception>
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

    /// <summary>
    ///     Bootstrap the SND subsystem: register custom converters, auto-discover
    ///     strategies, load scene alias and template mappings, then invoke the
    ///     main menu entry workflow.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when Bootstrap is called more than once, or when the adapter
    ///     scene host is not fully wired (its observer topology context is not
    ///     bound) before the entry workflow is enqueued.
    /// </exception>
    public void Bootstrap()
    {
        if (_bootstrapped)
            throw new InvalidOperationException(
                "SndContext.Bootstrap has already been executed. Bootstrap must be called exactly once.");

        EnsureSceneHostReady();
        _bootstrapped = true;

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

    /// <summary>
    ///     Fails early when the adapter scene host is not ready for entity
    ///     creation. The main-menu entry workflow is enqueued as a deferred
    ///     operation, so without this check a misordered startup would surface
    ///     as a confusing failure at flush time instead of at Bootstrap time.
    /// </summary>
    private void EnsureSceneHostReady()
    {
        if (Runtime.GetAdapterSceneHost() is IObserverTopologyHost host
            && !host.ObserverTopology.IsContextBound)
            throw new InvalidOperationException(
                "The adapter scene host's observer topology is not bound to a context. " +
                "Bind the context to the scene host before calling SndContext.Bootstrap.");
    }

    // ── Infrastructure (internal) ──

    internal OrigoRuntime Runtime { get; }
    internal IDataSourceIoGateway DataSourceIo { get; }
    internal IFileMetaAccess MetaAccess { get; }
    internal IPathResolver PathResolver { get; }
    /// <summary>Root directory for runtime saves.</summary>
    public string SaveRootPath { get; }

    /// <summary>Root directory for the initial (res://) saves.</summary>
    public string InitialSaveRootPath { get; }

    /// <summary>Path to the entry config file (<c>entry.json</c>).</summary>
    public string EntryConfigPath { get; }
    internal ISaveStorageService StorageService { get; }
    internal ISaveStorageService InitialStorageService { get; }
    internal ISavePathPolicy SavePathPolicy { get; }

    // ── Capability facets (public, each delegates to a companion object) ──

    /// <summary>System and progress-level blackboard access.</summary>
    public ISndBlackboardAccess Blackboard { get; }
    /// <summary>Deferred action scheduling (business queue, frame flush, persistence tracking).</summary>
    public ISndDeferredActions Deferred { get; }
    /// <summary>Template cloning and metadata resolution.</summary>
    public ISndTemplateAccess Template { get; }
    /// <summary>Console command submission and output subscription.</summary>
    public ISndConsoleAccess ConsoleAccess { get; }
    /// <summary>Progress-level state machine container access.</summary>
    public ISndStateMachineAccess StateMachines { get; }
    /// <summary>Save game operations (list, load, save, auto-save, continue target).</summary>
    public ISndSaveOperations Save { get; }
    /// <summary>Lifecycle entry points (continue, initial save, main menu).</summary>
    public ISndLifecycleOperations Lifecycle { get; }
    /// <summary>File access for reading/writing DataSourceNode trees and typed objects.</summary>
    public ISndFileAccess FileAccess { get; }
    /// <summary>Save-archive-scoped file access with path traversal protection.</summary>
    public ISndArchiveFileAccess ArchiveFileAccess { get; }
    /// <summary>State machine context that composes system, progress, and session blackboards.</summary>
    public IStateMachineContext StateMachineContext { get; }

    /// <summary>
    ///     Returns the active <see cref="ProgressRun" />, throwing if none is active.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when no ProgressRun is active (call
    ///     <see cref="RequestLoadMainMenuEntrySave" /> first).
    /// </exception>
    internal ProgressRun EnsureProgressRun()
    {
        return _progressRun ?? throw new InvalidOperationException(
            "No active ProgressRun. Call RequestLoadMainMenuEntrySave first.");
    }

    /// <summary>Swap the active ProgressRun (used during lifecycle transitions).</summary>
    internal void SetProgressRun(ProgressRun? progressRun) => _progressRun = progressRun;

    /// <summary>
    ///     Enter a lifecycle workflow guard. Ensures only one workflow
    ///     (load/save/change-level) executes at a time.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if a workflow is already in progress.
    /// </exception>
    internal void BeginWorkflow()
    {
        if (_workflowInProgress)
            throw new InvalidOperationException(
                "A lifecycle workflow (load/save/change-level) is already in progress. " +
                "Concurrent workflow operations are not supported.");
        _workflowInProgress = true;
    }

    /// <summary>Exit the lifecycle workflow guard.</summary>
    internal void EndWorkflow() => _workflowInProgress = false;

    /// <summary>
    ///     Dispose the current ProgressRun and clear the reference.
    ///     Called at the start of every lifecycle workflow.
    /// </summary>
    internal void ShutdownCurrentProgressAndScene()
    {
        _progressRun?.Dispose();
        _progressRun = null;
    }

    /// <summary>Enqueue an action on the system deferred queue.</summary>
    internal void EnqueueSystemDeferred(Action action) => Runtime.EnqueueSystemDeferred(action);

    /// <summary>
    ///     Enqueue a system deferred action with a tracked persistence request
    ///     counter. The counter is incremented before execution and decremented
    ///     after (on both success and failure paths).
    /// </summary>
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

    /// <summary>
    ///     Load a save game from a snapshot or continue an existing save.
    ///     Reads the session topology to determine the foreground level,
    ///     restores the payload into the current directory, creates a
    ///     ProgressRun, and marks the active save slot.
    /// </summary>
    /// <returns>The recovered <see cref="ProgressRun" />.</returns>
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
            MountNewProgressRun(progressRun, () => progressRun.LoadFromPayload(payload));
            _systemRun.SetActiveSaveSlot(saveId);
            return progressRun;
        });
    }

    /// <summary>
    ///     Execute the initial-save workflow: load the initial level payload
    ///     from initial storage, create a ProgressRun, and clear the
    ///     active save slot.
    /// </summary>
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
            StorageService.RestoreExtraFilesFromSnapshot(
                InitialStorageService, SndDefaults.InitialSaveId);

            var progressRun = CreateProgressRun(SndDefaults.InitialSaveId);
            SetProgressRun(progressRun);
            MountNewProgressRun(progressRun, () => progressRun.LoadFromPayload(payload));
            _systemRun.SystemBlackboard.SetValue(WellKnownKeys.ActiveSaveId, string.Empty);
        });
    }

    /// <summary>
    ///     Execute the main-menu entry workflow: load the entry config JSON
    ///     (levels structure: <c>{ "levels": { "&lt;id&gt;": { "snd_scene":
    ///     "..." } }, "main_menu_level": "&lt;id&gt;" }</c>), resolve the
    ///     main-menu level's snd_scene file, and auto-initialize entities
    ///     from it into a fresh foreground session at the main menu level.
    /// </summary>
    internal void ExecuteLoadMainMenuEntrySaveNow()
    {
        RunWorkflow(() =>
        {
            StorageService.DeleteCurrentDirectory();

            var progressRun = CreateProgressRun(SndDefaults.InitialSaveId);
            SetProgressRun(progressRun);
            MountNewProgressRun(progressRun, () =>
            {
                progressRun.LoadAndMountForeground(SndDefaults.MainMenuLevelId);

                var sndScenePath = ResolveMainMenuSndScenePath();
                OrigoAutoInitializer.LoadAndSpawnFromFile(
                    sndScenePath,
                    Runtime.SndWorld,
                    progressRun.SessionManager.ForegroundSession!,
                    DataSourceIo,
                    Runtime.Logger);
            });
        });
    }

    /// <summary>
    ///     Mounts a freshly created progress run and, on failure, disposes it
    ///     and clears the context reference so no half-initialized progress
    ///     state remains reachable: reads of the progress blackboard and state
    ///     machines fail fast with "no active progress run" instead of exposing
    ///     partially deserialized data. Cleanup failures are logged and never
    ///     mask the original load exception.
    /// </summary>
    private void MountNewProgressRun(ProgressRun progressRun, Action mount)
    {
        ArgumentNullException.ThrowIfNull(progressRun);
        ArgumentNullException.ThrowIfNull(mount);

        try
        {
            mount();
        }
        catch
        {
            try
            {
                progressRun.Dispose();
            }
            catch (Exception disposeEx)
            {
                Runtime.Logger.Log(LogLevel.Warning, nameof(SndContext),
                    new LogMessageBuilder()
                        .AddContext("saveId", progressRun.SaveId)
                        .Build($"Progress run cleanup after failed mount failed: {disposeEx.Message}"));
            }
            finally
            {
                SetProgressRun(null);
            }

            throw;
        }
    }

    /// <summary>
    ///     Resolves the main-menu level's snd_scene file path from the
    ///     entry config. The config must use the levels structure; a bare
    ///     entity array is rejected with a clear error.
    /// </summary>
    private string ResolveMainMenuSndScenePath()
    {
        using var entry = DataSourceIo.ReadTree(EntryConfigPath);
        if (entry.Kind != DataSourceNodeKind.Map)
            throw new InvalidOperationException(
                $"Entry config '{EntryConfigPath}' must be a levels map " +
                $"({{ \"levels\": {{ \"<id>\": {{ \"snd_scene\": \"...\" }} }}, \"main_menu_level\": \"<id>\" }}), " +
                $"but found {entry.Kind}.");

        if (!entry.ContainsKey("main_menu_level"))
            throw new InvalidOperationException(
                $"Entry config '{EntryConfigPath}' is missing the 'main_menu_level' key.");

        var mainMenuLevel = entry["main_menu_level"].AsString();
        if (string.IsNullOrWhiteSpace(mainMenuLevel))
            throw new InvalidOperationException(
                $"Entry config '{EntryConfigPath}' has an empty 'main_menu_level'.");

        if (!entry.ContainsKey("levels") || !entry["levels"].ContainsKey(mainMenuLevel))
            throw new InvalidOperationException(
                $"Entry config '{EntryConfigPath}' does not define level '{mainMenuLevel}' under 'levels'.");

        var sndScenePath = entry["levels"][mainMenuLevel]["snd_scene"].AsString();
        if (string.IsNullOrWhiteSpace(sndScenePath))
            throw new InvalidOperationException(
                $"Entry config '{EntryConfigPath}' level '{mainMenuLevel}' has an empty 'snd_scene' path.");

        return sndScenePath;
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
        ArgumentNullException.ThrowIfNull(body);
        RunWorkflow(() =>
        {
            body();
            return 0;
        });
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
