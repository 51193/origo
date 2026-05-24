using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Node;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.DataSource;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Console;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Tests;

internal sealed class TestLogger : ILogger
{
    public readonly List<string> Errors = new();
    public readonly List<string> Infos = new();
    public readonly List<string> Warnings = new();

    public void Log(LogLevel level, string tag, string message)
    {
        switch (level)
        {
            case LogLevel.Warning:
                Warnings.Add($"{tag}: {message}");
                break;
            case LogLevel.Error:
                Errors.Add($"{tag}: {message}");
                break;
            default:
                Infos.Add($"{tag}: {message}");
                break;
        }
    }
}

internal sealed class TestNodeHandle : INodeHandle
{
    public TestNodeHandle(string name, object? native = null)
    {
        Name = name;
        Native = native ?? new object();
    }

    public bool IsVisible { get; private set; } = true;
    public int FreeCount { get; private set; }

    public string Name { get; }
    public object Native { get; }

    public void Free() => FreeCount++;

    public void SetVisible(bool visible) => IsVisible = visible;
}

internal sealed class TestNodeFactory : INodeFactory
{
    private readonly HashSet<string> _resourceIdsThatFail;
    public readonly List<TestNodeHandle> CreatedHandles = new();

    public readonly List<(string logicalName, string resourceId)> Requests = new();

    public TestNodeFactory(IEnumerable<string>? resourceIdsThatFail = null)
    {
        _resourceIdsThatFail = resourceIdsThatFail != null
            ? new HashSet<string>(resourceIdsThatFail, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
    }

    public INodeHandle Create(string logicalName, string resourceId)
    {
        Requests.Add((logicalName, resourceId));
        if (_resourceIdsThatFail.Contains(resourceId))
            throw new InvalidOperationException($"Simulated node creation failure for resourceId='{resourceId}'.");

        var handle = new TestNodeHandle(logicalName, resourceId);
        CreatedHandles.Add(handle);
        return handle;
    }
}

internal sealed class TestFileSystem : IFileSystem
{
    private readonly HashSet<string> _directories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    public int ReadAllTextCallCount { get; private set; }

    public bool Exists(string path) => _files.ContainsKey(Normalize(path));

    public bool DirectoryExists(string path)
    {
        var normalized = Normalize(path).TrimEnd('/');
        return _directories.Contains(normalized) ||
               _files.Keys.Any(f => f.StartsWith(normalized + "/", StringComparison.Ordinal));
    }

    public string ReadAllText(string path)
    {
        ReadAllTextCallCount++;
        var normalized = Normalize(path);
        return _files[normalized];
    }

    public void WriteAllText(string path, string content, bool overwrite)
    {
        var normalized = Normalize(path);
        if (!overwrite && _files.ContainsKey(normalized))
            throw new IOException($"File already exists: {normalized}");

        _files[normalized] = content;
        EnsureParents(normalized);
    }

    public void Copy(string sourcePath, string destinationPath, bool overwrite)
    {
        var source = Normalize(sourcePath);
        var destination = Normalize(destinationPath);
        if (!_files.TryGetValue(source, out var content))
            throw new FileNotFoundException("Source not found.", source);

        if (!overwrite && _files.ContainsKey(destination))
            throw new IOException($"File already exists: {destination}");

        _files[destination] = content;
        EnsureParents(destination);
    }

