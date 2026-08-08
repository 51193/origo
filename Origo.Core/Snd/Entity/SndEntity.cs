using Origo.Core.Abstractions.Lifecycle;
using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Node;
using Origo.Core.Logging;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;

namespace Origo.Core.Snd.Entity;

/// <summary>
///     Entity aggregate root in the SND model. Composes four internal managers
///     (data, node, passive strategy, active strategy) and holds a reference
///     to the per-scene-host <see cref="ObserverTopology" /> for observer
///     bindings.
///     <para>
///         Implements three interfaces at different visibility levels:
///         <see cref="ISndEntity" /> (business-facing),
///         <see cref="IEntityLifecycle" /> (framework-facing lifecycle hooks,
///         explicit implementation), and
///         <see cref="ISndEntityRawSubscription" /> (internal raw data
///         subscription channel for ObserverTopology, explicit implementation).
///     </para>
/// </summary>
public sealed class SndEntity : ISndEntity, IEntityLifecycle, ISndEntityRawSubscription
{
    private const string _logTag = nameof(SndEntity);
    private readonly ActiveStrategyManager _activeStrategyManager;
    private readonly ISndContext _context;
    private readonly SndDataManager _dataManager;
    private readonly ILogger _logger;
    private readonly SndNodeManager _nodeHost;
    private readonly ObserverTopology _observerTopology;
    private ISessionRun? _owningSession;
    private readonly SndStrategyManager _strategyManager;

    /// <summary>Bind the owning session after entity creation.</summary>
    internal void BindSession(ISessionRun session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _owningSession = session;
    }

    /// <summary>
    ///     Constructs the entity and creates all four internal managers.
    ///     The <paramref name="observerTopology" /> is injected by the scene
    ///     host and shared across entities within the same scene.
    /// </summary>
    internal SndEntity(
        INodeFactory nodeFactory,
        SndStrategyPool strategyPool,
        Func<string, string> sceneAliasResolver,
        ISndContext context,
        ILogger logger,
        ObserverTopology observerTopology)
    {
        ArgumentNullException.ThrowIfNull(nodeFactory);
        ArgumentNullException.ThrowIfNull(strategyPool);
        ArgumentNullException.ThrowIfNull(sceneAliasResolver);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(observerTopology);
        _context = context;
        _logger = logger;
        _observerTopology = observerTopology;

        _dataManager = new SndDataManager(this, logger);
        var nodeHost = new SndNodeManager(nodeFactory, logger);
        nodeHost.SetSceneAliasResolver(sceneAliasResolver);
        _nodeHost = nodeHost;
        _strategyManager = new SndStrategyManager(strategyPool, logger);
        _activeStrategyManager = new ActiveStrategyManager(strategyPool);
    }

    /// <summary>Unique stable name of this entity within its session.</summary>
    public string Name { get; internal set; } = string.Empty;

    /// <inheritdoc cref="ISndDataAccess.SetData{T}"/>
    public void SetData<T>(string name, T value) => _dataManager.SetData(name, value);

    /// <inheritdoc cref="ISndDataAccess.GetData{T}"/>
    public T GetData<T>(string name) where T : notnull => _dataManager.GetData<T>(name);

    /// <inheritdoc cref="ISndDataAccess.TryGetData{T}(string)"/>
    public (bool found, T? value) TryGetData<T>(string name) => _dataManager.TryGetData<T>(name);

    /// <inheritdoc cref="ISndDataAccess.TryGetData{T}(string, out T?)"/>
    public bool TryGetData<T>(string name, out T? value) => _dataManager.TryGetData<T>(name, out value);

