using System;
using System.Collections.Generic;
using System.Diagnostics;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Logging;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Save;
using Origo.Core.Save.Serialization;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Origo.Core.Abstractions.Lifecycle;

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
    private const string LogTag = nameof(SessionRun);
    private readonly ILogger _logger;
    private readonly SaveContext _saveContext;
    private readonly ISndSceneHost _sceneHost;
    private readonly RunStateScope _sessionScope;
    private readonly ISaveStorageService _storageService;
    private bool _disposing;
    private bool _disposed;

    internal SessionRun(SessionManagerRuntime managerRuntime, SessionParameters sessionParams)
    {
        var watch = Stopwatch.StartNew();
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

        if (_sceneHost is ISndContextAttachableSceneHost contextAttachable)
            contextAttachable.BindContext(managerRuntime.SndContext);
        if (_sceneHost is ISessionScopedSceneHost scoped)
            scoped.SetOwningSession(this);
        _logger.Log(LogLevel.Info, LogTag,
            new LogMessageBuilder()
                .SetElapsedMs(watch.Elapsed.TotalMilliseconds)
                .Build($"Created SessionRun for level '{sessionParams.LevelId}'."));
    }

    internal RunStateScope SessionScope
    {
        get
        {
            ThrowIfDisposed();
            return _sessionScope;
        }
    }

    internal string? MountKey { get; set; }

    internal event Action? Disposing;

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

    public bool IsFrontSession { get; }

    public StateMachineContainer GetSessionStateMachines()
    {
        ThrowIfDisposed();
        return _sessionScope.StateMachines;
    }

    IStateMachineContainer ISessionRun.GetSessionStateMachines() => GetSessionStateMachines();

    public void Dispose()
    {
        if (_disposed || _disposing) return;
        _disposing = true;
        var watch = Stopwatch.StartNew();
        _logger.Log(LogLevel.Info, LogTag,
            $"Disposing SessionRun for level '{LevelId}' (mount key: {MountKey ?? "none"}).");

        Disposing?.Invoke();
        MountKey = null;

        _sessionScope.StateMachines.PopAllOnQuit();
        _sessionScope.StateMachines.Clear();

        try
        {
            ReleaseAllEntitiesAndClear(true);
        }
        finally
        {
            _sceneHost.RemoveAllEntities();
            _sessionScope.Blackboard.Clear();
            _disposed = true;
            _disposing = false;
            _logger.Log(LogLevel.Info, LogTag,
                new LogMessageBuilder()
                    .SetElapsedMs(watch.Elapsed.TotalMilliseconds)
                    .Build($"Disposed SessionRun for level '{LevelId}'."));
        }
    }

    internal LevelPayload SerializeToPayload()
    {
        ThrowIfDisposed();
        return BuildLevelPayload();
    }

    internal void LoadFromPayload(LevelPayload payload)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(payload);
        var watch = Stopwatch.StartNew();
        _logger.Log(LogLevel.Info, LogTag, $"Loading payload for level '{LevelId}'.");

        try
        {
            if (!payload.SessionNode.IsNull)
                _saveContext.DeserializeSession(payload.SessionNode);

            if (!payload.SessionStateMachinesNode.IsNull)
                _sessionScope.StateMachines.DeserializeFromNode(
                    payload.SessionStateMachinesNode,
                    _saveContext.SndWorld.ConverterRegistry);

            if (!payload.SndSceneNode.IsNull)
                _saveContext.RecoverSndScene(_sceneHost, payload.SndSceneNode);

            foreach (var entity in _sceneHost.GetEntities())
                if (entity is IEntityLifecycle lifecycle)
                    lifecycle.FireAfterLoadHooks();

            var observerTopology = (_sceneHost as IObserverTopologyHost)?.ObserverTopology;
            if (observerTopology is not null)
                foreach (var entity in _sceneHost.GetEntities())
                    if (entity is SndEntity se)
                    {
                        var meta = ((IEntityLifecycle)se).BuildMetaData();
                        var observerBindings = meta.StrategyMetaData?.ObserverIndices;
                        if (observerBindings is not null && observerBindings.Count > 0)
                            observerTopology.RecoverBindingsFor(se, observerBindings,
                                targetName => _sceneHost.FindByName(targetName));
                    }

            _sessionScope.StateMachines.FlushAllAfterLoad();

            _logger.Log(LogLevel.Info, LogTag,
                new LogMessageBuilder()
                    .SetElapsedMs(watch.Elapsed.TotalMilliseconds)
                    .Build($"Payload loaded for level '{LevelId}'."));
        }
        catch
        {
            ResetAfterLoadFailure();
            throw;
        }
    }

    internal void PersistLevelState()
    {
        ThrowIfDisposed();
        var watch = Stopwatch.StartNew();
        _logger.Log(LogLevel.Info, LogTag, $"Persisting level state for '{LevelId}'.");
        _storageService.WriteLevelPayloadOnlyToCurrent(BuildLevelPayload());
        _logger.Log(LogLevel.Info, LogTag,
            new LogMessageBuilder()
                .SetElapsedMs(watch.Elapsed.TotalMilliseconds)
                .Build($"Level state persisted for '{LevelId}'."));
    }

    /// <summary>
    ///     收割本会话场景中标记为待销毁的实体：先做观察者双向拆线，再触发 BeforeDead 钩子，最后物理移除。
    ///     由 <see cref="SessionManager.KillPendingAllSessions" /> 在帧末对每个会话统一调用。
    /// </summary>
    internal void KillPending()
    {
        ThrowIfDisposed();
        var entities = _sceneHost.GetEntities();
        var topology = (_sceneHost as IObserverTopologyHost)?.ObserverTopology;
        var pending = new List<ISndEntity>();
        foreach (var e in entities)
            if (e.IsPendingKill)
                pending.Add(e);

        if (pending.Count == 0)
            return;

        if (topology is not null)
        {
            foreach (var e in pending)
                if (e is SndEntity se)
                    TeardownOutgoingObserverBindings(se, entities, topology);

            foreach (var e in pending)
                foreach (var other in entities)
                    if (other is SndEntity otherSe && otherSe != e
                        && topology.HasBindingTargetingFrom(otherSe.Name, e.Name))
                        topology.RemoveBindingsTargetingFor(otherSe, e.Name);
        }

        foreach (var e in pending)
            if (e is IEntityLifecycle lifecycle)
                lifecycle.FireBeforeDeadHooks();

        foreach (var e in pending)
        {
            if (e is IEntityLifecycle lifecycle)
            {
                lifecycle.ReleaseStrategiesOnly();
                lifecycle.TeardownOnly();
            }

            _sceneHost.RemoveEntity(e.Name);
        }
    }

    private static void TeardownOutgoingObserverBindings(SndEntity entity,
        IReadOnlyCollection<ISndEntity> entities, ObserverTopology topology)
    {
        topology.TeardownOutgoingFor(entity, targetName =>
        {
            foreach (var other in entities)
                if (other.Name == targetName)
                    return other;
            return null;
        });
    }

    private LevelPayload BuildLevelPayload()
    {
        foreach (var entity in _sceneHost.GetEntities())
            if (entity is IEntityLifecycle lifecycle)
                lifecycle.FireBeforeSaveHooks();

        return new LevelPayload
        {
            LevelId = LevelId,
            SndSceneNode = _saveContext.BuildSndScene(_sceneHost),
            SessionNode = _saveContext.SerializeSession(),
            SessionStateMachinesNode =
                _sessionScope.StateMachines.SerializeToNode(_saveContext.SndWorld.ConverterRegistry)
        };
    }

    private void ResetAfterLoadFailure()
    {
        _sessionScope.StateMachines.Clear();
        ReleaseAllEntitiesAndClear(false);
        _sceneHost.RemoveAllEntities();
        _sessionScope.Blackboard.Clear();
    }

    private void ReleaseAllEntitiesAndClear(bool fireQuitHooks)
    {
        foreach (var entity in _sceneHost.GetEntities())
            if (entity is IEntityLifecycle lifecycle)
            {
                if (fireQuitHooks)
                    lifecycle.FireBeforeQuitHooks();
                lifecycle.ReleaseStrategiesOnly();
                lifecycle.TeardownOnly();
            }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
