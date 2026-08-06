using System.Threading;
using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.DataSource;
using Origo.Core.Save.Storage;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Save;

/// <summary>
///     Decorator blackboard: wraps an inner <see cref="IBlackboard" /> and
///     automatically serializes to a specified file path on every mutation.
///     All mutation operations are thread-safe via a lock.
/// </summary>
public sealed class PersistentBlackboard : IBlackboard
{
    private readonly IDataSourceIoGateway _dataSourceIo;
    private readonly string _filePath;
    private readonly IFileMetaAccess _metaAccess;
    private readonly IPathResolver _pathResolver;
    private readonly IBlackboard _inner;
    private readonly Lock _lock = new();
    private readonly DataSourceConverterRegistry _registry;

    /// <summary>
    ///     Creates a persistent blackboard instance, wrapping the specified
    ///     inner blackboard and binding it to a disk file path.
    /// </summary>
    public PersistentBlackboard(
        IFileMetaAccess metaAccess,
        IPathResolver pathResolver,
        string filePath,
        IDataSourceIoGateway dataSourceIo,
        DataSourceConverterRegistry registry,
        IBlackboard inner)
    {
        ArgumentNullException.ThrowIfNull(metaAccess);
        _metaAccess = metaAccess;
        ArgumentNullException.ThrowIfNull(pathResolver);
        _pathResolver = pathResolver;
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
    ///     Sets a key-value pair and automatically persists to disk.
    /// </summary>
    public void SetValue<T>(string key, T value)
    {
        lock (_lock)
        {
            _inner.SetValue(key, value);
            Persist();
        }
    }

    /// <summary>
    ///     Attempts to get the value of the specified key; found is false
    ///     when not found.
    /// </summary>
    public (bool found, T value) TryGet<T>(string key)
    {
        lock (_lock)
        {
            return _inner.TryGet<T>(key);
        }
    }

    /// <summary>
    ///     Clears all key-value pairs from the blackboard and persists
    ///     the empty state to disk.
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
    ///     Gets the set of all registered keys in the blackboard.
    /// </summary>
    public IReadOnlyCollection<string> GetKeys()
    {
        lock (_lock)
        {
            return _inner.GetKeys();
        }
    }

    /// <summary>
    ///     Serializes all key-value pairs in the blackboard to a typed-data
    ///     dictionary.
    /// </summary>
    public IReadOnlyDictionary<string, TypedData> SerializeAll()
    {
        lock (_lock)
        {
            return _inner.SerializeAll();
        }
    }

    /// <summary>
    ///     Restores all blackboard key-value pairs from a typed-data
    ///     dictionary and persists to disk.
    /// </summary>
    public void DeserializeAll(IReadOnlyDictionary<string, TypedData> data)
    {
        lock (_lock)
        {
            _inner.DeserializeAll(data);
            Persist();
        }
    }

    private const string _tempSuffix = ".tmp.json";
    private const string _backupSuffix = ".bak.json";

    /// <summary>
    ///     Restores state from disk at startup. If the file does not exist,
    ///     this is a no-op. Stale temporary files from an interrupted atomic
    ///     write are cleaned up automatically, and a previous version left in
    ///     the backup file is restored when the primary file is missing.
    /// </summary>
    public void LoadFromDisk()
    {
        lock (_lock)
        {
            var tempPath = _filePath + _tempSuffix;
            if (_metaAccess.FileExists(tempPath))
                _metaAccess.Delete(tempPath);

            var backupPath = _filePath + _backupSuffix;
            if (!_metaAccess.FileExists(_filePath) && _metaAccess.FileExists(backupPath))
                _metaAccess.Rename(backupPath, _filePath);

            if (!_metaAccess.FileExists(_filePath))
                return;

            using var node = _dataSourceIo.ReadTree(_filePath);
            _inner.DeserializeAll(_registry.Read<IReadOnlyDictionary<string, TypedData>>(node));
        }
    }

    private void Persist()
    {
        var parentDir = _pathResolver.GetParentDirectory(_filePath);
        if (!string.IsNullOrEmpty(parentDir) && !_metaAccess.DirectoryExists(parentDir))
            _metaAccess.CreateDirectory(parentDir);

        var data = _inner.SerializeAll();
        using var node = _registry.Write<IReadOnlyDictionary<string, TypedData>>(data);
        var tempPath = _filePath + _tempSuffix;
        var backupPath = _filePath + _backupSuffix;
        _dataSourceIo.WriteTree(tempPath, node);

        var hadExisting = _metaAccess.FileExists(_filePath);
        if (hadExisting)
        {
            if (_metaAccess.FileExists(backupPath))
                _metaAccess.Delete(backupPath);
            _metaAccess.Rename(_filePath, backupPath);
        }

        _metaAccess.Rename(tempPath, _filePath);

        if (hadExisting)
            _metaAccess.Delete(backupPath);
    }
}
