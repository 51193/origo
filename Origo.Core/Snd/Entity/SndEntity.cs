using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Node;
using Origo.Core.Logging;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;

namespace Origo.Core.Snd.Entity;

/// <summary>
///     SND 聚合实体。封装数据、节点与策略生命周期，保持 Core 与引擎解耦。
/// </summary>
public sealed class SndEntity : ISndEntity, IEntityLifecycle, ISndEntityRawSubscription
{
    private const string LogTag = nameof(SndEntity);
    private readonly ActiveStrategyManager _activeStrategyManager;
    private readonly ISndContext _context;
    private readonly SndDataManager _dataManager;
    private readonly ILogger _logger;
    private readonly SndNodeManager _nodeHost;
    private readonly SndStrategyManager _strategyManager;

    private readonly List<Action<ISndEntity, EntityLifecycleEvent>> _lifecycleObservers = new();
    private readonly List<OutgoingDataSub> _outgoingDataSubs = new();
    private readonly List<OutgoingLifecycleSub> _outgoingLifecycleSubs = new();

    internal SndEntity(
        INodeFactory nodeFactory,
        SndStrategyPool strategyPool,
        Func<string, string> sceneAliasResolver,
        ISndContext context,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(nodeFactory);
        ArgumentNullException.ThrowIfNull(strategyPool);
        ArgumentNullException.ThrowIfNull(sceneAliasResolver);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        _context = context;
        _logger = logger;

        _dataManager = new SndDataManager(this, logger);
        var nodeHost = new SndNodeManager(nodeFactory, logger);
        nodeHost.SetSceneAliasResolver(sceneAliasResolver);
        _nodeHost = nodeHost;
        _strategyManager = new SndStrategyManager(strategyPool, logger);
        _activeStrategyManager = new ActiveStrategyManager(strategyPool);
    }

    public string Name { get; internal set; } = string.Empty;

    public void SetData<T>(string name, T value) => _dataManager.SetData(name, value);

    public T GetData<T>(string name) => _dataManager.GetData<T>(name);

    public (bool found, T? value) TryGetData<T>(string name) => _dataManager.TryGetData<T>(name);

    public void Subscribe(string name, Action<ISndEntity, ISndEntity, TypedData, TypedData> callback,
        Func<ISndEntity, ISndEntity, TypedData, TypedData, bool>? filter = null)
    {
        SubscribeDataInternal((ISndEntity)this, name, callback, filter);
    }

    public void Unsubscribe(string name, Action<ISndEntity, ISndEntity, TypedData, TypedData> callback)
    {
        UnobserveData((ISndEntity)this, name, callback);
    }

    public void ObserveData(ISndEntity target, string dataName,
        Action<ISndEntity, ISndEntity, TypedData, TypedData> callback,
        Func<ISndEntity, ISndEntity, TypedData, TypedData, bool>? filter = null)
    {
        SubscribeDataInternal(target, dataName, callback, filter);
    }

    public void UnobserveData(ISndEntity target, string dataName,
        Action<ISndEntity, ISndEntity, TypedData, TypedData> callback)
    {
        for (var i = _outgoingDataSubs.Count - 1; i >= 0; i--)
        {
            var sub = _outgoingDataSubs[i];
            if (sub.Target != target || sub.DataName != dataName || sub.OriginalCallback != callback)
                continue;
            ((ISndEntityRawSubscription)target).UnsubscribeDataRaw(dataName, sub.WrappedCallback);
            _outgoingDataSubs.RemoveAt(i);
        }
    }

    public void SubscribeLifecycle(Action<ISndEntity, ISndEntity, EntityLifecycleEvent> callback)
    {
        SubscribeLifecycleInternal((ISndEntity)this, callback);
    }

    public void UnsubscribeLifecycle(Action<ISndEntity, ISndEntity, EntityLifecycleEvent> callback)
    {
        UnobserveLifecycle((ISndEntity)this, callback);
    }

    public void ObserveLifecycle(ISndEntity target,
        Action<ISndEntity, ISndEntity, EntityLifecycleEvent> callback)
    {
        SubscribeLifecycleInternal(target, callback);
    }

    public void UnobserveLifecycle(ISndEntity target,
        Action<ISndEntity, ISndEntity, EntityLifecycleEvent> callback)
    {
        for (var i = _outgoingLifecycleSubs.Count - 1; i >= 0; i--)
        {
            var sub = _outgoingLifecycleSubs[i];
            if (sub.Target != target || sub.OriginalCallback != callback)
                continue;
            ((ISndEntityRawSubscription)target).UnsubscribeLifecycleRaw(sub.WrappedCallback);
            _outgoingLifecycleSubs.RemoveAt(i);
        }
    }

