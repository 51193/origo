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
///     <see cref="ISndSceneHost" />, <see cref="IObserverTopologyHost" />,
///     and <see cref="IOwningSessionBindable" />. Entity collection logic is
///     delegated to <see cref="SndEntityCollection{T}" /> (pure C#); this
///     class only bridges it to the Godot node tree.
/// </summary>
[GlobalClass]
public partial class GodotSndManager
    : Node, ISndSceneHost, ISndContextAttachableSceneHost, IObserverTopologyHost, IOwningSessionBindable
{
    private readonly SndEntityCollection<GodotSndEntity> _collection;
    private ObserverTopology? _observerTopology;
    private ISessionRun? _owningSession;

    private bool _runtimeDepsBound;

    internal SndWorld SharedWorld { get; private set; } = null!;
    internal ILogger SharedLogger { get; private set; } = null!;
    internal ISndContext? Context { get; private set; }

    /// <summary>Number of <see cref="ProcessAll" /> invocations. Framework-internal observability (test projects access via InternalsVisibleTo).</summary>
    internal int ProcessTickCount { get; private set; }

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
        _owningSession = session;
        _collection.OwningSession = session;
    }

    public void BindContext(ISndContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_runtimeDepsBound) throw new InvalidOperationException("Call BindRuntimeDependencies before BindContext.");

        Context = context;
        _observerTopology!.BindContext(context);
    }

    public IReadOnlyList<SndMetaData> BuildMetaList() => _collection.BuildMetaList();

    public void RecoverFromMetaList(IEnumerable<SndMetaData> metaList)
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

    public void RemoveAllEntities() => _collection.RemoveAllEntities();

    public ISndEntity CreateEntity(SndMetaData metaData)
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

    public IReadOnlyCollection<ISndEntity> GetEntities() => _collection.GetEntities();

    public ISndEntity? FindByName(string name) => _collection.FindByName(name);

    public void ProcessAll(double delta)
    {
        ProcessTickCount++;
        _collection.ProcessAll(delta);
    }

    public void RemoveEntity(string name) => _collection.RemoveEntity(name);

    public void RequestKillEntity(string name) => _collection.RequestKillEntity(name);

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
