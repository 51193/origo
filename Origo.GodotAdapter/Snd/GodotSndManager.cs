using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Godot;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Kernel.Ports;
using Origo.Core.Logging;
using Origo.Core.Snd.Strategy;
using Origo.GodotAdapter.Bootstrap;

namespace Origo.GodotAdapter.Snd;

/// <summary>
///     Internal Godot engine adapter scene host. Manages <see cref="GodotSndEntity" />
///     instances in a Godot scene tree and implements
///     <see cref="ISndSceneHost" />, <see cref="ISndContextAttachableSceneHost" />,
///     <see cref="IObserverTopologyHost" />, and
///     <see cref="IOwningSessionBindable" />. Entity collection logic is
///     delegated to <see cref="SndEntityCollection{T}" /> (pure C#); this
///     class only bridges it to the Godot node tree.
/// </summary>
internal partial class GodotSndManager
    : Node, ISndSceneHost, ISndContextAttachableSceneHost, IObserverTopologyHost, IOwningSessionBindable,
        ISndSceneHostRuntimeBinder
{
    private readonly SndEntityCollection<GodotSndEntity> _collection;
    private ObserverTopology? _observerTopology;

    private bool _runtimeDepsBound;

    internal SndWorld SharedWorld { get; private set; } = null!;
    internal ILogger SharedLogger { get; private set; } = null!;
    internal ISndContext? Context { get; private set; }

    /// <summary>
    ///     Creates an empty manager. Runtime dependencies and context binding are
    ///     framework-orchestrated startup wiring (driven by the bootstrap flow
    ///     through <see cref="ISndContextAttachableSceneHost" />); entities must
    ///     not be spawned until that wiring has completed.
    /// </summary>
    public GodotSndManager()
    {
        _collection = new SndEntityCollection<GodotSndEntity>(CreateSndEntity, DetachAndFree);
    }

    ObserverTopology IObserverTopologyHost.ObserverTopology =>
        _observerTopology ?? throw new InvalidOperationException(
            "ObserverTopology is not available. Call BindRuntimeDependencies before accessing the observer topology.");

    void IOwningSessionBindable.SetOwningSession(ISessionRun session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _collection.OwningSession = session;
    }

    /// <inheritdoc/>
    void ISndContextAttachableSceneHost.BindContext(ISndContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_runtimeDepsBound) throw new InvalidOperationException("Call BindRuntimeDependencies before BindContext.");

        Context = context;
        _observerTopology!.BindContext(context);
    }

    /// <inheritdoc/>
    IReadOnlyList<SndMetaData> ISndSceneAccess.BuildMetaList() => _collection.BuildMetaList();

    /// <inheritdoc/>
    void ISndSceneAccess.RecoverFromMetaList(IEnumerable<SndMetaData> metaList)
    {
        ArgumentNullException.ThrowIfNull(metaList);
        _collection.RecoverFromMetaList(metaList, (meta, ex) =>
        {
            // The logger may be unbound when entity operations reach this
            // host before BindRuntimeDependencies (a failed bootstrap);
            // guard it like the _ExitTree fallback path does.
            SharedLogger?.Log(LogLevel.Warning, nameof(GodotSndManager),
                new LogMessageBuilder().AddContext("entityName", meta.Name)
                    .Build($"Entity recovery failed, rolling back partial load: {ex.Message}"));
        });
    }

    /// <inheritdoc/>
    void ISndSceneHost.RemoveAllEntities() => _collection.RemoveAllEntities();

    /// <inheritdoc/>
    ISndEntity ISndSceneHost.CreateEntity(SndMetaData metaData)
    {
        ArgumentNullException.ThrowIfNull(metaData);
        try
        {
            return _collection.CreateEntity(metaData);
        }
        catch (Exception ex)
        {
            // The logger may be unbound when entity operations reach this
            // host before BindRuntimeDependencies (a failed bootstrap);
            // guard it like the _ExitTree fallback path does.
            SharedLogger?.Log(LogLevel.Warning, nameof(GodotSndManager),
                new LogMessageBuilder().AddContext("entityName", metaData.Name).Build($"Entity creation failed, rolling back: {ex.Message}"));
            throw;
        }
    }

    /// <summary>Gets a view of all currently alive entities in the scene.</summary>
    public IReadOnlyCollection<ISndEntity> GetEntities() => _collection.GetEntities();

    /// <summary>Looks up an entity by its stable name.</summary>
    public ISndEntity? FindByName(string name) => _collection.FindByName(name);

    /// <inheritdoc/>
    void ISndSceneHost.ProcessAll(double delta) => _collection.ProcessAll(delta);

    /// <inheritdoc/>
    void ISndSceneHost.RemoveEntity(string name) => _collection.RemoveEntity(name);

    /// <inheritdoc/>
    void ISndSceneHost.RequestKillEntity(string name) => _collection.RequestKillEntity(name);

    /// <summary>
    ///     Binds the Core <see cref="SndWorld" /> and logger, and creates the
    ///     per-scene-host observer topology. Must be called before
    ///     <see cref="BindContext" />; calling it twice throws.
    ///     <para>
    ///         Framework-orchestrated startup wiring: driven only by the
    ///         bootstrap flow (<see cref="OrigoAutoHost" />) through this
    ///         internal member; business code cannot rebind the runtime
    ///         dependencies on the concrete manager type.
    ///     </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when runtime dependencies are already bound.
    /// </exception>
    void ISndSceneHostRuntimeBinder.BindRuntimeDependencies(SndWorld world, ILogger logger) =>
        BindRuntimeDependencies(world, logger);

    [MemberNotNull(nameof(SharedWorld), nameof(SharedLogger))]
    internal void BindRuntimeDependencies(SndWorld world, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(logger);

        if (_runtimeDepsBound) throw new InvalidOperationException("Runtime dependencies are already bound.");

        SharedWorld = world;
        SharedLogger = logger;
        _observerTopology = new ObserverTopology(world.StrategyPool, logger);
        _runtimeDepsBound = true;
    }

    private GodotSndEntity CreateSndEntity()
    {
        EnsureReadyForSpawn();
        var entity = GodotSndEntity.Create(SharedWorld, Context!, SharedLogger, _observerTopology!,
            factoryParent => new GodotPackedSceneNodeFactory(factoryParent));
        AddChild(entity);
        return entity;
    }

    /// <summary>
    ///     Normal-exit teardown for the adapter host. Godot calls a child's
    ///     <c>_ExitTree</c> before its parent's, so this manager runs before
    ///     <see cref="OrigoDefaultEntry._ExitTree" />. When a progress run is
    ///     active, shut the SndContext down here while the entity collection is
    ///     still populated, so entity <c>BeforeQuit</c> hooks run; the parent
    ///     call is then idempotent. If no context/progress run is bound (or the
    ///     context is being torn down out of contract), fall back to the
    ///     per-entity cleanup below. Hook/cleanup failures are logged and
    ///     dropped because the node tree is already going away.
    /// </summary>
    public override void _ExitTree()
    {
        if (Context is not null && Context.Blackboard.ProgressBlackboard is not null)
        {
            try
            {
                Context.Shutdown();
            }
            catch (Exception ex)
            {
                if (_runtimeDepsBound)
                {
                    SharedLogger.Log(LogLevel.Warning, nameof(GodotSndManager),
                        new LogMessageBuilder()
                            .Build($"SndContext shutdown during manager _ExitTree failed: {ex.Message}"));
                }
            }
        }

        // Snapshot the collection: teardown triggers OnUnmounted hooks whose
        // handlers may mutate the collection, which would abort a live
        // enumeration and skip the remaining entities' cleanup.
        foreach (var entity in _collection.GetEntities())
        {
            if (entity is not IEntityLifecycle lifecycle)
                continue;

            // Each cleanup step runs independently: a throwing OnUnmounted
            // hook during observer teardown must not skip the strategy-pool
            // release (the binding is already removed, so nothing else can
            // return those references). Failures are logged and dropped here:
            // the node tree is already going away and this path exists for
            // out-of-contract cleanup.
            RunExitTreeCleanupStep(entity.Name, "observer teardown",
                () => lifecycle.TeardownObserverBindings());
            RunExitTreeCleanupStep(entity.Name, "strategy release",
                () => lifecycle.ReleaseStrategiesOnly());
        }

        _collection.RemoveAllEntities();
    }

    private void RunExitTreeCleanupStep(string entityName, string step, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            // The logger may be unbound when the node is torn down before
            // BindRuntimeDependencies (a failed bootstrap); guard it here.
            SharedLogger?.Log(LogLevel.Warning, nameof(GodotSndManager),
                new LogMessageBuilder()
                    .AddContext("entityName", entityName)
                    .AddContext("cleanupStep", step)
                    .Build($"Entity cleanup on manager exit failed: {ex.Message}"));
        }
    }

    private void DetachAndFree(GodotSndEntity entity)
    {
        if (IsInstanceValid(entity) && entity.GetParent() == this)
            RemoveChild(entity);
        if (IsInstanceValid(entity))
            entity.Free();
    }

    private void EnsureReadyForSpawn()
    {
        if (!_runtimeDepsBound || Context is null || _observerTopology is null)
            throw new InvalidOperationException(
                "GodotSndManager is not ready: call BindRuntimeDependencies and BindContext before spawning entities.");
    }
}
