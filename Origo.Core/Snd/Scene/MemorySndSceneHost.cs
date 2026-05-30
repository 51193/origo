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

    /// <summary>
    ///     按元数据生成一个内存实体并加入场景。
    /// </summary>
    public ISndEntity Spawn(SndMetaData metaData)
    {
        ArgumentNullException.ThrowIfNull(metaData);
        _metaList.Add(metaData);
        var entity = new MemorySndEntity(metaData.Name);
        _entities.Add(entity);
        return entity;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ISndEntity> GetEntities() => _entities;

    /// <inheritdoc />
    public ISndEntity? FindByName(string name) =>
        _entities.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));

    /// <inheritdoc />
    public IReadOnlyList<SndMetaData> SerializeMetaList() => _metaList.ToArray();

    /// <inheritdoc />
    public void LoadFromMetaList(IEnumerable<SndMetaData> metaList)
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

    /// <inheritdoc />
    public void ClearAll()
    {
        _metaList.Clear();
        _entities.Clear();
    }

    /// <inheritdoc />
    public void ProcessAll(double delta)
    {
        // No-op: MemorySndSceneHost does not support process updates.
    }

    /// <inheritdoc />
    public void DeadByName(string name)
    {
        var index = _entities.FindIndex(e => string.Equals(e.Name, name, StringComparison.Ordinal));
        if (index < 0)
            return;

        var entity = _entities[index];
        entity.Kill();
        _entities.RemoveAt(index);
        _metaList.RemoveAt(index);
    }
}

/// <summary>
///     纯内存 <see cref="ISndEntity" /> 实现，用于 <see cref="MemorySndSceneHost" />。
///     支持基本的键值数据存取，不绑定任何引擎节点。
/// </summary>
internal sealed class MemorySndEntity : ISndEntity
{
    private readonly Dictionary<string, object?> _data = new(StringComparer.Ordinal);

    public MemorySndEntity(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _data["name"] = name;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public void SetData<T>(string name, T value) => _data[name] = value;

    /// <inheritdoc />
    public T GetData<T>(string name)
    {
        if (!_data.TryGetValue(name, out var value))
            throw new KeyNotFoundException($"Data key '{name}' not found in MemorySndSceneHost.");
        if (value is T cast)
            return cast;
        throw new InvalidCastException(
            $"Data key '{name}' is of type '{value?.GetType().Name ?? "null"}' but requested as '{typeof(T).Name}'.");
    }

    /// <inheritdoc />
    public (bool found, T? value) TryGetData<T>(string name)
    {
        if (_data.TryGetValue(name, out var value) && value is T cast)
            return (true, cast);
        return (false, default);
    }

    /// <inheritdoc />
    public void Subscribe(string name, Action<ISndEntity, object?, object?> callback,
        Func<ISndEntity, object?, object?, bool>? filter = null)
    {
        // No-op in memory entity.
    }

    /// <inheritdoc />
    public void Unsubscribe(string name, Action<ISndEntity, object?, object?> callback)
    {
        // No-op in memory entity.
    }

    /// <inheritdoc />
    public INodeHandle GetNode(string name)
    {
        throw new InvalidOperationException(
            $"MemorySndEntity does not support node access. Node '{name}' requested.");
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetNodeNames() => Array.Empty<string>();

    /// <inheritdoc />
    public void AddStrategy(string index)
    {
        // No-op in memory entity.
    }

    /// <inheritdoc />
    public void RemoveStrategy(string index)
    {
        // No-op in memory entity.
    }

    /// <inheritdoc />
    public void AddActiveStrategy(string index)
    {
        // No-op in memory entity.
    }

    /// <inheritdoc />
    public void RemoveActiveStrategy(string index)
    {
        // No-op in memory entity.
    }

    /// <inheritdoc />
    public object? InvokeStrategy(string strategyIndex, object? input = null)
    {
        return null;
    }

    public void Kill()
    {
        // No-op in memory entity.
    }
}
