using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
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
internal sealed class SessionRun : ISessionRun, IDisposable
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
            // A null node is corrupted data (the writer always emits the full
            // three-file set; missing files fail earlier in the reader). The
            // strict read must reject it instead of silently loading an empty
            // scene — matching ValidateLevelPayload on the direct-load path.
            if (payload.SessionNode.IsNull)
                throw new InvalidOperationException(
                    $"Level payload for '{LevelId}' has a null session node: " +
                    "the save data is corrupted or was written by an incompatible version.");
            if (payload.SessionStateMachinesNode.IsNull)
                throw new InvalidOperationException(
                    $"Level payload for '{LevelId}' has a null session state machines node: " +
                    "the save data is corrupted or was written by an incompatible version.");
            if (payload.SndSceneNode.IsNull)
                throw new InvalidOperationException(
                    $"Level payload for '{LevelId}' has a null scene node: " +
                    "the save data is corrupted or was written by an incompatible version.");

            _saveContext.DeserializeSession(payload.SessionNode);
            _sessionScope.StateMachines.DeserializeFromNode(
                payload.SessionStateMachinesNode,
                _saveContext.SndWorld.ConverterRegistry);

            IReadOnlyList<SndMetaData>? recoveredSceneMeta = null;
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
            // otherwise mask the exception that triggered them. Cleanup
            // failures are logged and never mask the original failure.
            _logger.Log(LogLevel.Error, _logTag,
                new LogMessageBuilder()
                    .AddContext("levelId", LevelId)
                    .Build($"Payload load failed: {ex.Message}"));
            try
            {
                ResetAfterLoadFailure();
            }
            catch (Exception cleanupEx)
            {
                _logger.Log(LogLevel.Warning, _logTag,
                    new LogMessageBuilder()
                        .AddContext("levelId", LevelId)
                        .Build($"Session cleanup after failed load failed: {cleanupEx.Message}"));
            }

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

        Exception? firstFailure = null;

        if (topology is not null)
        {
            // Resolve entity names to instances once; the same dictionary
            // serves outgoing target resolution and incoming observer lookup.
            // Entity names are not mandated unique by the host contract; a
            // duplicate name collapses the lookup (last one wins), which is
            // observable via this warning instead of being fully silent.
            var entitiesByName = new Dictionary<string, ISndEntity>(entities.Count, StringComparer.Ordinal);
            foreach (var other in entities)
            {
                if (entitiesByName.ContainsKey(other.Name))
                    _logger.Log(LogLevel.Warning, _logTag,
                        new LogMessageBuilder()
                            .AddContext("entityName", other.Name)
                            .Build("Duplicate entity name detected while resolving kill-pending observers; the last entity wins."));
                entitiesByName[other.Name] = other;
            }

            // Outgoing teardown: every pending entity's own observer bindings
            // are unmounted (unsubscribe + OnUnmounted + pool release),
            // regardless of whether the entity is a bare SndEntity or an
            // adapter wrapper implementing ISndEntityRawSubscription.
            foreach (var e in pending)
                firstFailure = TryEntityStep(e, firstFailure, "observer teardown (outgoing)",
                    () => TeardownOutgoingObserverBindings(e, entitiesByName, topology));

            // Incoming teardown via the topology's O(1) incoming index: a
            // dying entity may be the target of other entities' bindings.
            foreach (var e in pending)
                firstFailure = TryEntityStep(e, firstFailure, "observer teardown (incoming)", () =>
                {
                    foreach (var observerName in topology.GetObserverNamesTargeting(e.Name))
                        if (entitiesByName.TryGetValue(observerName, out var observer))
                            topology.RemoveBindingsTargetingFor(observer, e.Name);
                });
        }

        // Each phase runs independently per entity (matching the dispose
        // path): a throwing BeforeDead hook must not skip the strategy
        // release or the physical removal of any pending entity, otherwise
        // the entity stays pending forever and the sweep re-fails every
        // frame. The first failure is rethrown after the sweep completes.
        foreach (var e in pending)
            firstFailure = TryEntityStep(e, firstFailure, "before-dead hook",
                () => ((IEntityLifecycle)e).FireBeforeDeadHooks());

        foreach (var e in pending)
            firstFailure = TryEntityStep(e, firstFailure, "strategy release", () =>
            {
                var lifecycle = (IEntityLifecycle)e;
                lifecycle.ReleaseStrategiesOnly();
                lifecycle.TeardownOnly();
            });

        // Physical removal is a host operation independent of lifecycle
        // delegation: every pending entity must leave the host for the sweep
        // to converge. A removal failure propagates immediately.
        foreach (var e in pending)
            _sceneHost.RemoveEntity(e.Name);

        if (firstFailure is not null)
            ExceptionDispatchInfo.Capture(firstFailure).Throw();
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

    /// <summary>
    ///     Rolls the session back to a clean state after a failed load: every
    ///     cleanup step (state machines, entities, scene host, blackboard)
    ///     runs independently even when an earlier step throws, so a user hook
    ///     failure (e.g. <c>OnUnmounted</c>) cannot skip the remaining steps.
    ///     Step failures are logged and rethrown as an
    ///     <see cref="AggregateException" /> so the caller can surface them
    ///     without masking the original load failure.
    /// </summary>
    private void ResetAfterLoadFailure()
    {
        var failures = new List<Exception>();
        TryCleanupStep("state machines", () => _sessionScope.StateMachines.Clear(), failures);
        TryCleanupStep("entities", () => ReleaseAllEntitiesAndClear(false), failures);
        TryCleanupStep("scene host", () => _sceneHost.RemoveAllEntities(), failures);
        TryCleanupStep("blackboard", () => _sessionScope.Blackboard.Clear(), failures);

        if (failures.Count > 0)
            throw new AggregateException(
                "Session cleanup after failed load did not fully complete; see inner exceptions.", failures);
    }

    private void TryCleanupStep(string step, Action action, List<Exception> failures)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Warning, _logTag,
                new LogMessageBuilder()
                    .AddContext("levelId", LevelId)
                    .AddContext("cleanupStep", step)
                    .Build($"Load-failure cleanup step failed: {ex.Message}"));
            failures.Add(ex);
        }
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
    ///     <para>
    ///     Hook failures (BeforeQuit, OnUnmounted) propagate fail-fast, but each
    ///     phase runs independently per entity: a throwing hook on one entity
    ///     must not skip the release of every other entity (strategy-pool
    ///     references and node handles would leak). The first hook failure is
    ///     rethrown after the release passes complete; further failures are
    ///     logged as warnings so they stay visible.
    ///     </para>
    /// </summary>
    private void ReleaseAllEntitiesAndClear(bool fireQuitHooks)
    {
        const int maxPasses = 4;
        Exception? firstHookFailure = null;
        var pass = 0;
        while (true)
        {
            List<ISndEntity> entities = [.. _sceneHost.GetEntities()];
            if (entities.Count == 0)
                break;

            if (pass >= maxPasses)
                throw new InvalidOperationException(
                    $"Entity teardown for session '{LevelId}' did not converge after {maxPasses} passes: " +
                    "a quit hook keeps spawning entities during disposal.");

            if (fireQuitHooks)
                foreach (var entity in entities)
                    firstHookFailure = TryEntityStep(entity, firstHookFailure, "quit hook",
                        () => ((IEntityLifecycle)entity).FireBeforeQuitHooks());

            foreach (var entity in entities)
                firstHookFailure = TryEntityStep(entity, firstHookFailure, "observer teardown",
                    () => ((IEntityLifecycle)entity).TeardownObserverBindings());

            foreach (var entity in entities)
                firstHookFailure = TryEntityStep(entity, firstHookFailure, "strategy release",
                    () =>
                    {
                        var lifecycle = (IEntityLifecycle)entity;
                        lifecycle.ReleaseStrategiesOnly();
                        lifecycle.TeardownOnly();
                    });

            // Physical removal is a host operation independent of lifecycle
            // delegation: every harvested entity must leave the host for the
            // pass loop to converge. A removal failure propagates immediately
            // (a host that cannot remove entities cannot be cleaned).
            foreach (var entity in entities)
                _sceneHost.RemoveEntity(entity.Name);

            pass++;
        }

        if (firstHookFailure is not null)
            ExceptionDispatchInfo.Capture(firstHookFailure).Throw();
    }

    private Exception? TryEntityStep(ISndEntity entity, Exception? firstFailure, string step, Action action)
    {
        if (entity is not IEntityLifecycle)
            return firstFailure;

        try
        {
            action();
            return firstFailure;
        }
        catch (Exception ex)
        {
            if (firstFailure is null)
                return ex;
            _logger.Log(LogLevel.Warning, _logTag,
                new LogMessageBuilder()
                    .AddContext("entityName", entity.Name)
                    .AddContext("step", step)
                    .Build($"Entity teardown step failed during disposal: {ex.Message}"));
            return firstFailure;
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
