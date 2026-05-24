using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Save;
using Origo.Core.Save.Storage;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;

namespace Origo.Core.Snd;

/// <summary>
///     面向策略与游戏层的统一生命周期编排门面。
///     实现 <see cref="IStateMachineContext" /> 以便状态机钩子统一使用接口而非具体类型。
///     <para>
///         运行时分层：SystemRun → ProgressRun → SessionManager → SessionRun，
///         每层通过结构化参数构造，单向传递运行时能力。
///     </para>
/// </summary>
public sealed class SndContext : IStateMachineContext, ISndContext
{
    private readonly SystemRun _systemRun;
    private int _pendingPersistenceRequests;
    private ProgressRun? _progressRun;
    private bool _workflowInProgress;

    public SndContext(SndContextParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        Runtime = parameters.Runtime;
        FileSystem = parameters.FileSystem;
        SaveRootPath = parameters.SaveRootPath;
        InitialSaveRootPath = parameters.InitialSaveRootPath;
        EntryConfigPath = parameters.EntryConfigPath;

        SavePathPolicy = parameters.SavePathPolicy ?? new DefaultSavePathPolicy();
        StorageService =
            parameters.StorageService ?? new DefaultSaveStorageService(FileSystem, SaveRootPath, SavePathPolicy);
        InitialStorageService =
            parameters.InitialStorageService ??
            new DefaultSaveStorageService(FileSystem, InitialSaveRootPath, SavePathPolicy);

        var systemParams = new SystemParameters(
            Runtime.Logger, FileSystem, SaveRootPath, StorageService, SavePathPolicy);
        var systemRuntime = new SystemRuntime(Runtime, systemParams);
        _systemRun = new SystemRun(systemRuntime);
    }

    internal OrigoRuntime Runtime { get; }
    internal IFileSystem FileSystem { get; }
    public string SaveRootPath { get; }
    public string InitialSaveRootPath { get; }
    public string EntryConfigPath { get; }
    internal ISaveStorageService StorageService { get; }
    internal ISaveStorageService InitialStorageService { get; }
    internal ISavePathPolicy SavePathPolicy { get; }

    public SndRuntime SndRuntime => Runtime.Snd;

    public ISessionManager SessionManager => _progressRun?.SessionManager ?? EmptySessionManager.Instance;

    /// <inheritdoc />
    public ISessionRun? CurrentSession => SessionManager.ForegroundSession;

    /// <inheritdoc />
    public bool IsFrontSession => CurrentSession?.IsFrontSession ?? false;

    public int GetPendingPersistenceRequestCount() =>
        Interlocked.CompareExchange(ref _pendingPersistenceRequests, 0, 0);

    public void FlushDeferredActionsForCurrentFrame() => Runtime.FlushEndOfFrameDeferred();

    public SndMetaData CloneTemplate(string templateKey, string? overrideName = null)
    {
        var template = Runtime.SndWorld.ResolveTemplate(templateKey);
        var cloned = SndWorld.CloneMetaData(template);
        if (!string.IsNullOrWhiteSpace(overrideName))
            cloned.Name = overrideName;
        return cloned;
    }

    public bool TrySubmitConsoleCommand(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return false;

        Runtime.ConsoleInput?.Enqueue(commandLine.Trim());
        return Runtime.ConsoleInput is not null;
    }

    public void ProcessConsolePending() => Runtime.Console?.ProcessPending();

    public long SubscribeConsoleOutput(Action<string> onLine)
    {
        ArgumentNullException.ThrowIfNull(onLine);
        var channel = Runtime.ConsoleOutputChannel
                      ?? throw new InvalidOperationException("Console output channel is not available.");
        return channel.Subscribe(line => onLine(line ?? string.Empty));
    }

    public void UnsubscribeConsoleOutput(long subscriptionId)
    {
        if (subscriptionId <= 0)
            return;
        Runtime.ConsoleOutputChannel?.Unsubscribe(subscriptionId);
    }

    public StateMachineContainer? GetProgressStateMachines() => _progressRun?.GetProgressStateMachines();

    // ── Entry point ─────────────────────────────────────────────────────

    public IReadOnlyList<string> ListSaves() => StorageService.EnumerateSaveIds();

    public void RequestLoadGame(string saveId)
    {
        if (string.IsNullOrWhiteSpace(saveId))
            throw new ArgumentException("Save id cannot be null or whitespace.", nameof(saveId));

        EnqueueTrackedSystemDeferred(() => SetProgressRun(LoadOrContinueStrict(saveId)));
    }

    public void RequestSaveGame(string newSaveId)
    {
        if (string.IsNullOrWhiteSpace(newSaveId))
            throw new ArgumentException("New save id cannot be null or whitespace.", nameof(newSaveId));

        EnqueueTrackedSystemDeferred(() =>
        {
            BeginWorkflow();
            try
            {
                var progressRun = EnsureProgressRun();
                var payload = progressRun.BuildSavePayload(newSaveId);
                StorageService.WriteSavePayloadToCurrentThenSnapshot(payload, newSaveId, Runtime.Logger);
                progressRun.SetSaveId(newSaveId);
                _systemRun.SetActiveSaveSlot(newSaveId);
            }
            finally
            {
                EndWorkflow();
            }
        });
    }

