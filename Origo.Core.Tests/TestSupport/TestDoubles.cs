using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Node;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.DataSource;
using Origo.Core.DataSource.Codec;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Console;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Tests;

internal sealed class TestLogger : ILogger
{
    public readonly List<string> Debugs = [];
    public readonly List<string> Errors = [];
    public readonly List<string> Infos = [];
    public readonly List<string> Warnings = [];

    public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

    public void Log(LogLevel level, string tag, string message)
    {
        if (level < MinimumLevel) return;
        switch (level)
        {
            case LogLevel.Debug:
                Debugs.Add($"{tag}: {message}");
                break;
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

    public void Clear()
    {
        Debugs.Clear();
        Infos.Clear();
        Warnings.Clear();
        Errors.Clear();
    }
}

internal sealed class TestNodeHandle(string name) : INodeHandle
{
    public bool IsVisible { get; private set; } = true;
    public int FreeCount { get; private set; }

    public string Name { get; } = name;

    public void Free() => FreeCount++;

    public void SetVisible(bool visible) => IsVisible = visible;
}

internal sealed class TestNodeFactory(IEnumerable<string>? resourceIdsThatFail = null) : INodeFactory
{
    private readonly HashSet<string> _resourceIdsThatFail = resourceIdsThatFail != null
            ? new HashSet<string>(resourceIdsThatFail, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
    public readonly List<TestNodeHandle> CreatedHandles = [];

    public readonly List<(string logicalName, string resourceId)> Requests = [];

    public INodeHandle Create(string logicalName, string resourceId)
    {
        Requests.Add((logicalName, resourceId));
        if (_resourceIdsThatFail.Contains(resourceId))
            throw new InvalidOperationException($"Simulated node creation failure for resourceId='{resourceId}'.");

        var handle = new TestNodeHandle(logicalName);
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
                var rest = file[prefix.Length..];
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
        return index <= 0 ? string.Empty : normalized[..index];
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
            var rest = dir[prefix.Length..];
            var slash = rest.IndexOf('/');
            if (slash >= 0)
                rest = rest[..slash];
            if (rest.Length > 0)
                children.Add(prefix + rest);
        }

        foreach (var file in _files.Keys.ToArray())
        {
            if (!file.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            var rest = file[prefix.Length..];
            var slash = rest.IndexOf('/');
            if (slash > 0)
                children.Add(string.Concat(prefix.AsSpan(), rest.AsSpan(0, slash)));
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
            var newPath = string.Concat(dst.AsSpan(), file.AsSpan(src.Length));
            _files[newPath] = _files[file];
            _files.Remove(file);
            EnsureParents(newPath);
        }

        // Move all directories under source to destination
        var dirsToMove = _directories.Where(d => d.StartsWith(srcPrefix, StringComparison.Ordinal) || d == src)
            .ToList();
        foreach (var dir in dirsToMove)
        {
            var newDir = string.Concat(dst.AsSpan(), dir.AsSpan(src.Length));
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
            var dir = normalized[..index];
            _directories.Add(dir);
            index = dir.LastIndexOf('/');
        }
    }
}

internal sealed class TestSndSceneHost : ISndSceneHost
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

    public IReadOnlyCollection<ISndEntity> GetEntities() => _entities;

    public ISndEntity? FindByName(string name) => _entities.FirstOrDefault(e => e.Name == name);

    public IReadOnlyList<SndMetaData> BuildMetaList() => [.. _metaList];

    public void RecoverFromMetaList(IEnumerable<SndMetaData> metaList)
    {
        _metaList.Clear();
        _entities.Clear();
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
        var entity = _entities.FirstOrDefault(e => e.Name == name);
        if (entity is not null)
            _entities.Remove(entity);
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

internal sealed class DummySndEntity : ISndEntity
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

    public T GetData<T>(string name) => _data.TryGetValue(name, out var value) && value is T cast ? cast : default!;

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

    public static IFileMetaAccess CreateFileMetaAccess(IFileSystem fileSystem) =>
        DataSourceFactory.CreateFileMetaAccess(fileSystem);

    public static IPathResolver CreatePathResolver(IFileSystem fileSystem) =>
        DataSourceFactory.CreatePathResolver(fileSystem);

    public static SndWorld CreateSndWorld(
        TypeStringMapping? tm = null,
        ILogger? logger = null,
        IFileSystem? fileSystem = null)
    {
        tm ??= new TypeStringMapping();
        logger ??= new TestLogger();
        var reg = CreateRegistry(tm);
        return new SndWorld(tm, logger, reg, CreateIoGateway(fileSystem ?? new TestFileSystem()));
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
        IDataSourceIoGateway sharedDataSourceIo,
        OrigoMeta? meta = null)
    {
        meta ??= new OrigoMeta("Origo", "test", string.Empty);
        var reg = CreateRegistry(tm);
        return new OrigoRuntime(
            meta, logger, sceneHost, tm, reg, sharedDataSourceIo, systemBb);
    }

    public static OrigoRuntime CreateRuntime(
        ILogger logger,
        ISndSceneHost sceneHost,
        TypeStringMapping tm,
        IBlackboard systemBb,
        IFileSystem sharedFileSystem,
        OrigoMeta? meta = null)
    {
        meta ??= new OrigoMeta("Origo", "test", string.Empty);
        var reg = CreateRegistry(tm);
        var io = CreateIoGateway(sharedFileSystem);
        return new OrigoRuntime(
            meta, logger, sceneHost, tm, reg, io, systemBb);
    }

    public static OrigoRuntime CreateRuntime(
        ILogger logger,
        ISndSceneHost sceneHost,
        TypeStringMapping tm,
        IBlackboard systemBb,
        IConsoleInputSource consoleInput,
        IConsoleOutputChannel consoleOutput,
        IDataSourceIoGateway? sharedDataSourceIo = null,
        OrigoMeta? meta = null)
    {
        meta ??= new OrigoMeta("Origo", "test", string.Empty);
        var reg = CreateRegistry(tm);
        var io = sharedDataSourceIo ?? CreateIoGateway(new TestFileSystem());
        return new OrigoRuntime(
            meta, logger, sceneHost, tm, reg, io, systemBb, consoleInput, consoleOutput);
    }

    // ── Lifecycle helpers for tests ────────────────────────────────────

    public static SystemRuntime CreateSystemRuntime(
        ILogger logger,
        IFileMetaAccess metaAccess,
        IPathResolver pathResolver,
        string saveRootPath,
        OrigoRuntime runtime,
        ISaveStorageService? storageService = null,
        ISavePathPolicy? savePathPolicy = null,
        IDataSourceIoGateway? sharedDataSourceIo = null)
    {
        savePathPolicy ??= new DefaultSavePathPolicy();
        storageService ??= CreateDefaultSaveStorageServiceForTests(metaAccess, runtime, pathResolver, saveRootPath, savePathPolicy, sharedDataSourceIo);
        return new SystemRuntime(runtime,
            new SystemParameters(logger, metaAccess, pathResolver, saveRootPath, storageService, savePathPolicy,
                runtime.GetAdapterSceneHost()));
    }

    public static ISessionRun BootstrapForegroundSession(
        OrigoRuntime runtime,
        IDataSourceIoGateway? dataSourceIo = null)
    {
        var fs = new TestFileSystem();
        fs.SeedFile("entry.json", "[]");
        var io = dataSourceIo ?? CreateIoGateway(fs);
        var metaAccess = CreateFileMetaAccess(fs);
        var pathResolver = CreatePathResolver(fs);

        var ctx = new SndContext(new SndContextParameters(
            runtime, io, metaAccess, pathResolver, "root", "initial", "entry.json"));
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        return runtime.SessionManager.ForegroundSession
               ?? throw new InvalidOperationException("Foreground session was not created.");
    }

    private static DefaultSaveStorageService CreateDefaultSaveStorageServiceForTests(
        IFileMetaAccess metaAccess, OrigoRuntime runtime, IPathResolver pathResolver,
        string saveRootPath, ISavePathPolicy savePathPolicy,
        IDataSourceIoGateway? sharedDataSourceIo = null)
    {
        return new DefaultSaveStorageService(metaAccess,
            sharedDataSourceIo ?? runtime.SndWorld.DataSourceIo,
            pathResolver,
            saveRootPath,
            savePathPolicy);
    }

    public static ProgressRun CreateProgressRun(
        string saveId,
        ILogger logger,
        IFileMetaAccess metaAccess,
        IPathResolver pathResolver,
        string saveRootPath,
        OrigoRuntime runtime,
        ISndContext sndContext,
        ISaveStorageService? storageService = null,
        ISavePathPolicy? savePathPolicy = null,
        IDataSourceIoGateway? sharedDataSourceIo = null)
    {
        var systemRuntime = CreateSystemRuntime(
            logger, metaAccess, pathResolver, saveRootPath, runtime, storageService, savePathPolicy, sharedDataSourceIo);
        return new ProgressRun(
            systemRuntime,
            new ProgressParameters(saveId),
            (IStateMachineContext)sndContext,
            sndContext);
    }
}
