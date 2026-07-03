using Origo.Core.Abstractions.Lifecycle;
using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Node;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd.Scene;

/// <summary>
///     纯内存 <see cref="ISndSceneHost" /> 实现，不依赖任何引擎适配层。
///     用于 <see cref="LevelBuilder" /> 等 Core 层离线构建关卡场景，
///     以及单元测试中需要完全内存化的场景宿主。
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

    public IReadOnlyCollection<ISndEntity> GetEntities() => _entities;

    public ISndEntity? FindByName(string name) =>
        _entities.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));

    public IReadOnlyList<SndMetaData> BuildMetaList() => [.. _metaList];

    public void RecoverFromMetaList(IEnumerable<SndMetaData> metaList)
    {
        ArgumentNullException.ThrowIfNull(metaList);
        _metaList.Clear();
        _entities.Clear();
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
            return;

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

    public ISessionRun OwningSession
    {
        get => _owningSession ?? throw new InvalidOperationException(
            "OwningSession has not been bound. Entities must be created through a scene host that implements IOwningSessionBindable.");
        set => _owningSession = value;
    }

    public string Name { get; }

    public void SetData<T>(string name, T value) => _data[name] = value;

    public T GetData<T>(string name)
    {
        if (!_data.TryGetValue(name, out var value))
            throw new KeyNotFoundException($"Data key '{name}' not found in StubSndSceneHost.");
        if (value is T cast)
            return cast;
        throw new InvalidCastException(
            $"Data key '{name}' is of type '{value?.GetType().Name ?? "null"}' but requested as '{typeof(T).Name}'.");
    }

    public (bool found, T? value) TryGetData<T>(string name)
    {
        if (_data.TryGetValue(name, out var value) && value is T cast)
            return (true, cast);
        return (false, default);
    }

    public void MountObserverStrategy(string targetName, string observerIndex) { }

    public void UnmountObserverStrategy(string targetName, string observerIndex) { }
    public void MountObserverStrategy(Origo.Core.Abstractions.Entity.ISndEntity target, string observerIndex) { }
    public void UnmountObserverStrategy(Origo.Core.Abstractions.Entity.ISndEntity target, string observerIndex) { }

    public INodeHandle GetNode(string name)
    {
        throw new InvalidOperationException(
            $"StubSndEntity does not support node access. Node '{name}' requested.");
    }

    public IReadOnlyCollection<string> GetNodeNames() => [];

    public void AddStrategy(string index)
    {
    }

    public void RemoveStrategy(string index)
    {
    }

    public void AddActiveStrategy(string index)
    {
    }

    public void RemoveActiveStrategy(string index)
    {
    }

    public object? InvokeStrategy(string strategyIndex, object? input = null) => null;

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
