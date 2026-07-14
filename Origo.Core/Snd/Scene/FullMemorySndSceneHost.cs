using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;

namespace Origo.Core.Snd.Scene;

internal sealed class FullMemorySndSceneHost
    : ISndSceneHost, ISndContextAttachableSceneHost, IObserverTopologyHost, IOwningSessionBindable
{
    private readonly List<MemoryEntityEntry> _entries = [];
    private readonly ILogger _logger;
    private readonly NullNodeFactory _nodeFactory = new();
    private ISndContext? _context;
    private ObserverTopology? _observerTopology;
    private ISessionRun? _owningSession;
    private SndWorld? _world;

    public FullMemorySndSceneHost(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public ObserverTopology ObserverTopology =>
        _observerTopology ?? throw new InvalidOperationException(
            "ObserverTopology is not available. Call BindWorld before accessing the observer topology.");

    public void BindContext(ISndContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _observerTopology?.BindContext(context);
    }

    public void SetOwningSession(ISessionRun session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _owningSession = session;
    }

    public ISndEntity CreateEntity(SndMetaData metaData)
    {
        ArgumentNullException.ThrowIfNull(metaData);
        EnsureReady();
        return CreateAndRecover(metaData);
    }

    public IReadOnlyCollection<ISndEntity> GetEntities() => [.. _entries.Select(e => (ISndEntity)e.Entity)];

    public ISndEntity? FindByName(string name)
    {
        return _entries.FirstOrDefault(e =>
            string.Equals(e.Entity.Name, name, StringComparison.Ordinal))?.Entity;
    }

    public IReadOnlyList<SndMetaData> BuildMetaList()
    {
        var list = new List<SndMetaData>(_entries.Count);
        foreach (var entry in _entries)
            list.Add(((IEntityLifecycle)entry.Entity).BuildMetaData());
        return list;
    }

    public void RecoverFromMetaList(IEnumerable<SndMetaData> metaList)
    {
        ArgumentNullException.ThrowIfNull(metaList);
        EnsureReady();

        foreach (var meta in metaList)
            CreateAndRecover(meta);
    }

    public void RemoveAllEntities() => _entries.Clear();

    public void RemoveEntity(string name)
    {
        var index = _entries.FindIndex(e =>
            string.Equals(e.Entity.Name, name, StringComparison.Ordinal));
        if (index < 0)
            throw new InvalidOperationException($"No entity with name '{name}'.");

        _entries.RemoveAt(index);
    }

    public void RequestKillEntity(string name)
    {
        var index = _entries.FindIndex(e =>
            string.Equals(e.Entity.Name, name, StringComparison.Ordinal));
        if (index < 0)
            throw new InvalidOperationException($"No entity with name '{name}'.");

        var entry = _entries[index];
        if (entry.Entity.IsPendingKill)
            throw new InvalidOperationException($"Entity '{name}' is already pending kill.");

        entry.Entity.IsPendingKill = true;
    }

    public void ProcessAll(double delta)
    {
        for (var i = 0; i < _entries.Count; i++)
            _entries[i].Entity.Process(delta);
    }

    internal void BindWorld(SndWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        _world = world;
        _observerTopology = new ObserverTopology(world.StrategyPool, _logger);
        if (_context is not null)
            _observerTopology.BindContext(_context);
    }

    private SndEntity CreateAndRecover(SndMetaData metaData)
    {
        var entity = _world!.CreateEntity(_nodeFactory, _context!, _logger, _observerTopology!);
        entity.Name = metaData.Name;
        _entries.Add(new MemoryEntityEntry(entity));
        ((IEntityLifecycle)entity).RecoverForLifecycle(metaData);
        if (_owningSession is not null)
            entity.BindSession(_owningSession);
        return entity;
    }

    private void EnsureReady()
    {
        if (_world is null)
            throw new InvalidOperationException(
                "SndWorld is not bound. Call BindWorld before spawning or loading entities.");
        if (_context is null)
            throw new InvalidOperationException(
                "ISndContext is not bound. Call BindContext before spawning or loading entities.");
    }

    private sealed class MemoryEntityEntry(SndEntity entity)
    {
        public SndEntity Entity { get; } = entity;
    }
}
