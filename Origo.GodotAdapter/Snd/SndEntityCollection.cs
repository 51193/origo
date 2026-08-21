using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Snd.Metadata;

namespace Origo.GodotAdapter.Snd;

/// <summary>
///     Contract for the entity operations the Godot scene host drives,
///     implemented by <see cref="GodotSndEntity" />. Kept as an interface so
///     <see cref="SndEntityCollection" /> (pure C#, no engine dependency) can
///     be unit-tested without instantiating Godot nodes.
/// </summary>
internal interface ISndEntityFacade : ISndEntity
{
    string StableName { get; }

    SndMetaData BuildSndMetaData();

    void RecoverForLifecycle(SndMetaData meta);

    void BindSession(ISessionRun session);

    void ProcessSnd(double delta);

    void DetachFromManager();

    void RollbackAcquiredResources();

    void MarkPendingKill();
}

/// <summary>
///     Pure-C# entity collection backing <see cref="GodotSndManager" />: add,
///     find, remove, kill, frame processing, meta-list round trips, and
///     staged rollback on partial load failure. Engine-level work (node
///     creation, RemoveChild/Free) is delegated to injected callbacks so
///     this class stays testable without a Godot runtime.
/// </summary>
internal sealed class SndEntityCollection<T> : IReadOnlyCollection<ISndEntity>
    where T : class, ISndEntity, ISndEntityFacade
{
    private readonly List<T> _entities = [];
    private readonly Func<T> _entityFactory;
    private readonly Action<T>? _detachCallback;

    public SndEntityCollection(Func<T> entityFactory, Action<T>? detachCallback = null)
    {
        ArgumentNullException.ThrowIfNull(entityFactory);
        _entityFactory = entityFactory;
        _detachCallback = detachCallback;
    }

    /// <summary>
    ///     Session every staged entity is bound to on creation; set by the
    ///     hosting scene manager when a session binds itself.
    /// </summary>
    public ISessionRun? OwningSession { get; set; }

    /// <inheritdoc/>
    public int Count => _entities.Count;

    public IReadOnlyList<SndMetaData> BuildMetaList()
    {
        var list = new List<SndMetaData>(_entities.Count);
        for (var i = 0; i < _entities.Count; i++)
            list.Add(_entities[i].BuildSndMetaData());
        return list;
    }

    public void RecoverFromMetaList(IEnumerable<SndMetaData> metaList,
        Action<SndMetaData, Exception>? onFailure = null)
    {
        ArgumentNullException.ThrowIfNull(metaList);
        var staged = new List<T>();
        SndMetaData? failingMeta = null;
        try
        {
            foreach (var meta in metaList)
            {
                failingMeta = meta;
                CreateAndStage(meta, staged);
            }
        }
        catch (Exception ex)
        {
            RollbackPartialLoad(
                staged,
                ex,
                onFailure: failingMeta is not null ? onFailure : null,
                failingMeta: failingMeta);
            throw;
        }
    }

    public void RemoveAllEntities()
    {
        for (var i = _entities.Count - 1; i >= 0; i--)
        {
            var entity = _entities[i];
            entity.DetachFromManager();
            _detachCallback?.Invoke(entity);
        }
        _entities.Clear();
    }

    public ISndEntity CreateEntity(SndMetaData metaData)
    {
        ArgumentNullException.ThrowIfNull(metaData);
        var staged = new List<T>();
        try
        {
            return CreateAndStage(metaData, staged);
        }
        catch (Exception ex)
        {
            RollbackPartialLoad(staged, ex);
            throw;
        }
    }

    /// <summary>
    ///     Returns a snapshot of the currently alive entities. A snapshot (not
    ///     a live view) matches the Core scene hosts' contract: callers that
    ///     iterate while the host is mutated do not hit "collection was
    ///     modified", and the result cannot be downcast to the mutable backing
    ///     list to bypass collection management.
    /// </summary>
    public IReadOnlyCollection<ISndEntity> GetEntities() => [.. _entities];

    public ISndEntity? FindByName(string name)
    {
        for (var i = 0; i < _entities.Count; i++)
        {
            var entity = _entities[i];
            if (entity.StableName == name)
                return entity;
        }
        return null;
    }

    public void ProcessAll(double delta)
    {
        // Matches FullMemorySndSceneHost: the host container must not be
        // mutated while entities process. A strategy that spawns/removes
        // entities mid-frame would otherwise make the index loop skip or
        // double-process entities silently.
        var initialCount = _entities.Count;
        for (var i = 0; i < _entities.Count; i++)
            _entities[i].ProcessSnd(delta);

        if (_entities.Count != initialCount)
            throw new InvalidOperationException(
                $"Scene container modified during ProcessAll: entity count changed from {initialCount} to {_entities.Count}. " +
                "The host must not be mutated while entities process.");
    }

    public void RemoveEntity(string name)
    {
        for (var i = 0; i < _entities.Count; i++)
        {
            var entity = _entities[i];
            if (entity.StableName != name)
                continue;

            _entities.RemoveAt(i);
            entity.DetachFromManager();
            _detachCallback?.Invoke(entity);
            return;
        }

        throw new InvalidOperationException($"No entity with StableName '{name}'.");
    }

    public void RequestKillEntity(string name)
    {
        var entity = FindAsT(name);
        if (entity.IsPendingKill)
            throw new InvalidOperationException($"Entity '{name}' is already pending kill.");
        entity.MarkPendingKill();
    }

    private T CreateAndStage(SndMetaData meta, List<T> staged)
    {
        var entity = _entityFactory();
        _entities.Add(entity);
        staged.Add(entity);
        entity.RecoverForLifecycle(meta);
        if (OwningSession is not null)
            entity.BindSession(OwningSession);
        return entity;
    }

    private T FindAsT(string name)
    {
        for (var i = 0; i < _entities.Count; i++)
        {
            var entity = _entities[i];
            if (entity.StableName == name)
                return entity;
        }

        throw new InvalidOperationException($"No entity with StableName '{name}'.");
    }

    private void RollbackPartialLoad(
        List<T> staged,
        Exception originalException,
        Action<SndMetaData, Exception>? onFailure = null,
        SndMetaData? failingMeta = null)
    {
        var cleanupFailures = new List<Exception>();
        for (var i = staged.Count - 1; i >= 0; i--)
        {
            var entity = staged[i];
            RunCleanupStep(entity.RollbackAcquiredResources, cleanupFailures);
            RunCleanupStep(() => _entities.Remove(entity), cleanupFailures);
            RunCleanupStep(entity.DetachFromManager, cleanupFailures);
            RunCleanupStep(() => _detachCallback?.Invoke(entity), cleanupFailures);
        }

        if (onFailure is not null && failingMeta is not null)
            RunCleanupStep(() => onFailure(failingMeta, originalException), cleanupFailures);

        if (cleanupFailures.Count == 0)
        {
            ExceptionDispatchInfo.Capture(originalException).Throw();
        }

        throw new AggregateException(
            "Entity recovery failed and rollback cleanup also failed; see inner exceptions.",
            [originalException, .. cleanupFailures]);
    }

    private static void RunCleanupStep(Action step, List<Exception> failures)
    {
        try
        {
            step();
        }
        catch (Exception ex)
        {
            failures.Add(ex);
        }
    }

    /// <inheritdoc/>
    public IEnumerator<ISndEntity> GetEnumerator()
    {
        for (var i = 0; i < _entities.Count; i++)
            yield return _entities[i];
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