    public INodeHandle GetNode(string name) => _nodeHost.GetNode(name);

    public IReadOnlyCollection<string> GetNodeNames() => _nodeHost.GetNodeNames();

    public void AddStrategy(string index) => _strategyManager.Add(this, index, _context);

    public void RemoveStrategy(string index) => _strategyManager.Remove(this, index, _context);

    public void AddActiveStrategy(string index) => _activeStrategyManager.Add(index);

    public void RemoveActiveStrategy(string index) => _activeStrategyManager.Remove(index);

    public object? InvokeStrategy(string strategyIndex, object? input = null) =>
        _activeStrategyManager.Invoke(this, _context, strategyIndex, input);

    public bool IsPendingKill { get; set; }

    public void Process(double delta) => _strategyManager.Process(this, delta, _context);

    void ISndEntityRawSubscription.SubscribeDataRaw(string name, Action<ISndEntity, TypedData, TypedData> callback,
        Func<ISndEntity, TypedData, TypedData, bool>? filter)
    {
        _dataManager.Subscribe(name, callback, filter);
    }

    void ISndEntityRawSubscription.UnsubscribeDataRaw(string name, Action<ISndEntity, TypedData, TypedData> callback)
    {
        _dataManager.Unsubscribe(name, callback);
    }

    void ISndEntityRawSubscription.SubscribeLifecycleRaw(Action<ISndEntity, EntityLifecycleEvent> callback)
    {
        _lifecycleObservers.Add(callback);
    }

    void ISndEntityRawSubscription.UnsubscribeLifecycleRaw(Action<ISndEntity, EntityLifecycleEvent> callback)
    {
        _lifecycleObservers.Remove(callback);
    }

    void IEntityLifecycle.RecoverForLifecycle(SndMetaData metaData)
    {
        Name = metaData.Name;
        _dataManager.Recover(metaData.DataMetaData ??
                             throw new InvalidOperationException("DataMetaData is required."));
        _nodeHost.Recover(metaData.NodeMetaData ??
                          throw new InvalidOperationException("NodeMetaData is required."));
        _strategyManager.RecoverStrategiesOnly(
            metaData.StrategyMetaData?.EntityIndices ?? Enumerable.Empty<string>());
        _activeStrategyManager.Recover(
            metaData.StrategyMetaData?.ActiveIndices ?? Enumerable.Empty<string>());
    }

    void IEntityLifecycle.FireAfterSpawnHooks()
    {
        NotifyLifecycleObservers(EntityLifecycleEvent.AfterSpawn);
        _strategyManager.TriggerAfterSpawn(this, _context);
        _logger.Log(LogLevel.Debug, LogTag,
            new LogMessageBuilder().AddContext("entityName", Name).Build("Entity spawned (hooks)."));
    }

    void IEntityLifecycle.FireAfterLoadHooks()
    {
        NotifyLifecycleObservers(EntityLifecycleEvent.AfterLoad);
        _strategyManager.TriggerAfterLoad(this, _context);
        _logger.Log(LogLevel.Debug, LogTag,
            new LogMessageBuilder().AddContext("entityName", Name).Build("Entity loaded (hooks)."));
    }

    void IEntityLifecycle.FireBeforeSaveHooks()
    {
        NotifyLifecycleObservers(EntityLifecycleEvent.BeforeSave);
        _strategyManager.TriggerBeforeSave(this, _context);
    }

    void IEntityLifecycle.FireBeforeQuitHooks()
    {
        NotifyLifecycleObservers(EntityLifecycleEvent.BeforeQuit);
        _strategyManager.TriggerBeforeQuit(this, _context);
    }

    void IEntityLifecycle.FireBeforeDeadHooks()
    {
        NotifyLifecycleObservers(EntityLifecycleEvent.BeforeDead);
        _strategyManager.TriggerBeforeDead(this, _context);
    }

    void IEntityLifecycle.ReleaseStrategiesOnly()
    {
        _activeStrategyManager.ReleaseAll();
        _strategyManager.ReleaseStrategiesOnly();
    }

    void IEntityLifecycle.TeardownOnly()
    {
        foreach (var sub in _outgoingDataSubs)
            ((ISndEntityRawSubscription)sub.Target).UnsubscribeDataRaw(sub.DataName, sub.WrappedCallback);
        _outgoingDataSubs.Clear();

        foreach (var sub in _outgoingLifecycleSubs)
            ((ISndEntityRawSubscription)sub.Target).UnsubscribeLifecycleRaw(sub.WrappedCallback);
        _outgoingLifecycleSubs.Clear();

        _lifecycleObservers.Clear();
        _nodeHost.Release();
        _dataManager.Release();
    }

