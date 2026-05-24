using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Save;
using Origo.Core.Save.Serialization;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;
using Origo.Core.Snd.Scene;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     关卡会话级运行时实现，持有关卡会话黑板与 SND 场景访问。
///     构造时接收 <see cref="SessionManagerRuntime" /> 与 <see cref="SessionParameters" />。
///     <para>
///         能力边界：SessionRun 拥有对自身内部的完全支配权（黑板读写、场景操作、状态机），
///         但生命周期（创建 / 销毁 / 序列化）由 <see cref="SessionManager" /> 管理。
///         资源回收遵循 RAII 原则：SessionRun 在 Dispose 中回收自己的所有资源。
///     </para>
/// </summary>
public sealed class SessionRun : ISessionRun
{
    private const string LogTag = "SessionRun";
    private readonly ILogger _logger;
    private readonly SaveContext _saveContext;
    private readonly ISndSceneHost _sceneHost;
    private readonly SessionSndContext _sessionContext;
    private readonly RunStateScope _sessionScope;
    private readonly ISaveStorageService _storageService;
    private bool _disposed;

    internal SessionRun(SessionManagerRuntime managerRuntime, SessionParameters sessionParams)
    {
        ArgumentNullException.ThrowIfNull(managerRuntime);
        if (string.IsNullOrWhiteSpace(sessionParams.LevelId))
            throw new ArgumentException("Level id cannot be null or whitespace.");
        ArgumentNullException.ThrowIfNull(sessionParams.SceneHost);

        LevelId = sessionParams.LevelId;
        IsFrontSession = sessionParams.IsFrontSession;
        _sceneHost = sessionParams.SceneHost;
        _storageService = managerRuntime.StorageService;
        _logger = managerRuntime.Logger;

        var progressBb = managerRuntime.ProgressBlackboard;
        _saveContext = new SaveContext(progressBb, sessionParams.SessionBlackboard, managerRuntime.SndWorld);

        var sessionSmCtx = new SessionStateMachineContext(
            managerRuntime.StateMachineContext,
            sessionParams.SessionBlackboard,
            sessionParams.SceneHost);
        var sessionMachines = new StateMachineContainer(managerRuntime.SndWorld.StrategyPool, sessionSmCtx);
        _sessionScope = new RunStateScope(sessionParams.SessionBlackboard, sessionMachines);

        var effectiveContext = managerRuntime.SndContext;
        _sessionContext = new SessionSndContext(effectiveContext, this);
        if (_sceneHost is ISndContextAttachableSceneHost contextAttachable)
            contextAttachable.BindContext(_sessionContext);
        _logger.Log(LogLevel.Info, LogTag, $"Created SessionRun for level '{sessionParams.LevelId}'.");
    }

    /// <summary>
    ///     内部使用的完整 RunStateScope，包含黑板与状态机。
    ///     不在 <see cref="ISessionRun" /> 接口上暴露，仅供 ProgressRun / SessionManager 等内部代码使用。
    /// </summary>
    internal RunStateScope SessionScope
    {
        get
        {
            ThrowIfDisposed();
            return _sessionScope;
        }
    }

    /// <summary>
    ///     当前会话在 <see cref="ISessionManager" /> 中的挂载键；未挂载时为 null。
    ///     由 SessionManager 在创建/销毁时自动维护，外部不应直接设置。
    /// </summary>
    internal string? MountKey { get; set; }

    /// <summary>
    ///     在 SessionRun 被 Dispose 时需要自动从 SessionManager 卸载的回调。
    ///     由 SessionManager 在创建时注入。
    /// </summary>
    internal Action<SessionRun>? UnmountCallback { get; set; }

    public IBlackboard SessionBlackboard
    {
        get
        {
            ThrowIfDisposed();
            return _sessionScope.Blackboard;
        }
    }

    public ISndSceneHost SceneHost
    {
        get
        {
            ThrowIfDisposed();
            return _sceneHost;
        }
    }

    public string LevelId { get; }

    /// <inheritdoc />
    public bool IsFrontSession { get; }

    /// <inheritdoc />
    public StateMachineContainer GetSessionStateMachines()
    {
        ThrowIfDisposed();
        return _sessionScope.StateMachines;
    }

