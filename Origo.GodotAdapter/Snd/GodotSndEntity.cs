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

[GlobalClass]
public partial class GodotSndEntity : Node, ISndEntity, IEntityLifecycle, ISndEntityRawSubscription
{
    private readonly ISndContext _context;
    private readonly ILogger _logger;
    private readonly Func<GodotSndEntity, INodeFactory> _nodeFactoryCreator;
    private readonly ObserverTopology _observerTopology;
    private readonly SndWorld _world;
    private SndEntity? _entity;
    internal void BindSession(ISessionRun session)
    {
        ThrowIfReleasedFromManager();
        if (_entity is not null)
            _entity.BindSession(session);
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

    public void SetData<T>(string name, T value)
    {
        EnsureEntity();
        _entity!.SetData(name, value);
    }

    public T GetData<T>(string name)
    {
        EnsureEntity();
        return _entity!.GetData<T>(name);
    }

    public (bool found, T? value) TryGetData<T>(string name)
    {
        EnsureEntity();
        return _entity!.TryGetData<T>(name);
    }

    public void MountObserverStrategy(string targetName, string observerIndex)
    {
        EnsureEntity();
        _entity!.MountObserverStrategy(targetName, observerIndex);
    }

    public void UnmountObserverStrategy(string targetName, string observerIndex)
    {
        EnsureEntity();
        _entity!.UnmountObserverStrategy(targetName, observerIndex);
    }

    public void MountObserverStrategy(ISndEntity target, string observerIndex)
    {
        EnsureEntity();
        _entity!.MountObserverStrategy(target, observerIndex);
    }

    public void UnmountObserverStrategy(ISndEntity target, string observerIndex)
    {
        EnsureEntity();
        _entity!.UnmountObserverStrategy(target, observerIndex);
    }

    void ISndEntityRawSubscription.SubscribeDataRaw(string name, Action<ISndEntity, TypedData, TypedData> callback,
        Func<ISndEntity, TypedData, TypedData, bool>? filter)
    {
        EnsureEntity();
        ((ISndEntityRawSubscription)_entity!).SubscribeDataRaw(name, callback, filter);
    }

    void ISndEntityRawSubscription.UnsubscribeDataRaw(string name, Action<ISndEntity, TypedData, TypedData> callback)
    {
        EnsureEntity();
        ((ISndEntityRawSubscription)_entity!).UnsubscribeDataRaw(name, callback);
    }

    public INodeHandle GetNode(string name)
    {
        EnsureEntity();
        return _entity!.GetNode(name);
    }

    public IReadOnlyCollection<string> GetNodeNames()
    {
        EnsureEntity();
        return _entity!.GetNodeNames();
    }

    public void AddStrategy(string index)
    {
        EnsureEntity();
        _entity!.AddStrategy(index);
    }

    public void RemoveStrategy(string index)
    {
        EnsureEntity();
        _entity!.RemoveStrategy(index);
    }

    public void AddActiveStrategy(string index)
    {
        EnsureEntity();
        _entity!.AddActiveStrategy(index);
    }

    public void RemoveActiveStrategy(string index)
    {
        EnsureEntity();
        _entity!.RemoveActiveStrategy(index);
    }

    public object? InvokeStrategy(string strategyIndex, object? input = null)
    {
        EnsureEntity();
        return _entity!.InvokeStrategy(strategyIndex, input);
    }

    public bool IsPendingKill => _entity?.IsPendingKill ?? false;

    internal void MarkPendingKill()
    {
        EnsureEntity();
        _entity!.IsPendingKill = true;
    }

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
        EnsureEntity();
        ((IEntityLifecycle)_entity!).RecoverForLifecycle(metaData);
        StableName = _entity.Name;
        Name = _entity.Name;
    }

    internal void RecoverForLifecycle(SndMetaData metaData) => ((IEntityLifecycle)this).RecoverForLifecycle(metaData);

    void IEntityLifecycle.FireAfterSpawnHooks()
    {
        EnsureEntity();
        ((IEntityLifecycle)_entity!).FireAfterSpawnHooks();
    }

    void IEntityLifecycle.FireAfterLoadHooks()
    {
        EnsureEntity();
        ((IEntityLifecycle)_entity!).FireAfterLoadHooks();
    }

    void IEntityLifecycle.FireBeforeSaveHooks()
    {
        EnsureEntity();
        ((IEntityLifecycle)_entity!).FireBeforeSaveHooks();
    }

    void IEntityLifecycle.FireBeforeQuitHooks()
    {
        EnsureEntity();
        ((IEntityLifecycle)_entity!).FireBeforeQuitHooks();
    }

    void IEntityLifecycle.FireBeforeDeadHooks()
    {
        EnsureEntity();
        ((IEntityLifecycle)_entity!).FireBeforeDeadHooks();
    }

    void IEntityLifecycle.ReleaseStrategiesOnly()
    {
        EnsureEntity();
        ((IEntityLifecycle)_entity!).ReleaseStrategiesOnly();
    }

    void IEntityLifecycle.TeardownOnly()
    {
        EnsureEntity();
        ((IEntityLifecycle)_entity!).TeardownOnly();
    }

    SndMetaData IEntityLifecycle.BuildMetaData()
    {
        EnsureEntity();
        return ((IEntityLifecycle)_entity!).BuildMetaData();
    }

    internal SndMetaData BuildSndMetaData()
    {
        EnsureEntity();
        return ((IEntityLifecycle)_entity!).BuildMetaData();
    }

    public void SpawnSingle(SndMetaData metaData)
    {
        ThrowIfReleasedFromManager();
        ArgumentNullException.ThrowIfNull(metaData);
        StableName = metaData.Name;
        Name = metaData.Name;
        EnsureEntity();
        _entity!.SpawnSingle(metaData);
        StableName = _entity.Name;
        Name = _entity.Name;
    }

    public void LoadSingle(SndMetaData metaData)
    {
        ThrowIfReleasedFromManager();
        ArgumentNullException.ThrowIfNull(metaData);
        StableName = metaData.Name;
        Name = metaData.Name;
        EnsureEntity();
        _entity!.LoadSingle(metaData);
        StableName = _entity.Name;
        Name = _entity.Name;
    }

    internal void DetachFromManager()
    {
        if (_releasedFromManager) return;
        _releasedFromManager = true;
        _entity = null;
        Free();
    }

    public SndMetaData SaveSingle()
    {
        EnsureEntity();
        return _entity!.SaveSingle();
    }

    public void ProcessSnd(double delta) => _entity?.Process(delta);

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
