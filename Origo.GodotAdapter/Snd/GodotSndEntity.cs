using Origo.Core.Abstractions.Lifecycle;
using System;
using System.Collections.Generic;
using Godot;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Node;
using Origo.Core.Snd;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;

namespace Origo.GodotAdapter.Snd;

/// <summary>
///     Godot engine adapter for <see cref="SndEntity" />. Wraps a Core
///     <see cref="SndEntity" /> inside a Godot <see cref="Node" /> so that
///     SND entities participate in the Godot scene tree.
///     <para>
///         Implements <see cref="ISndEntity" /> (delegating to the inner
///         SndEntity) and <see cref="IEntityLifecycle" /> +
///         <see cref="ISndEntityRawSubscription" /> (explicit interface
///         implementations, also delegating to the inner entity).
///     </para>
/// </summary>
[GlobalClass]
public partial class GodotSndEntity : Node, ISndEntity, IEntityLifecycle, ISndEntityRawSubscription, ISndEntityFacade
{
    private readonly ISndContext _context;
    private readonly ILogger _logger;
    private readonly Func<GodotSndEntity, INodeFactory> _nodeFactoryCreator;
    private readonly ObserverTopology _observerTopology;
    private readonly SndWorld _world;
    private SndEntity? _entity;

    private SndEntity Entity
    {
        get
        {
            EnsureEntity();
            return _entity!;
        }
    }

    internal void BindSession(ISessionRun session)
    {
        ThrowIfReleasedFromManager();
        Entity.BindSession(session);
    }
    public ISessionRun OwningSession => _entity?.OwningSession ?? throw new InvalidOperationException("GodotSndEntity has no backing SndEntity.");
    private bool _releasedFromManager;

