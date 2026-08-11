using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Node;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Snd.Metadata;

namespace Origo.TestSupport;

public sealed class TestSndSceneHost : ISndSceneHost
{
    private readonly List<ISndEntity> _entities = [];
    private readonly List<SndMetaData> _metaList = [];
    public int ClearAllCount { get; private set; }

    public ISndEntity CreateEntity(SndMetaData metaData)
    {
        _metaList.Add(metaData);
        var entity = new DummySndEntity(metaData.Name);
        _entities.Add(entity);
        return entity;
    }

    public IReadOnlyCollection<ISndEntity> GetEntities() => [.. _entities];

    public ISndEntity? FindByName(string name) => _entities.FirstOrDefault(e => e.Name == name);

    public IReadOnlyList<SndMetaData> BuildMetaList() => [.. _metaList];

    public void RecoverFromMetaList(IEnumerable<SndMetaData> metaList)
    {
        // Matches the production scene-host contract (SndEntityCollection /
        // FullMemorySndSceneHost): recovery appends to the existing scene and
        // does not automatically clear it. Callers handle cleanup.
        foreach (var meta in metaList)
        {
            _metaList.Add(meta);
            _entities.Add(new DummySndEntity(meta.Name));
        }
    }

    public void RemoveAllEntities()
    {
        ClearAllCount++;
        _metaList.Clear();
        _entities.Clear();
    }

    public void ProcessAll(double delta)
    {
    }

    public void RemoveEntity(string name)
    {
        // Matches the production scene-host contract (SndEntityCollection):
        // removing an unknown entity fails instead of silently no-opping.
        var entity = _entities.FirstOrDefault(e => e.Name == name)
                     ?? throw new InvalidOperationException($"No entity with name '{name}'.");
        _entities.Remove(entity);

        // Keep the metadata view in sync with the entity view so
        // BuildMetaList never returns removed entities.
        var meta = _metaList.FirstOrDefault(m => m.Name == name);
        if (meta is not null)
            _metaList.Remove(meta);
    }

    public void RequestKillEntity(string name)
    {
        var entity = _entities.FirstOrDefault(e => e.Name == name);
        if (entity is not DummySndEntity testEntity)
            throw new InvalidOperationException($"No entity with name '{name}'.");
        if (testEntity.IsPendingKill)
            throw new InvalidOperationException($"Entity '{name}' is already pending kill.");
        testEntity.IsPendingKill = true;
    }
}

public sealed class DummySndEntity : ISndEntity
{
    public ISessionRun OwningSession { get; set; } = null!;

    private readonly Dictionary<string, object?> _data = new(StringComparer.Ordinal);
    public readonly string EntityName;

    public DummySndEntity(string entityName)
    {
        EntityName = entityName;
        _data["name"] = entityName;
    }

    public string Name => EntityName;

    public bool IsPendingKill { get; set; }

    public void SetData<T>(string name, T value) => _data[name] = value;

    public T GetData<T>(string name) where T : notnull
    {
        if (!_data.TryGetValue(name, out var value))
            throw new InvalidOperationException($"Data key '{name}' not found in DummySndEntity '{Name}'.");
        if (value is T cast)
            return cast;
        throw new InvalidOperationException(
            $"Data key '{name}' is of type '{value?.GetType().Name ?? "null"}' but requested as '{typeof(T).Name}'.");
    }

    public (bool found, T? value) TryGetData<T>(string name)
    {
        if (_data.TryGetValue(name, out var value) && value is T cast)
            return (true, cast);
        return (false, default);
    }

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

    public void MountObserverStrategy(string targetName, string observerIndex) { }

    public void UnmountObserverStrategy(string targetName, string observerIndex) { }
    public void MountObserverStrategy(ISndEntity target, string observerIndex) { }
    public void UnmountObserverStrategy(ISndEntity target, string observerIndex) { }

    public INodeHandle GetNode(string name) => throw new InvalidOperationException($"Node '{name}' not found.");

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

    public object? InvokeStrategy(string strategyIndex, object? input = null) =>
        throw new InvalidOperationException("InvokeStrategy not supported on DummySndEntity.");
}
