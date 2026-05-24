using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.DataSource;
using Origo.Core.Save.Storage;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Save;

/// <summary>
///     装饰器黑板：包装内部 <see cref="IBlackboard" />，每次 mutation 自动序列化到指定文件路径。
///     所有 mutation 操作通过锁保证线程安全。
/// </summary>
public sealed class PersistentBlackboard : IBlackboard
{
    private readonly IDataSourceIoGateway _dataSourceIo;
    private readonly string _filePath;
    private readonly IFileSystem _fileSystem;
    private readonly IBlackboard _inner;
    private readonly object _lock = new();
    private readonly DataSourceConverterRegistry _registry;

    /// <summary>
    ///     创建持久化黑板实例，包装指定的内部黑板并绑定到磁盘文件路径。
    /// </summary>
    public PersistentBlackboard(
        IFileSystem fileSystem,
        string filePath,
        IDataSourceIoGateway dataSourceIo,
        DataSourceConverterRegistry registry,
        IBlackboard inner)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));
        _filePath = filePath;
        ArgumentNullException.ThrowIfNull(dataSourceIo);
        _dataSourceIo = dataSourceIo;
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <summary>
    ///     设置键值并自动持久化到磁盘。
    /// </summary>
    public void Set<T>(string key, T value)
    {
        lock (_lock)
        {
            _inner.Set(key, value);
            Persist();
        }
    }

    /// <summary>
    ///     尝试获取指定键的值；未找到时 found 为 false。
    /// </summary>
    public (bool found, T value) TryGet<T>(string key)
    {
        lock (_lock)
        {
            return _inner.TryGet<T>(key);
        }
    }

    /// <summary>
    ///     清空黑板中所有键值并持久化空状态到磁盘。
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _inner.Clear();
            Persist();
        }
    }

    /// <summary>
    ///     获取黑板中所有已注册的键集合。
    /// </summary>
    public IReadOnlyCollection<string> GetKeys()
    {
        lock (_lock)
        {
            return _inner.GetKeys();
        }
    }

    /// <summary>
    ///     将黑板中所有键值序列化为带类型信息的字典。
    /// </summary>
    public IReadOnlyDictionary<string, TypedData> SerializeAll()
    {
        lock (_lock)
        {
            return _inner.SerializeAll();
        }
    }

    /// <summary>
    ///     从带类型信息的字典恢复黑板全部键值并持久化到磁盘。
    /// </summary>
    public void DeserializeAll(IReadOnlyDictionary<string, TypedData> data)
    {
        lock (_lock)
        {
            _inner.DeserializeAll(data);
            Persist();
        }
    }

    /// <summary>
    ///     启动时从磁盘恢复状态。若文件不存在则不做任何操作。
    /// </summary>
    public void LoadFromDisk()
    {
        lock (_lock)
        {
            if (!_dataSourceIo.Exists(_filePath))
                return;

            using var node = _dataSourceIo.ReadTree(_filePath);
            var dict = _registry.Read<IReadOnlyDictionary<string, TypedData>>(node);
            if (dict is not null)
                _inner.DeserializeAll(dict);
        }
    }

    private void Persist()
    {
        SavePathResolver.EnsureParentDirectory(_fileSystem, _filePath);

        var data = _inner.SerializeAll();
        using var node = _registry.Write<IReadOnlyDictionary<string, TypedData>>(data);
        _dataSourceIo.WriteTree(_filePath, node);
    }
}