    internal GodotSndEntity(
        SndWorld world,
        ISndContext context,
        ILogger logger,
        ObserverTopology observerTopology,
        Func<GodotSndEntity, INodeFactory> nodeFactoryCreator)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(observerTopology);
        ArgumentNullException.ThrowIfNull(nodeFactoryCreator);
        _world = world;
        _context = context;
        _logger = logger;
        _observerTopology = observerTopology;
        _nodeFactoryCreator = nodeFactoryCreator;
    }

    internal string StableName { get; private set; } = string.Empty;

    string ISndEntity.Name => StableName;

    public void SetData<T>(string name, T value) => Entity.SetData(name, value);

    public T GetData<T>(string name) where T : notnull => Entity.GetData<T>(name);

    public (bool found, T? value) TryGetData<T>(string name) => Entity.TryGetData<T>(name);

    public bool TryGetData<T>(string name, out T? value) => Entity.TryGetData<T>(name, out value);

    public void MountObserverStrategy(string targetName, string observerIndex) => Entity.MountObserverStrategy(targetName, observerIndex);

    public void UnmountObserverStrategy(string targetName, string observerIndex) => Entity.UnmountObserverStrategy(targetName, observerIndex);

    public void MountObserverStrategy(ISndEntity target, string observerIndex) => Entity.MountObserverStrategy(target, observerIndex);

    public void UnmountObserverStrategy(ISndEntity target, string observerIndex) => Entity.UnmountObserverStrategy(target, observerIndex);

    void ISndEntityRawSubscription.SubscribeDataRaw(string name, Action<ISndEntity, TypedData, TypedData> callback,
        Func<ISndEntity, TypedData, TypedData, bool>? filter) => ((ISndEntityRawSubscription)Entity).SubscribeDataRaw(name, callback, filter);

    void ISndEntityRawSubscription.UnsubscribeDataRaw(string name, Action<ISndEntity, TypedData, TypedData> callback) => ((ISndEntityRawSubscription)Entity).UnsubscribeDataRaw(name, callback);

    public INodeHandle GetNode(string name) => Entity.GetNode(name);

    public IReadOnlyCollection<string> GetNodeNames() => Entity.GetNodeNames();

    public void AddStrategy(string index) => Entity.AddStrategy(index);

    public void RemoveStrategy(string index) => Entity.RemoveStrategy(index);

    public void AddActiveStrategy(string index) => Entity.AddActiveStrategy(index);

    public void RemoveActiveStrategy(string index) => Entity.RemoveActiveStrategy(index);

    public object? InvokeStrategy(string strategyIndex, object? input = null) => Entity.InvokeStrategy(strategyIndex, input);

    public bool IsPendingKill => _entity?.IsPendingKill
        ?? throw new InvalidOperationException(
            "GodotSndEntity has been released from GodotSndManager and cannot be used.");

    internal void MarkPendingKill() => Entity.IsPendingKill = true;

    public TNode? GetNodeFromSnd<TNode>(string name) where TNode : Node
    {
        var handle = GetNode(name);
        if (handle is GodotNodeHandle godotHandle)
            return godotHandle.UnsafeGetNode() as TNode;
        return null;
    }

    void IEntityLifecycle.RecoverForLifecycle(SndMetaData metaData)
    {
        ThrowIfReleasedFromManager();
        ArgumentNullException.ThrowIfNull(metaData);
        StableName = metaData.Name;
        Name = metaData.Name;
        ((IEntityLifecycle)Entity).RecoverForLifecycle(metaData);
        StableName = Entity.Name;
        Name = Entity.Name;
    }

    internal void RecoverForLifecycle(SndMetaData metaData) => ((IEntityLifecycle)this).RecoverForLifecycle(metaData);

    void IEntityLifecycle.FireAfterSpawnHooks() => ((IEntityLifecycle)Entity).FireAfterSpawnHooks();

    void IEntityLifecycle.FireAfterLoadHooks() => ((IEntityLifecycle)Entity).FireAfterLoadHooks();

    void IEntityLifecycle.FireBeforeSaveHooks() => ((IEntityLifecycle)Entity).FireBeforeSaveHooks();

    void IEntityLifecycle.FireBeforeQuitHooks() => ((IEntityLifecycle)Entity).FireBeforeQuitHooks();

    void IEntityLifecycle.FireBeforeDeadHooks() => ((IEntityLifecycle)Entity).FireBeforeDeadHooks();

    void IEntityLifecycle.ReleaseStrategiesOnly() => ((IEntityLifecycle)Entity).ReleaseStrategiesOnly();

    void IEntityLifecycle.TeardownOnly() => ((IEntityLifecycle)Entity).TeardownOnly();

    void IEntityLifecycle.TeardownObserverBindings() =>
        ((IEntityLifecycle)Entity).TeardownObserverBindings();

    SndMetaData IEntityLifecycle.BuildMetaData() => ((IEntityLifecycle)Entity).BuildMetaData();

    internal SndMetaData BuildSndMetaData() => ((IEntityLifecycle)Entity).BuildMetaData();

    /// <summary>
    ///     Detaches this entity from its manager: marks it released and drops
    ///     the backing Core entity. Engine-level teardown (RemoveChild/Free)
    ///     is the responsibility of the manager's detach callback, which
    ///     always runs right after this method.
    /// </summary>
    internal void DetachFromManager()
    {
        if (_releasedFromManager) return;
        _releasedFromManager = true;
        _entity = null;
    }

    string ISndEntityFacade.StableName => StableName;

    SndMetaData ISndEntityFacade.BuildSndMetaData() => BuildSndMetaData();

    void ISndEntityFacade.RecoverForLifecycle(SndMetaData meta) => RecoverForLifecycle(meta);

    void ISndEntityFacade.BindSession(ISessionRun session) => BindSession(session);

    void ISndEntityFacade.ProcessSnd(double delta) => ProcessSnd(delta);

    void ISndEntityFacade.DetachFromManager() => DetachFromManager();

    void ISndEntityFacade.MarkPendingKill() => MarkPendingKill();

    internal void ProcessSnd(double delta)
    {
        ThrowIfReleasedFromManager();
        _entity!.Process(delta);
    }

    private void EnsureEntity()
    {
        ThrowIfReleasedFromManager();
        if (_entity is not null) return;
        var nodeFactory = _nodeFactoryCreator(this);
        _entity = _world.CreateEntity(nodeFactory, _context, _logger, _observerTopology);
    }

    private void ThrowIfReleasedFromManager()
    {
        if (_releasedFromManager)
            throw new InvalidOperationException(
                "GodotSndEntity has been released from GodotSndManager and cannot be used.");
    }
}
