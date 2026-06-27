using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Godot;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;

namespace Origo.GodotAdapter.Snd;

[GlobalClass]
public partial class GodotSndManager
    : Node, ISndSceneHost, ISndContextAttachableSceneHost, IObserverTopologyHost, IOwningSessionBindable
{
    private readonly List<GodotSndEntity> _entities = new();
    private EntityView? _entityView;
    private ObserverTopology? _observerTopology;
    private ISessionRun? _owningSession;

    private bool _runtimeDepsBound;

    public SndWorld SharedWorld { get; private set; } = null!;
    public ILogger SharedLogger { get; private set; } = null!;
    public ISndContext? Context { get; private set; }
    public int ProcessTickCount { get; private set; }
    public double ProcessDeltaSum { get; private set; }

    ObserverTopology IObserverTopologyHost.ObserverTopology =>
        _observerTopology ?? throw new InvalidOperationException(
            "ObserverTopology is not available. Call BindRuntimeDependencies before accessing the observer topology.");

    void IOwningSessionBindable.SetOwningSession(ISessionRun session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _owningSession = session;
    }
    public void BindContext(ISndContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_runtimeDepsBound) throw new InvalidOperationException("Call BindRuntimeDependencies before BindContext.");

        Context = context;
        _observerTopology!.BindContext(context);
    }

    public IReadOnlyList<SndMetaData> BuildMetaList()
    {
        var list = new List<SndMetaData>(_entities.Count);
        for (var i = 0; i < _entities.Count; i++)
            list.Add(_entities[i].BuildSndMetaData());
        return list;
    }

    public void RecoverFromMetaList(IEnumerable<SndMetaData> metaList)
    {
        ArgumentNullException.ThrowIfNull(metaList);
        var staged = new List<GodotSndEntity>();
        foreach (var meta in metaList)
        {
            GodotSndEntity? snd = null;
            try
            {
                snd = CreateSndEntity();
                AddChild(snd);
                _entities.Add(snd);
                staged.Add(snd);
                snd.RecoverForLifecycle(meta);
                        if (_owningSession is not null)
                snd.BindSession(_owningSession);
            }
            catch
            {
                RollbackPartialLoad(staged);
                if (snd is not null && IsInstanceValid(snd))
                {
                    _entities.Remove(snd);
                    if (snd.GetParent() == this)
                        RemoveChild(snd);
                    snd.Free();
                }

                throw;
            }
        }
    }

    public void RemoveAllEntities()
    {
        for (var i = _entities.Count - 1; i >= 0; i--)
            _entities[i].DetachFromManager();
        _entities.Clear();
        _entityView = null;
    }

    public ISndEntity CreateEntity(SndMetaData metaData)
    {
        var staged = new List<GodotSndEntity>();
        try
        {
            var snd = CreateSndEntity();
            AddChild(snd);
            _entities.Add(snd);
            staged.Add(snd);
            snd.RecoverForLifecycle(metaData);
                        if (_owningSession is not null)
                snd.BindSession(_owningSession);
            return snd;
        }
        catch
        {
            RollbackPartialLoad(staged);
            throw;
        }
    }

    public IReadOnlyCollection<ISndEntity> GetEntities() => _entityView ??= new EntityView(_entities);

    public ISndEntity? FindByName(string name)
    {
        var entity = _entities.FirstOrDefault(s => s.StableName == name);
        return entity;
    }

    public void ProcessAll(double delta)
    {
        ProcessTickCount++;
        ProcessDeltaSum += delta;
        var snapshot = _entities.ToArray();
        foreach (var entity in snapshot)
            entity.ProcessSnd(delta);
    }

    public void RemoveEntity(string name)
    {
        var snd = _entities.FirstOrDefault(s => s.StableName == name);
        if (snd is null)
            throw new InvalidOperationException($"No entity with StableName '{name}'.");

        _entities.Remove(snd);
        snd.DetachFromManager();
    }

    public void RequestKillEntity(string name)
    {
        var snd = _entities.FirstOrDefault(s => s.StableName == name);
        if (snd is null)
            throw new InvalidOperationException($"No entity with StableName '{name}'.");

        if (snd.IsPendingKill)
            throw new InvalidOperationException($"Entity '{name}' is already pending kill.");

        snd.MarkPendingKill();
    }

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

    private void RollbackPartialLoad(List<GodotSndEntity> staged)
    {
        for (var i = staged.Count - 1; i >= 0; i--)
        {
            var s = staged[i];
            _entities.Remove(s);
            if (IsInstanceValid(s) && s.GetParent() == this)
                RemoveChild(s);
            if (IsInstanceValid(s))
                s.Free();
        }
    }

    private GodotSndEntity CreateSndEntity()
    {
        EnsureReadyForSpawn();
        return new GodotSndEntity(SharedWorld, Context!, SharedLogger, _observerTopology!,
            entity => new GodotPackedSceneNodeFactory(entity));
    }

    private void EnsureReadyForSpawn()
    {
        if (!_runtimeDepsBound || Context is null)
            throw new InvalidOperationException(
                "GodotSndManager is not ready: call BindRuntimeDependencies and BindContext before spawning entities.");
    }

    private sealed class EntityView(List<GodotSndEntity> inner) : IReadOnlyList<ISndEntity>
    {
        public int Count => inner.Count;

        public ISndEntity this[int index] => inner[index];

        public IEnumerator<ISndEntity> GetEnumerator()
        {
            for (var i = 0; i < inner.Count; i++)
                yield return inner[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
