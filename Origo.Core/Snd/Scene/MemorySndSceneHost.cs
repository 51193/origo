using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Node;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd.Scene;

/// <summary>
///     纯内存 <see cref="ISndSceneHost" /> 实现，不依赖任何引擎适配层。
///     用于 <see cref="LevelBuilder" /> 等 Core 层离线构建关卡场景，
///     以及单元测试中需要完全内存化的场景宿主。
/// </summary>
internal sealed class MemorySndSceneHost : ISndSceneHost
{
    private readonly List<ISndEntity> _entities = new();
    private readonly List<SndMetaData> _metaList = new();

    public ISndEntity CreateEntity(SndMetaData metaData)
    {
        ArgumentNullException.ThrowIfNull(metaData);
        _metaList.Add(metaData);
        var entity = new MemorySndEntity(metaData.Name);
        _entities.Add(entity);
        return entity;
    }

    public IReadOnlyCollection<ISndEntity> GetEntities() => _entities;

    public ISndEntity? FindByName(string name) =>
        _entities.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));

    public IReadOnlyList<SndMetaData> BuildMetaList() => _metaList.ToArray();

    public void RecoverFromMetaList(IEnumerable<SndMetaData> metaList)
    {
        ArgumentNullException.ThrowIfNull(metaList);
        _metaList.Clear();
        _entities.Clear();
        foreach (var meta in metaList)
        {
            _metaList.Add(meta);
            _entities.Add(new MemorySndEntity(meta.Name));
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

        var entity = _entities[index];
        _entities.RemoveAt(index);
        _metaList.RemoveAt(index);
        if (entity is MemorySndEntity memEntity)
            memEntity.Dead();
    }

    public void RequestKillEntity(string name)
    {
        var entity = _entities.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));
        if (entity is not MemorySndEntity memEntity)
            throw new InvalidOperationException($"No entity with name '{name}'.");
        if (memEntity.IsPendingKill)
            throw new InvalidOperationException($"Entity '{name}' is already pending kill.");
        memEntity.IsPendingKill = true;
    }
}

internal sealed class MemorySndEntity : ISndEntity
{
    private readonly Dictionary<string, object?> _data = new(StringComparer.Ordinal);

    public MemorySndEntity(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _data["name"] = name;
    }

    public string Name { get; }

    public void SetData<T>(string name, T value) => _data[name] = value;

    public T GetData<T>(string name)
    {
        if (!_data.TryGetValue(name, out var value))
            throw new KeyNotFoundException($"Data key '{name}' not found in MemorySndSceneHost.");
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

    public void Subscribe(string name, Action<ISndEntity, object?, object?> callback,
        Func<ISndEntity, object?, object?, bool>? filter = null)
    {
    }

    public void Unsubscribe(string name, Action<ISndEntity, object?, object?> callback)
    {
    }

    public INodeHandle GetNode(string name)
    {
        throw new InvalidOperationException(
            $"MemorySndEntity does not support node access. Node '{name}' requested.");
    }

    public IReadOnlyCollection<string> GetNodeNames() => Array.Empty<string>();

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

#pragma warning disable CA1822
    internal void Dead()
    {
    }
#pragma warning restore CA1822
}
