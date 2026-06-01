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
public sealed class SndEntity : ISndEntity
{
    private const string LogTag = nameof(SndEntity);
    private readonly ActiveStrategyManager _activeStrategyManager;
    private readonly ISndContext _context;
    private readonly SndDataManager _dataManager;
    private readonly ILogger _logger;
    private readonly SndNodeManager _nodeHost;
    private readonly SndStrategyManager _strategyManager;

    internal SndEntity(
        INodeFactory nodeFactory,
        SndStrategyPool strategyPool,
        SndMappings mappings,
        ISndContext context,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(nodeFactory);
        ArgumentNullException.ThrowIfNull(strategyPool);
        ArgumentNullException.ThrowIfNull(mappings);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        _context = context;
        _logger = logger;

        _dataManager = new SndDataManager(this, logger);
        _nodeHost = new SndNodeManager(nodeFactory, mappings, logger);
        _strategyManager = new SndStrategyManager(strategyPool, logger);
        _activeStrategyManager = new ActiveStrategyManager(strategyPool);
    }

    public string Name { get; internal set; } = string.Empty;

    public void SetData<T>(string name, T value) => _dataManager.SetData(name, value);

    public T GetData<T>(string name) => _dataManager.GetData<T>(name);

    public (bool found, T? value) TryGetData<T>(string name) => _dataManager.TryGetData<T>(name);

    public void Subscribe(string name, Action<ISndEntity, object?, object?> callback,
        Func<ISndEntity, object?, object?, bool>? filter = null) =>
        _dataManager.Subscribe(name, callback, filter);

    public void Unsubscribe(string name, Action<ISndEntity, object?, object?> callback) =>
        _dataManager.Unsubscribe(name, callback);

    public INodeHandle GetNode(string name) => _nodeHost.GetNode(name);

    public IReadOnlyCollection<string> GetNodeNames() => _nodeHost.GetNodeNames();

    public void AddStrategy(string index) => _strategyManager.Add(this, index, _context);

    public void RemoveStrategy(string index) => _strategyManager.Remove(this, index, _context);

    public void AddActiveStrategy(string index)
    {
        _activeStrategyManager.Add(index);
    }

    public void RemoveActiveStrategy(string index)
    {
        _activeStrategyManager.Remove(index);
    }

    public object? InvokeStrategy(string strategyIndex, object? input = null)
    {
        return _activeStrategyManager.Invoke(this, _context, strategyIndex, input);
    }

    public void Load(SndMetaData metaData)
    {
        RecoverFromMetaData(metaData);
        _strategyManager.Load(
            metaData.StrategyMetaData?.EntityIndices ?? Enumerable.Empty<string>(), this, _context);
        _activeStrategyManager.Recover(
            metaData.StrategyMetaData?.ActiveIndices ?? Enumerable.Empty<string>());
        _logger.Log(LogLevel.Info, LogTag,
            new LogMessageBuilder().AddSuffix("entityName", Name).Build("Entity loaded."));
    }

    public void Spawn(SndMetaData metaData)
    {
        RecoverFromMetaData(metaData);
        _strategyManager.Spawn(
            metaData.StrategyMetaData?.EntityIndices ?? Enumerable.Empty<string>(), this, _context);
        _activeStrategyManager.Recover(
            metaData.StrategyMetaData?.ActiveIndices ?? Enumerable.Empty<string>());
        _logger.Log(LogLevel.Info, LogTag,
            new LogMessageBuilder().AddSuffix("entityName", Name).Build("Entity spawned."));
    }

    public void Quit()
    {
        _activeStrategyManager.ReleaseAll();
        _strategyManager.Quit(this, _context);
        Teardown();
        _logger.Log(LogLevel.Info, LogTag, new LogMessageBuilder().AddSuffix("entityName", Name).Build("Entity quit."));
    }

    public void Dead()
    {
        _activeStrategyManager.ReleaseAll();
        _strategyManager.Dead(this, _context);
        Teardown();
        _logger.Log(LogLevel.Info, LogTag, new LogMessageBuilder().AddSuffix("entityName", Name).Build("Entity dead."));
    }

    public SndMetaData SerializeMetaData()
    {
        var entityIndices = _strategyManager.SerializeIndices(this, _context);
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

    public void Process(double delta) => _strategyManager.Process(this, delta, _context);

    public bool IsPendingKill { get; internal set; }

    private void RecoverFromMetaData(SndMetaData metaData)
    {
        Name = metaData.Name;
        _dataManager.Recover(metaData.DataMetaData ??
                             throw new InvalidOperationException("DataMetaData is required."));
        _nodeHost.Recover(metaData.NodeMetaData ??
                          throw new InvalidOperationException("NodeMetaData is required."));
    }

    private void Teardown()
    {
        _nodeHost.Release();
        _dataManager.Release();
    }
}