    public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive)
    {
        var normalized = Normalize(directoryPath).TrimEnd('/');
        var prefix = normalized + "/";
        foreach (var file in _files.Keys.ToArray())
        {
            if (!file.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            if (!recursive)
            {
                var rest = file.Substring(prefix.Length);
                if (rest.Contains('/'))
                    continue;
            }

            if (searchPattern is "*" or "*.*" || file.EndsWith(searchPattern.TrimStart('*'), StringComparison.Ordinal))
                yield return file;
        }
    }

    public void CreateDirectory(string directoryPath)
    {
        var normalized = Normalize(directoryPath).TrimEnd('/');
        if (normalized.Length == 0)
            return;

        _directories.Add(normalized);
        EnsureParents(normalized + "/dummy");
    }

    public void Delete(string path)
    {
        var normalized = Normalize(path);
        _files.Remove(normalized);
    }

    public string CombinePath(string basePath, string relativePath) =>
        Normalize($"{Normalize(basePath).TrimEnd('/')}/{relativePath}");

    public string GetParentDirectory(string path)
    {
        var normalized = Normalize(path).TrimEnd('/');
        var index = normalized.LastIndexOf('/');
        return index <= 0 ? string.Empty : normalized.Substring(0, index);
    }

    public IEnumerable<string> EnumerateDirectories(string directoryPath)
    {
        var normalized = Normalize(directoryPath).TrimEnd('/');
        var prefix = normalized + "/";
        var children = new HashSet<string>(StringComparer.Ordinal);

        foreach (var dir in _directories)
        {
            if (!dir.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            var rest = dir.Substring(prefix.Length);
            var slash = rest.IndexOf('/');
            if (slash >= 0)
                rest = rest.Substring(0, slash);
            if (rest.Length > 0)
                children.Add(prefix + rest);
        }

        foreach (var file in _files.Keys.ToArray())
        {
            if (!file.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            var rest = file.Substring(prefix.Length);
            var slash = rest.IndexOf('/');
            if (slash > 0)
                children.Add(prefix + rest.Substring(0, slash));
        }

        return children;
    }

    public void Rename(string sourcePath, string destinationPath)
    {
        var src = Normalize(sourcePath).TrimEnd('/');
        var dst = Normalize(destinationPath).TrimEnd('/');

        // Move all files under source to destination
        var srcPrefix = src + "/";
        var filesToMove = _files.Keys.Where(f => f.StartsWith(srcPrefix, StringComparison.Ordinal) || f == src)
            .ToList();
        foreach (var file in filesToMove)
        {
            var newPath = dst + file.Substring(src.Length);
            _files[newPath] = _files[file];
            _files.Remove(file);
            EnsureParents(newPath);
        }

        // Move all directories under source to destination
        var dirsToMove = _directories.Where(d => d.StartsWith(srcPrefix, StringComparison.Ordinal) || d == src)
            .ToList();
        foreach (var dir in dirsToMove)
        {
            var newDir = dst + dir.Substring(src.Length);
            _directories.Remove(dir);
            _directories.Add(newDir);
        }

        EnsureParents(dst + "/dummy");
    }

    public void DeleteDirectory(string directoryPath)
    {
        var normalized = Normalize(directoryPath).TrimEnd('/');
        var prefix = normalized + "/";

        var filesToRemove = _files.Keys.Where(f => f.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        foreach (var file in filesToRemove)
            _files.Remove(file);

        var dirsToRemove = _directories.Where(d => d == normalized || d.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();
        foreach (var dir in dirsToRemove)
            _directories.Remove(dir);
    }

    public void SeedFile(string path, string content)
    {
        var normalized = Normalize(path);
        _files[normalized] = content;
        EnsureParents(normalized);
    }

    private static string Normalize(string path) => path.Replace('\\', '/').Trim();

    private void EnsureParents(string filePath)
    {
        var normalized = Normalize(filePath);
        var index = normalized.LastIndexOf('/');
        while (index > 0)
        {
            var dir = normalized.Substring(0, index);
            _directories.Add(dir);
            index = dir.LastIndexOf('/');
        }
    }
}

internal sealed class TestSndSceneHost : ISndSceneHost
{
    private readonly List<ISndEntity> _entities = new();
    private readonly List<SndMetaData> _metaList = new();
    public int ClearAllCount { get; private set; }

    public ISndEntity Spawn(SndMetaData metaData)
    {
        _metaList.Add(metaData);
        var entity = new DummySndEntity(metaData.Name);
        _entities.Add(entity);
        return entity;
    }

    public IReadOnlyCollection<ISndEntity> GetEntities() => _entities;

    public ISndEntity? FindByName(string name) => _entities.FirstOrDefault(e => e.Name == name);

    public IReadOnlyList<SndMetaData> SerializeMetaList() => _metaList.ToArray();

    public void LoadFromMetaList(IEnumerable<SndMetaData> metaList)
    {
        _metaList.Clear();
        _metaList.AddRange(metaList);
    }

    public void ClearAll()
    {
        ClearAllCount++;
        _metaList.Clear();
        _entities.Clear();
    }

    public void ProcessAll(double delta)
    {
    }
}

internal sealed class DummySndEntity : ISndEntity
{
    private readonly Dictionary<string, object?> _data = new(StringComparer.Ordinal);
    public readonly string EntityName;

    public DummySndEntity(string entityName)
    {
        EntityName = entityName;
        _data["name"] = entityName;
    }

    public string Name => EntityName;

    public void SetData<T>(string name, T value) => _data[name] = value;

    public T GetData<T>(string name) => _data.TryGetValue(name, out var value) && value is T cast ? cast : default!;

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

    public INodeHandle GetNode(string name) => throw new InvalidOperationException($"Node '{name}' not found.");

    public IReadOnlyCollection<string> GetNodeNames() => Array.Empty<string>();

    public void AddStrategy(string index)
    {
    }

    public void RemoveStrategy(string index)
    {
    }
}

/// <summary>
///     Centralized factory for test infrastructure objects with the new DataSource-based constructors.
/// </summary>
internal static class TestFactory
{
    public static JsonDataSourceCodec CreateJsonCodec() => new();

    public static MapDataSourceCodec CreateMapCodec() => new();

    public static DataSourceNode NodeFromJson(string json) => CreateJsonCodec().Decode(json);

    public static string JsonFromNode(DataSourceNode node) => CreateJsonCodec().Encode(node);

    public static DataSourceConverterRegistry CreateRegistry()
    {
        var tm = new TypeStringMapping();
        return DataSourceFactory.CreateDefaultRegistry(tm);
    }

    public static DataSourceConverterRegistry CreateRegistry(
        TypeStringMapping tm) =>
        DataSourceFactory.CreateDefaultRegistry(tm);

    public static IDataSourceIoGateway CreateIoGateway(IFileSystem fileSystem) =>
        DataSourceFactory.CreateDefaultIoGateway(fileSystem);

    public static SndWorld CreateSndWorld(
        TypeStringMapping? tm = null,
        ILogger? logger = null)
    {
        tm ??= new TypeStringMapping();
        logger ??= new TestLogger();
        var reg = CreateRegistry(tm);
        return new SndWorld(tm, logger, reg, CreateIoGateway(new TestFileSystem()));
    }

    public static OrigoRuntime CreateRuntime(
        ILogger? logger = null,
        ISndSceneHost? sceneHost = null,
        TypeStringMapping? tm = null,
        IBlackboard? systemBb = null,
        OrigoMeta? meta = null)
    {
        logger ??= new TestLogger();
        sceneHost ??= new TestSndSceneHost();
        tm ??= new TypeStringMapping();
        systemBb ??= new Blackboard.Blackboard();
        meta ??= new OrigoMeta("Origo", "test", string.Empty);
        var reg = CreateRegistry(tm);
        var io = CreateIoGateway(new TestFileSystem());
        return new OrigoRuntime(
            meta, logger, sceneHost, tm, reg, io, systemBb);
    }

    public static OrigoRuntime CreateRuntime(
        ILogger logger,
        ISndSceneHost sceneHost,
        TypeStringMapping tm,
        IBlackboard systemBb,
        ConsoleInputQueue consoleInput,
        ConsoleOutputChannel consoleOutput,
        OrigoMeta? meta = null)
    {
        meta ??= new OrigoMeta("Origo", "test", string.Empty);
        var reg = CreateRegistry(tm);
        var io = CreateIoGateway(new TestFileSystem());
        return new OrigoRuntime(
            meta, logger, sceneHost, tm, reg, io,
            systemBb, consoleInput, consoleOutput);
    }

    // ── Lifecycle helpers for tests ────────────────────────────────────

    public static SystemRuntime CreateSystemRuntime(
        ILogger logger,
        IFileSystem fileSystem,
        string saveRootPath,
        OrigoRuntime runtime,
        ISaveStorageService? storageService = null,
        ISavePathPolicy? savePathPolicy = null)
    {
        savePathPolicy ??= new DefaultSavePathPolicy();
        storageService ??= new DefaultSaveStorageService(fileSystem, saveRootPath, savePathPolicy);
        return new SystemRuntime(runtime,
            new SystemParameters(logger, fileSystem, saveRootPath, storageService, savePathPolicy));
    }

    public static ProgressRun CreateProgressRun(
        string saveId,
        ILogger logger,
        IFileSystem fileSystem,
        string saveRootPath,
        OrigoRuntime runtime,
        ISndContext sndContext,
        ISaveStorageService? storageService = null,
        ISavePathPolicy? savePathPolicy = null)
    {
        var systemRuntime = CreateSystemRuntime(
            logger, fileSystem, saveRootPath, runtime, storageService, savePathPolicy);
        return new ProgressRun(
            systemRuntime,
            new ProgressParameters(saveId),
            (IStateMachineContext)sndContext,
            sndContext);
    }
}