    public string RequestSaveGameAuto(string? newSaveId = null)
    {
        var effectiveNewSaveId = string.IsNullOrWhiteSpace(newSaveId)
            ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)
            : newSaveId;
        RequestSaveGame(effectiveNewSaveId);
        return effectiveNewSaveId;
    }

    public void SetContinueTarget(string saveId) => _systemRun.SetActiveSaveSlot(saveId);

    public void RequestSwitchForegroundLevel(string newLevelId)
    {
        if (string.IsNullOrWhiteSpace(newLevelId))
            throw new ArgumentException("New level id cannot be null or whitespace.", nameof(newLevelId));
        EnqueueBusinessDeferred(() => { EnsureProgressRun().SwitchForeground(newLevelId); });
    }

    public bool HasContinueData()
    {
        var (found, saveId) = SystemBlackboard.TryGet<string>(WellKnownKeys.ActiveSaveId);
        return found && !string.IsNullOrWhiteSpace(saveId);
    }

    public bool RequestContinueGame()
    {
        var (found, saveId) = SystemBlackboard.TryGet<string>(WellKnownKeys.ActiveSaveId);
        if (!found || string.IsNullOrWhiteSpace(saveId))
            return false;

        EnqueueSystemDeferred(() => { SetProgressRun(LoadOrContinueStrict(saveId)); });
        return true;
    }

    public void RequestLoadInitialSave() => EnqueueSystemDeferred(ExecuteLoadInitialSaveNow);

    public void RequestLoadMainMenuEntrySave() => EnqueueSystemDeferred(ExecuteLoadMainMenuEntrySaveNow);

    public IBlackboard SystemBlackboard => _systemRun.SystemBlackboard;
    public IBlackboard? ProgressBlackboard => _progressRun?.ProgressBlackboard;

    ISndSceneAccess IStateMachineContext.SceneAccess => Runtime.Snd.SceneHost;

    IBlackboard? IStateMachineContext.SessionBlackboard => SessionManager.ForegroundSession?.SessionBlackboard;

    // ── Public API ─────────────────────────────────────────────────────

    public void EnqueueBusinessDeferred(Action action) => Runtime.EnqueueBusinessDeferred(action);

    // ── Internal helpers ──────────────────────────────────────────────

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
        Runtime.Snd.ClearAll();
    }

    private ProgressRun CreateProgressRun(string saveId)
    {
        return new ProgressRun(
            _systemRun.Runtime,
            new ProgressParameters(saveId),
            this,
            this);
    }

    internal void EnqueueSystemDeferred(Action action) => Runtime.EnqueueSystemDeferred(action);

    private void EnqueueTrackedSystemDeferred(Action action)
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

    private ProgressRun LoadOrContinueStrict(string saveId)
    {
        return RunWorkflow(() =>
        {
            using var progressNode = StorageService.ReadProgressNodeFromSnapshot(saveId);
            if (progressNode is null)
                throw new InvalidOperationException($"Missing required progress.json in save '{saveId}'.");
            var progressDict = Runtime.SndWorld.ReadTypedDataMap(progressNode);

            if (!progressDict.TryGetValue(WellKnownKeys.SessionTopology, out var topologyData)
                || topologyData.Data is not string rawTopology
                || string.IsNullOrWhiteSpace(rawTopology))
                throw new InvalidOperationException(
                    $"Cannot determine foreground level from '{WellKnownKeys.SessionTopology}' in progress for save '{saveId}'.");

            var activeLevelId = SessionTopologyCodec.ExtractForegroundLevelId(rawTopology);

            var payload = StorageService.ReadSavePayloadFromSnapshot(saveId, activeLevelId);
            StorageService.DeleteCurrentDirectory();
            StorageService.WriteSavePayloadToCurrent(payload);

            var progressRun = CreateProgressRun(saveId);
            SetProgressRun(progressRun);
            progressRun.LoadFromPayload(payload);
            _systemRun.SetActiveSaveSlot(saveId);
            return progressRun;
        });
    }

    private void ExecuteLoadInitialSaveNow()
    {
        RunWorkflow(() =>
        {
            var payload = InitialStorageService.ReadSavePayloadFromSnapshot(
                SndDefaults.InitialSaveId,
                SndDefaults.InitialLevelId);
            payload.SaveId = SndDefaults.InitialSaveId;

            StorageService.DeleteCurrentDirectory();
            StorageService.WriteSavePayloadToCurrent(payload);

            var progressRun = CreateProgressRun(SndDefaults.InitialSaveId);
            SetProgressRun(progressRun);
            progressRun.LoadFromPayload(payload);
            SystemBlackboard.Set(WellKnownKeys.ActiveSaveId, string.Empty);
        });
    }

    private void ExecuteLoadMainMenuEntrySaveNow()
    {
        RunWorkflow(() =>
        {
            var progressRun = CreateProgressRun(SndDefaults.InitialSaveId);
            SetProgressRun(progressRun);
            progressRun.LoadAndMountForeground(SndDefaults.MainMenuLevelId);

            OrigoAutoInitializer.LoadAndSpawnFromFile(
                EntryConfigPath,
                Runtime.Snd,
                FileSystem,
                Runtime.Logger);
        });
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