    /// <inheritdoc cref="ISndObserverStrategyAccess.MountObserverStrategy(string, string)"/>
    public void MountObserverStrategy(string targetName, string observerIndex)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(targetName);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(observerIndex);
        var self = (ISndEntity)this;
        var target = ResolveTargetForMount(self, targetName);
        _observerTopology.Mount(self, target, observerIndex);
    }

    /// <inheritdoc cref="ISndObserverStrategyAccess.UnmountObserverStrategy(string, string)"/>
    public void UnmountObserverStrategy(string targetName, string observerIndex)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(targetName);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(observerIndex);
        var self = (ISndEntity)this;
        var target = ResolveTargetForMount(self, targetName);
        _observerTopology.Unmount(self, target, observerIndex);
    }

    /// <inheritdoc cref="ISndObserverStrategyAccess.MountObserverStrategy(ISndEntity, string)"/>
    public void MountObserverStrategy(ISndEntity target, string observerIndex)
    {
        ArgumentNullException.ThrowIfNull(target);
        _observerTopology.Mount((ISndEntity)this, target, observerIndex);
    }

    /// <inheritdoc cref="ISndObserverStrategyAccess.UnmountObserverStrategy(ISndEntity, string)"/>
    public void UnmountObserverStrategy(ISndEntity target, string observerIndex)
    {
        ArgumentNullException.ThrowIfNull(target);
        _observerTopology.Unmount((ISndEntity)this, target, observerIndex);
    }

    private static ISndEntity ResolveTargetForMount(ISndEntity self, string targetName)
    {
        if (self.Name == targetName) return self;
        throw new InvalidOperationException(
            $"Cross-entity observer resolution requires a scene host. " +
            $"Target '{targetName}' differs from self '{self.Name}'. " +
            $"Resolve the target via entity.OwningSession.FindByName(targetName), then use MountObserverStrategy(ISndEntity target, string observerIndex).");
    }

    /// <inheritdoc cref="ISndNodeAccess.GetNode"/>
    public INodeHandle GetNode(string name)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(name);
        return _nodeHost.GetNode(name);
    }

    /// <inheritdoc cref="ISndNodeAccess.GetNodeNames"/>
    public IReadOnlyCollection<string> GetNodeNames() => _nodeHost.GetNodeNames();

    /// <inheritdoc cref="ISndStrategyAccess.AddStrategy"/>
    public void AddStrategy(string index)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(index);
        _strategyManager.Add(this, index, _context);
    }

    /// <inheritdoc cref="ISndStrategyAccess.RemoveStrategy"/>
    public void RemoveStrategy(string index)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(index);
        _strategyManager.Remove(this, index, _context);
    }

    /// <inheritdoc cref="ISndActiveStrategyAccess.AddActiveStrategy"/>
    public void AddActiveStrategy(string index)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(index);
        _activeStrategyManager.Add(index);
    }

    /// <inheritdoc cref="ISndActiveStrategyAccess.RemoveActiveStrategy"/>
    public void RemoveActiveStrategy(string index)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(index);
        _activeStrategyManager.Remove(index);
    }

    /// <inheritdoc cref="ISndActiveStrategyAccess.InvokeStrategy"/>
    public object? InvokeStrategy(string strategyIndex, object? input = null)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(strategyIndex);
        return _activeStrategyManager.Invoke(this, _context, strategyIndex, input);
    }

    /// <summary>
    ///     Whether this entity has been marked for removal at the end of
    ///     the current frame.
    /// </summary>
    public bool IsPendingKill { get; internal set; }
    /// <summary>
    ///     The session this entity belongs to. Throws if accessed before
    ///     <see cref="BindSession" /> has been called.
    /// </summary>
    public ISessionRun OwningSession => _owningSession ?? throw new InvalidOperationException("Entity is not bound to a session. OwningSession must be set before the entity is used.");

    internal void Process(double delta) => _strategyManager.Process(this, delta, _context);

    internal bool HasStrategyMounted(string index) => _strategyManager.HasMounted(index);

    void ISndEntityRawSubscription.SubscribeDataRaw(string name, Action<ISndEntity, TypedData, TypedData> callback,
        Func<ISndEntity, TypedData, TypedData, bool>? filter) => _dataManager.Subscribe(name, callback, filter);

    void ISndEntityRawSubscription.UnsubscribeDataRaw(string name, Action<ISndEntity, TypedData, TypedData> callback) => _dataManager.Unsubscribe(name, callback);

    void IEntityLifecycle.RecoverForLifecycle(SndMetaData metaData)
    {
        try
        {
            Name = metaData.Name;
            _dataManager.Recover(metaData.DataMetaData ??
                                 throw new InvalidOperationException("DataMetaData is required."));
            _nodeHost.Recover(metaData.NodeMetaData ??
                              throw new InvalidOperationException("NodeMetaData is required."));
            if (metaData.StrategyMetaData is null)
                throw new InvalidOperationException("StrategyMetaData is required during entity recovery.");
            _strategyManager.RecoverStrategiesOnly(metaData.StrategyMetaData.LifecycleIndices);
            _activeStrategyManager.Recover(metaData.StrategyMetaData.ActiveIndices);
        }
        catch
        {
            // Each sub-manager rolls back its own partial state, but a failure
            // in a later phase (e.g. active strategy recovery) leaves the
            // acquisitions of earlier phases (passive strategies, nodes) in
            // place. Release everything before rethrowing so no global pool
            // reference or node handle leaks, regardless of which scene host
            // invoked this method. All releases are idempotent.
            _activeStrategyManager.ReleaseAll();
            _strategyManager.ReleaseStrategiesOnly();
            _nodeHost.Release();
            throw;
        }
    }

    void IEntityLifecycle.FireAfterSpawnHooks()
    {
        _strategyManager.TriggerAfterSpawn(this, _context);
        _logger.Log(LogLevel.Debug, _logTag,
            new LogMessageBuilder().AddContext("entityName", Name).Build("Entity spawned (hooks)."));
    }

    void IEntityLifecycle.FireAfterLoadHooks()
    {
        _strategyManager.TriggerAfterLoad(this, _context);
        _logger.Log(LogLevel.Debug, _logTag,
            new LogMessageBuilder().AddContext("entityName", Name).Build("Entity loaded (hooks)."));
    }

    void IEntityLifecycle.FireBeforeSaveHooks() => _strategyManager.TriggerBeforeSave(this, _context);

    void IEntityLifecycle.FireBeforeQuitHooks() => _strategyManager.TriggerBeforeQuit(this, _context);

    void IEntityLifecycle.FireBeforeDeadHooks() => _strategyManager.TriggerBeforeDead(this, _context);

    void IEntityLifecycle.ReleaseStrategiesOnly()
    {
        _activeStrategyManager.ReleaseAll();
        _strategyManager.ReleaseStrategiesOnly();
        _observerTopology.ReleaseStrategiesFor((ISndEntity)this);
    }

    void IEntityLifecycle.TeardownOnly()
    {
        _nodeHost.Release();
        _dataManager.Release();
    }

    void IEntityLifecycle.TeardownObserverBindings() =>
        _observerTopology.TeardownAllBindingsFor((ISndEntity)this);

    SndMetaData IEntityLifecycle.BuildMetaData()
    {
        var lifecycleIndices = _strategyManager.GetStrategyIndices();
        var activeIndices = _activeStrategyManager.SerializeIndices();

        return new SndMetaData
        {
            Name = Name,
            NodeMetaData = _nodeHost.SerializeMetaData(),
            StrategyMetaData = new StrategyMetaData
            {
                LifecycleIndices = [.. lifecycleIndices],
                ActiveIndices = [.. activeIndices],
                ObserverIndices = [.. _observerTopology.BuildBindingsFor(Name)]
            },
            DataMetaData = _dataManager.SerializeMeta()
        };
    }
}