    public void Dispose()
    {
        if (_disposed) return;
        // Set flag first to prevent recursive Dispose calls (e.g. from cleanup callbacks).
        _disposed = true;
        _logger.Log(LogLevel.Info, LogTag,
            $"Disposing SessionRun for level '{LevelId}' (mount key: {MountKey ?? "none"}).");

        // Auto-persist before cleanup: save current session state so no runtime data is lost.
        try
        {
            PersistLevelStateInternal();
        }
        catch (Exception ex)
        {
            // Best-effort: if persistence fails (e.g. no storage configured), log warning and continue.
            _logger.Log(LogLevel.Warning, LogTag,
                $"Auto-persist failed during Dispose for level '{LevelId}': {ex.Message}");
        }

        // Auto-unmount from SessionManager if still mounted.
        UnmountCallback?.Invoke(this);
        MountKey = null;
        UnmountCallback = null;

        _sessionScope.StateMachines.PopAllOnQuit();
        _sessionScope.StateMachines.Clear();
        _sceneHost.ClearAll();
        _sessionScope.Blackboard.Clear();
    }

    /// <summary>
    ///     将当前会话状态序列化为 <see cref="LevelPayload" />（不执行任何磁盘 I/O）。
    ///     仅由 <see cref="SessionManager" /> 调用，不在 <see cref="ISessionRun" /> 接口暴露。
    /// </summary>
    internal LevelPayload SerializeToPayload()
    {
        ThrowIfDisposed();
        return BuildLevelPayload();
    }

    /// <summary>
    ///     从 <see cref="LevelPayload" /> 恢复会话状态（黑板、状态机、场景实体）。
    ///     仅由 <see cref="SessionManager" /> 调用，不在 <see cref="ISessionRun" /> 接口暴露。
    /// </summary>
    internal void LoadFromPayload(LevelPayload payload)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(payload);
        _logger.Log(LogLevel.Info, LogTag, $"Loading payload for level '{LevelId}'.");

        try
        {
            // 1. 恢复会话黑板
            if (!payload.SessionNode.IsNull)
                _saveContext.DeserializeSession(payload.SessionNode);

            // 2. 恢复状态机（不触发钩子，等场景加载完毕后统一 Flush）
            if (!payload.SessionStateMachinesNode.IsNull)
                _sessionScope.StateMachines.DeserializeFromNode(
                    payload.SessionStateMachinesNode,
                    _saveContext.SndWorld.ConverterRegistry);

            // 3. 恢复 SND 场景实体
            if (!payload.SndSceneNode.IsNull)
                _saveContext.DeserializeSndScene(_sceneHost, payload.SndSceneNode);

            // 4. 统一触发 AfterLoad 钩子
            _sessionScope.StateMachines.FlushAllAfterLoad();
        }
        catch
        {
            ResetAfterLoadFailure();
            throw;
        }
    }

    /// <summary>
    ///     将会话状态序列化并持久化到 current/ 目录。
    ///     仅由 <see cref="SessionManager" /> 调用，不在 <see cref="ISessionRun" /> 接口暴露。
    /// </summary>
    internal void PersistLevelState()
    {
        ThrowIfDisposed();
        _logger.Log(LogLevel.Info, LogTag, $"Persisting level state for '{LevelId}'.");
        _storageService.WriteLevelPayloadOnlyToCurrent(BuildLevelPayload());
    }

    /// <summary>
    ///     Internal persistence that does not check disposed flag (called from Dispose).
    /// </summary>
    private void PersistLevelStateInternal() => _storageService.WriteLevelPayloadOnlyToCurrent(BuildLevelPayload());

    private LevelPayload BuildLevelPayload()
    {
        return new LevelPayload
        {
            LevelId = LevelId,
            SndSceneNode = _saveContext.SerializeSndScene(_sceneHost),
            SessionNode = _saveContext.SerializeSession(),
            SessionStateMachinesNode =
                _sessionScope.StateMachines.SerializeToNode(_saveContext.SndWorld.ConverterRegistry)
        };
    }

    private void ResetAfterLoadFailure()
    {
        try
        {
            _sessionScope.StateMachines.Clear();
            _sceneHost.ClearAll();
            _sessionScope.Blackboard.Clear();
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Warning, LogTag,
                $"Failed to reset session state after load failure for level '{LevelId}': {ex.Message}");
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
