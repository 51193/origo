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
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Origo.Core.Abstractions.Lifecycle;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     Level-session-level runtime implementation, holding the level session blackboard and SND scene access.
///     Receives <see cref="SessionManagerRuntime" /> and <see cref="SessionParameters" /> at construction.
///     <para>
///         Capability boundary: SessionRun has full control over its own internals (blackboard read/write,
///         scene operations, state machines), but lifecycle (creation / destruction / serialization) is
///         managed by <see cref="SessionManager" />. Resource reclamation follows the RAII principle:
///         SessionRun reclaims all its own resources in Dispose.
///     </para>
/// </summary>
internal sealed class SessionRun : ISessionRun
{
    private const string _logTag = nameof(SessionRun);
    private readonly ISessionManager _sessionManager;
    private readonly ILogger _logger;
    private readonly SaveContext _saveContext;
    private readonly ISndSceneHost _sceneHost;
    private readonly RunStateScope _sessionScope;
    private readonly ISaveStorageService _storageService;
    private bool _disposing;
    private bool _disposed;

    internal SessionRun(SessionManagerRuntime managerRuntime, SessionParameters sessionParams,
        ISessionManager sessionManager)
    {
        var watch = Stopwatch.StartNew();
        ArgumentNullException.ThrowIfNull(managerRuntime);
        ArgumentNullException.ThrowIfNull(sessionManager);
        if (string.IsNullOrWhiteSpace(sessionParams.LevelId))
            throw new ArgumentException("Level id cannot be null or whitespace.");
        ArgumentNullException.ThrowIfNull(sessionParams.SceneHost);

        LevelId = sessionParams.LevelId;
        IsFrontSession = sessionParams.IsFrontSession;
        _sceneHost = sessionParams.SceneHost;
        _sessionManager = sessionManager;
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
        if (_sceneHost is IOwningSessionBindable bindable)
            bindable.SetOwningSession(this);
        _logger.Log(LogLevel.Info, _logTag,
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

    /// <summary>
    ///     Internal scene host. For framework-level orchestration only
    ///     (<c>SaveContext</c> / <c>SessionManager</c> / <c>SndSceneSerializer</c>);
    ///     not visible to strategies.
    /// </summary>
    internal ISndSceneHost SceneHost => _sceneHost;

    public string LevelId { get; }

    public bool IsFrontSession { get; }

    public ISessionManager SessionManager => _sessionManager;

    public ISndEntity? FindByName(string name)
    {
        ThrowIfDisposed();
        return _sceneHost.FindByName(name);
    }

    public IReadOnlyCollection<ISndEntity> GetEntities()
    {
        ThrowIfDisposed();
        return _sceneHost.GetEntities();
    }

    public ISndEntity Spawn(SndMetaData meta)
    {
        ThrowIfDisposed();
        return SndEntityFactory.Spawn(_sceneHost, meta);
    }

    public void SpawnMany(params SndMetaData[] metaList)
    {
        ThrowIfDisposed();
        SndEntityFactory.SpawnMany(_sceneHost, metaList);
    }

    public void RequestKillEntity(string entityName)
    {
        ThrowIfDisposed();
        _sceneHost.RequestKillEntity(entityName);
    }

    internal StateMachineContainer GetSessionStateMachines()
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
        _logger.Log(LogLevel.Info, _logTag,
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
            _logger.Log(LogLevel.Info, _logTag,
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
        _logger.Log(LogLevel.Info, _logTag, $"Loading payload for level '{LevelId}'.");

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

            _logger.Log(LogLevel.Info, _logTag,
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
        _logger.Log(LogLevel.Info, _logTag, $"Persisting level state for '{LevelId}'.");
        _storageService.WriteLevelPayloadOnlyToCurrent(BuildLevelPayload());
        _logger.Log(LogLevel.Info, _logTag,
            new LogMessageBuilder()
                .SetElapsedMs(watch.Elapsed.TotalMilliseconds)
                .Build($"Level state persisted for '{LevelId}'."));
    }

    /// <summary>
    ///     Harvests entities in this session's scene that are marked as pending kill:
    ///     first tears down observer bindings bidirectionally, then fires <c>BeforeDead</c> hooks,
    ///     and finally removes them physically.
    ///     Called by <see cref="SessionManager.KillPendingAllSessions" /> at end of frame for each session.
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

    /// <summary>
    ///     Fires BeforeSave hooks on all entities in this session's scene,
    ///     giving strategies a final chance to flush in-memory state into
    ///     entity Data before serialization.
    /// </summary>
    internal void FireBeforeSaveHooks()
    {
        ThrowIfDisposed();
        foreach (var entity in _sceneHost.GetEntities())
            if (entity is IEntityLifecycle lifecycle)
                lifecycle.FireBeforeSaveHooks();
    }

    private LevelPayload BuildLevelPayload()
    {
        FireBeforeSaveHooks();

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
