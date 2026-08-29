using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Node;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Snd.Metadata;

namespace Origo.TestSupport;

/// <summary>
///     Test scene host that stores lightweight <see cref="DummySndEntity" />
///     instances and mirrors the production scene-host contracts relevant to
///     tests (snapshot reads, append-only recovery, strict removal).
/// </summary>
public sealed class TestSndSceneHost : ISndSceneHost
{
    private readonly List<ISndEntity> _entities = [];
    private readonly List<SndMetaData> _metaList = [];

    /// <summary>Number of times <see cref="RemoveAllEntities" /> has been called.</summary>
    public int ClearAllCount { get; private set; }

    /// <inheritdoc/>
    public ISndEntity CreateEntity(SndMetaData metaData)
    {
        _metaList.Add(metaData);
        var entity = new DummySndEntity(metaData.Name);
        _entities.Add(entity);
        return entity;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<ISndEntity> GetEntities() => [.. _entities];

    /// <inheritdoc/>
    public ISndEntity? FindByName(string name) => _entities.FirstOrDefault(e => e.Name == name);

    /// <inheritdoc/>
    public IReadOnlyList<SndMetaData> BuildMetaList() => [.. _metaList];

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public void RemoveAllEntities()
    {
        ClearAllCount++;
        _metaList.Clear();
        _entities.Clear();
    }

    /// <inheritdoc/>
    public void ProcessAll(double delta)
    {
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

/// <summary>
///     Minimal <see cref="ISndEntity" /> test double with in-memory data and
///     no-op strategy operations. Node access and active-strategy invocation
///     fail explicitly.
/// </summary>
public sealed class DummySndEntity : ISndEntity
{
    /// <summary>Owning session; assign before exercising session-dependent paths.</summary>
    public ISessionRun OwningSession { get; set; } = null!;

    private readonly Dictionary<string, object?> _data = new(StringComparer.Ordinal);

    /// <summary>Stable entity name used by the host and by name-based lookups.</summary>
    public readonly string EntityName;

    /// <summary>Creates an entity with the given name.</summary>
    public DummySndEntity(string entityName)
    {
        EntityName = entityName;
        _data["name"] = entityName;
    }

    /// <inheritdoc/>
    public string Name => EntityName;

    /// <inheritdoc/>
    public bool IsPendingKill { get; set; }

    /// <inheritdoc/>
    public void SetData<T>(string name, T value) => _data[name] = value;

    /// <inheritdoc/>
    public T GetData<T>(string name) where T : notnull
    {
        if (!_data.TryGetValue(name, out var value))
            throw new InvalidOperationException($"Data key '{name}' not found in DummySndEntity '{Name}'.");
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
    public void MountObserverStrategy(ISndEntity target, string observerIndex) { }
    /// <inheritdoc/>
    public void UnmountObserverStrategy(ISndEntity target, string observerIndex) { }

    /// <inheritdoc/>
    public INodeHandle GetNode(string name) => throw new InvalidOperationException($"Node '{name}' not found.");

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
    public object? InvokeStrategy(string strategyIndex, object? input = null) =>
        throw new InvalidOperationException("InvokeStrategy not supported on DummySndEntity.");
}
