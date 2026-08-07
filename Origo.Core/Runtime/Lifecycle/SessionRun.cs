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

        try
        {
            // The disposing notification and the session-scoped state
            // machines' quit-pop hooks can throw (exceptions propagate to the
            // caller per the fail-fast contract); the session state machines,
            // entity strategies, scene container, and blackboard are still
            // guaranteed to be released and the disposed flag committed via
            // the nested finally blocks (matching ProgressRun.Dispose).
            Disposing?.Invoke();
            MountKey = null;
        }
        finally
        {
            try
            {
                _sessionScope.StateMachines.PopAllOnQuit();
            }
            finally
            {
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

            IReadOnlyList<SndMetaData>? recoveredSceneMeta = null;
            if (!payload.SndSceneNode.IsNull)
                recoveredSceneMeta = _saveContext.RecoverSndScene(_sceneHost, payload.SndSceneNode);

            // Snapshot the collection before firing hooks: AfterLoad hooks may
            // spawn new entities, and hosts expose a live entity view (the
            // Godot adapter returns the backing list).
            List<ISndEntity> loadedEntities = [.. _sceneHost.GetEntities()];
            foreach (var entity in loadedEntities)
                if (entity is IEntityLifecycle lifecycle)
                    lifecycle.FireAfterLoadHooks();

            var observerTopology = (_sceneHost as IObserverTopologyHost)?.ObserverTopology;
            if (observerTopology is not null && recoveredSceneMeta is not null)
                foreach (var meta in recoveredSceneMeta)
                {
                    var observerBindings = meta.StrategyMetaData?.ObserverIndices;
                    if (observerBindings is null || observerBindings.Count == 0)
                        continue;
                    var entity = _sceneHost.FindByName(meta.Name)
                        ?? throw new InvalidOperationException(
                            $"Observer binding recovery references entity '{meta.Name}', " +
                            "but no entity with that name exists in the recovered scene. " +
                            "The save topology is inconsistent and cannot be recovered.");
                    observerTopology.RecoverBindingsFor(entity, observerBindings,
                        targetName => _sceneHost.FindByName(targetName));
                }

            _sessionScope.StateMachines.FlushAllAfterLoad();

            _logger.Log(LogLevel.Info, _logTag,
                new LogMessageBuilder()
                    .SetElapsedMs(watch.Elapsed.TotalMilliseconds)
                    .Build($"Payload loaded for level '{LevelId}'."));
        }
        catch (Exception ex)
        {
            // Record the original load failure before cleanup: cleanup steps
            // (ResetAfterLoadFailure) can throw on their own and would
            // otherwise mask the exception that triggered them.
            _logger.Log(LogLevel.Error, _logTag,
                new LogMessageBuilder()
                    .AddContext("levelId", LevelId)
                    .Build($"Payload load failed: {ex.Message}"));
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
            // Resolve entity names to instances once; the same dictionary
            // serves outgoing target resolution and incoming observer lookup.
            var entitiesByName = new Dictionary<string, ISndEntity>(entities.Count, StringComparer.Ordinal);
            foreach (var other in entities)
                entitiesByName[other.Name] = other;

            // Outgoing teardown: every pending entity's own observer bindings
            // are unmounted (unsubscribe + OnUnmounted + pool release),
            // regardless of whether the entity is a bare SndEntity or an
            // adapter wrapper implementing ISndEntityRawSubscription.
            foreach (var e in pending)
                TeardownOutgoingObserverBindings(e, entitiesByName, topology);

            // Incoming teardown via the topology's O(1) incoming index: a
            // dying entity may be the target of other entities' bindings.
            foreach (var e in pending)
                foreach (var observerName in topology.GetObserverNamesTargeting(e.Name))
                    if (entitiesByName.TryGetValue(observerName, out var observer))
                        topology.RemoveBindingsTargetingFor(observer, e.Name);
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

    private static void TeardownOutgoingObserverBindings(ISndEntity entity,
        Dictionary<string, ISndEntity> entitiesByName, ObserverTopology topology)
    {
        topology.TeardownOutgoingFor(entity, targetName =>
            entitiesByName.TryGetValue(targetName, out var target) ? target : null);
    }

    /// <summary>
    ///     Fires BeforeSave hooks on all entities in this session's scene,
    ///     giving strategies a final chance to flush in-memory state into
    ///     entity Data before serialization.
    /// </summary>
    internal void FireBeforeSaveHooks()
    {
        ThrowIfDisposed();
        // Snapshot the collection: BeforeSave hooks may spawn new entities,
        // and hosts expose a live entity view (the Godot adapter returns the
        // backing list). Entities spawned inside a hook are serialized but do
        // not fire BeforeSave (they entered the scene mid-save).
        List<ISndEntity> saveEntities = [.. _sceneHost.GetEntities()];
        foreach (var entity in saveEntities)
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

    /// <summary>
    ///     Releases every entity in the session: fires quit hooks (when requested),
    ///     tears down observer bindings bidirectionally (so observers receive
    ///     <c>OnUnmounted</c> and stop listening to target data), then releases
    ///     strategies and engine resources. Each harvested pass works on a
    ///     snapshot (hooks may spawn new entities, and hosts expose a live
    ///     entity view); entities spawned inside a hook are harvested by the
    ///     next pass, and processed entities are removed from the host so the
    ///     loop converges. A pass cap guards against a quit hook that spawns
    ///     entities forever (business-code pathology): it fails loudly instead
    ///     of hanging disposal.
    /// </summary>
    private void ReleaseAllEntitiesAndClear(bool fireQuitHooks)
    {
        const int maxPasses = 4;
        for (var pass = 0; pass < maxPasses; pass++)
        {
            List<ISndEntity> entities = [.. _sceneHost.GetEntities()];
            if (entities.Count == 0)
                return;

            foreach (var entity in entities)
                if (fireQuitHooks && entity is IEntityLifecycle lifecycle)
                    lifecycle.FireBeforeQuitHooks();

            foreach (var entity in entities)
                if (entity is IEntityLifecycle lifecycle)
                    lifecycle.TeardownObserverBindings();

            foreach (var entity in entities)
                if (entity is IEntityLifecycle lifecycle)
                {
                    lifecycle.ReleaseStrategiesOnly();
                    lifecycle.TeardownOnly();
                }

            foreach (var entity in entities)
                _sceneHost.RemoveEntity(entity.Name);
        }

        throw new InvalidOperationException(
            $"Entity teardown for session '{LevelId}' did not converge after {maxPasses} passes: " +
            "a quit hook keeps spawning entities during disposal.");
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
