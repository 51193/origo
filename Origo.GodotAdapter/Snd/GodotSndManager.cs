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
using Origo.Core.Logging;
using Origo.Core.Snd.Strategy;

namespace Origo.GodotAdapter.Snd;

/// <summary>
///     Godot engine adapter scene host. Manages <see cref="GodotSndEntity" />
///     instances in a Godot scene tree and implements
///     <see cref="ISndSceneHost" />, <see cref="ISndContextAttachableSceneHost" />,
///     <see cref="IObserverTopologyHost" />, and
///     <see cref="IOwningSessionBindable" />. Entity collection logic is
///     delegated to <see cref="SndEntityCollection{T}" /> (pure C#); this
///     class only bridges it to the Godot node tree.
/// </summary>
[GlobalClass]
public partial class GodotSndManager
    : Node, ISndSceneHost, ISndContextAttachableSceneHost, IObserverTopologyHost, IOwningSessionBindable
{
    private readonly SndEntityCollection<GodotSndEntity> _collection;
    private ObserverTopology? _observerTopology;

    private bool _runtimeDepsBound;

    internal SndWorld SharedWorld { get; private set; } = null!;
    internal ILogger SharedLogger { get; private set; } = null!;
    internal ISndContext? Context { get; private set; }

    /// <summary>
    ///     Creates an empty manager. Call <see cref="BindRuntimeDependencies" />
    ///     and <see cref="BindContext" /> before spawning entities.
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
            if (SharedLogger is null)
                return;
            SharedLogger.Log(LogLevel.Warning, nameof(GodotSndManager),
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
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when runtime dependencies are already bound.
    /// </exception>
    [MemberNotNull(nameof(SharedWorld), nameof(SharedLogger))]
    public void BindRuntimeDependencies(SndWorld world, ILogger logger)
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
        var entity = new GodotSndEntity(SharedWorld, Context!, SharedLogger, _observerTopology!,
            factoryParent => new GodotPackedSceneNodeFactory(factoryParent));
        AddChild(entity);
        return entity;
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