    SndMetaData IEntityLifecycle.BuildMetaData()
    {
        var entityIndices = _strategyManager.GetStrategyIndices();
        var activeIndices = _activeStrategyManager.SerializeIndices();

        return new SndMetaData
        {
            Name = Name,
            NodeMetaData = _nodeHost.SerializeMetaData(),
            StrategyMetaData = new StrategyMetaData
            {
                EntityIndices = new List<string>(entityIndices),
                ActiveIndices = new List<string>(activeIndices)
            },
            DataMetaData = _dataManager.SerializeMeta()
        };
    }

    public void SpawnSingle(SndMetaData metaData)
    {
        ((IEntityLifecycle)this).RecoverForLifecycle(metaData);
        ((IEntityLifecycle)this).FireAfterSpawnHooks();
    }

    public void LoadSingle(SndMetaData metaData)
    {
        ((IEntityLifecycle)this).RecoverForLifecycle(metaData);
        ((IEntityLifecycle)this).FireAfterLoadHooks();
    }

    public void QuitSingle()
    {
        ((IEntityLifecycle)this).FireBeforeQuitHooks();
        ((IEntityLifecycle)this).ReleaseStrategiesOnly();
        ((IEntityLifecycle)this).TeardownOnly();
        _logger.Log(LogLevel.Debug, LogTag, new LogMessageBuilder().AddContext("entityName", Name).Build("Entity quit."));
    }

    public void DeadSingle()
    {
        ((IEntityLifecycle)this).FireBeforeDeadHooks();
        ((IEntityLifecycle)this).ReleaseStrategiesOnly();
        ((IEntityLifecycle)this).TeardownOnly();
        _logger.Log(LogLevel.Debug, LogTag, new LogMessageBuilder().AddContext("entityName", Name).Build("Entity dead."));
    }

    public SndMetaData SaveSingle()
    {
        ((IEntityLifecycle)this).FireBeforeSaveHooks();
        return ((IEntityLifecycle)this).BuildMetaData();
    }

    private void SubscribeDataInternal(ISndEntity target, string dataName,
        Action<ISndEntity, ISndEntity, TypedData, TypedData> callback,
        Func<ISndEntity, ISndEntity, TypedData, TypedData, bool>? filter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataName);
        ArgumentNullException.ThrowIfNull(callback);

        var self = (ISndEntity)this;
        Action<ISndEntity, TypedData, TypedData> wrappedCb = (t, o, n) => callback(t, self, o, n);
        Func<ISndEntity, TypedData, TypedData, bool>? wrappedFilter = filter is null
            ? null
            : (t, o, n) => filter(t, self, o, n);

        ((ISndEntityRawSubscription)target).SubscribeDataRaw(dataName, wrappedCb, wrappedFilter);

        _outgoingDataSubs.Add(new OutgoingDataSub
        {
            Target = target,
            DataName = dataName,
            OriginalCallback = callback,
            WrappedCallback = wrappedCb
        });
    }

    private void SubscribeLifecycleInternal(ISndEntity target,
        Action<ISndEntity, ISndEntity, EntityLifecycleEvent> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        var self = (ISndEntity)this;
        Action<ISndEntity, EntityLifecycleEvent> wrappedCb = (t, evt) => callback(t, self, evt);

        ((ISndEntityRawSubscription)target).SubscribeLifecycleRaw(wrappedCb);

        _outgoingLifecycleSubs.Add(new OutgoingLifecycleSub
        {
            Target = target,
            OriginalCallback = callback,
            WrappedCallback = wrappedCb
        });
    }

    private void NotifyLifecycleObservers(EntityLifecycleEvent evt)
    {
        foreach (var observer in _lifecycleObservers.ToArray())
            observer(this, evt);
    }

    private struct OutgoingDataSub
    {
        public ISndEntity Target;
        public string DataName;
        public Action<ISndEntity, ISndEntity, TypedData, TypedData> OriginalCallback;
        public Action<ISndEntity, TypedData, TypedData> WrappedCallback;
    }

    private struct OutgoingLifecycleSub
    {
        public ISndEntity Target;
        public Action<ISndEntity, ISndEntity, EntityLifecycleEvent> OriginalCallback;
        public Action<ISndEntity, EntityLifecycleEvent> WrappedCallback;
    }
}
