using Origo.Core.Abstractions.Lifecycle;
using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Node;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;

namespace Origo.TestSupport;

/// <summary>
///     Pure in-memory <see cref="ISndSceneHost" /> implementation that does not depend
///     on any engine adapter layer. Used by <see cref="LevelBuilder" /> and for
///     fully in-memory scene hosts needed in unit tests.
/// </summary>
internal sealed class StubSndSceneHost : ISndSceneHost, IOwningSessionBindable
{
    private readonly List<ISndEntity> _entities = [];
    private readonly List<SndMetaData> _metaList = [];
    private ISessionRun? _owningSession;

    public void SetOwningSession(ISessionRun session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _owningSession = session;
    }

    public ISndEntity CreateEntity(SndMetaData metaData)
    {
        ArgumentNullException.ThrowIfNull(metaData);
        _metaList.Add(metaData);
        var entity = new StubSndEntity(metaData.Name) { OwningSession = _owningSession! };
        _entities.Add(entity);
        return entity;
    }

    // Returns a snapshot so callers iterating while the host is mutated do
    // not hit "collection was modified" (consistent with FullMemorySndSceneHost).
    /// <inheritdoc/>
    public IReadOnlyCollection<ISndEntity> GetEntities() => [.. _entities];

    /// <inheritdoc/>
    public ISndEntity? FindByName(string name) =>
        _entities.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));

    public IReadOnlyList<SndMetaData> BuildMetaList() => [.. _metaList];

    public void RecoverFromMetaList(IEnumerable<SndMetaData> metaList)
    {
        ArgumentNullException.ThrowIfNull(metaList);
        // Per the ISndSceneAccess contract, recovery must not clear existing
        // entities; the caller handles old-entity cleanup before invoking this
        // method (consistent with FullMemorySndSceneHost).
        foreach (var meta in metaList)
        {
            _metaList.Add(meta);
            _entities.Add(new StubSndEntity(meta.Name) { OwningSession = _owningSession! });
        }
    }

    public void RemoveAllEntities()
    {
        _metaList.Clear();
        _entities.Clear();
    }

    public void ProcessAll(double delta)
    {
    }

    public void RemoveEntity(string name)
    {
        var index = _entities.FindIndex(e => string.Equals(e.Name, name, StringComparison.Ordinal));
        if (index < 0)
            throw new InvalidOperationException($"No entity with name '{name}'.");

        _entities.RemoveAt(index);
        _metaList.RemoveAt(index);
    }

    public void RequestKillEntity(string name)
    {
        var entity = _entities.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));
        if (entity is not StubSndEntity memEntity)
            throw new InvalidOperationException($"No entity with name '{name}'.");
        if (memEntity.IsPendingKill)
            throw new InvalidOperationException($"Entity '{name}' is already pending kill.");
        memEntity.IsPendingKill = true;
    }
}

/// <summary>
///     Lightweight <see cref="ISndEntity" /> and raw subscription test
///     implementation for the in-memory stub scene host. It stores data and
///     observer subscriptions directly without strategy or node managers.
/// </summary>
internal sealed class StubSndEntity : ISndEntity, ISndEntityRawSubscription
{
    private readonly Dictionary<string, object?> _data = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<(Action<ISndEntity, TypedData, TypedData> Original, Action<TypedData, TypedData> Wrapped)>> _subscriptions = new(StringComparer.Ordinal);

    public StubSndEntity(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _data["name"] = name;
    }

    private ISessionRun? _owningSession;

    /// <inheritdoc/>
    public ISessionRun OwningSession
    {
        get => _owningSession ?? throw new InvalidOperationException(
            "OwningSession has not been bound. Entities must be created through a scene host that implements IOwningSessionBindable.");
        set => _owningSession = value;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public void SetData<T>(string name, T value) => _data[name] = value;

    /// <inheritdoc/>
    public T GetData<T>(string name) where T : notnull
    {
        if (!_data.TryGetValue(name, out var value))
            throw new InvalidOperationException(
                $"Data key '{name}' not found in StubSndEntity '{Name}'.");
        if (value is T cast)
            return cast;
        throw new InvalidOperationException(
            $"Data key '{name}' is of type '{value?.GetType().Name ?? "null"}' but requested as '{typeof(T).Name}'.");
    }

    /// <inheritdoc/>
    public (bool found, T? value) TryGetData<T>(string name)
    {
        if (_data.TryGetValue(name, out var value) && value is T cast)
            return (true, cast);
        return (false, default);
    }

    /// <inheritdoc/>
    public bool TryGetData<T>(string name, out T? value)
    {
        if (_data.TryGetValue(name, out var stored) && stored is T cast)
        {
            value = cast;
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc/>
    public void MountObserverStrategy(string targetName, string observerIndex) { }

    /// <inheritdoc/>
    public void UnmountObserverStrategy(string targetName, string observerIndex) { }
    /// <inheritdoc/>
    public void MountObserverStrategy(Origo.Core.Abstractions.Entity.ISndEntity target, string observerIndex) { }
    /// <inheritdoc/>
    public void UnmountObserverStrategy(Origo.Core.Abstractions.Entity.ISndEntity target, string observerIndex) { }

    /// <inheritdoc/>
    public INodeHandle GetNode(string name)
    {
        throw new InvalidOperationException(
            $"StubSndEntity does not support node access. Node '{name}' requested.");
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<string> GetNodeNames() => [];

    /// <inheritdoc/>
    public void AddStrategy(string index)
    {
    }

    /// <inheritdoc/>
    public void RemoveStrategy(string index)
    {
    }

    /// <inheritdoc/>
    public void AddActiveStrategy(string index)
    {
    }

    /// <inheritdoc/>
    public void RemoveActiveStrategy(string index)
    {
    }

    /// <inheritdoc/>
    public object? InvokeStrategy(string strategyIndex, object? input = null) => null;

    /// <inheritdoc/>
    public bool IsPendingKill { get; set; }

    void ISndEntityRawSubscription.SubscribeDataRaw(string name, Action<ISndEntity, TypedData, TypedData> callback,
        Func<ISndEntity, TypedData, TypedData, bool>? filter)
    {
        var wrapped = new Action<TypedData, TypedData>((o, n) => callback(this, o, n));
        if (!_subscriptions.TryGetValue(name, out var list))
            _subscriptions[name] = list = [];
        list.Add((callback, wrapped));
    }

    void ISndEntityRawSubscription.UnsubscribeDataRaw(string name, Action<ISndEntity, TypedData, TypedData> callback)
    {
        if (!_subscriptions.TryGetValue(name, out var list)) return;
        list.RemoveAll(p => p.Original == callback);
        if (list.Count == 0) _subscriptions.Remove(name);
    }
}
