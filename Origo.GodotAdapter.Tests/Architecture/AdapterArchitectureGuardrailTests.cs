using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Origo.Core;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Node;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.Snd;
using Origo.Core.Blackboard;
using Origo.Core.DataSource;
using Origo.Core.Runtime;
using Origo.Core.Serialization;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.GodotAdapter.Tests.Architecture;

public class AdapterArchitectureGuardrailTests
{
    [Fact]
    public void SndContext_AllRoleInterfaces_AreAccessibleThroughISndContext()
    {
        var runtime = CreateSimpleOrigoRuntime();
        var fs = new InMemoryFileSystem();

        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial", "entry.json"));

        ISndBlackboardAccess bb = ctx;
        bb.SystemBlackboard.Set("k", 1);
        Assert.Equal(1, bb.SystemBlackboard.TryGet<int>("k").value);

        ISndDeferredActions def = ctx;
        var ran = false;
        def.EnqueueBusinessDeferred(() => ran = true);
        def.FlushDeferredActionsForCurrentFrame();
        Assert.True(ran);

        ISndSessionAccess session = ctx;
        Assert.NotNull(session.SessionManager);

        ISndSaveOperations save = ctx;
        Assert.Empty(save.ListSaves());

        ISndLifecycleOperations lifecycle = ctx;
        Assert.False(lifecycle.HasContinueData());

        ISndConsoleAccess console = ctx;
        Assert.False(console.TrySubmitConsoleCommand(""));

        ISndFileAccess fileAccess = ctx;
        Assert.False(fileAccess.FileExists("nonexistent.json"));

        ISndArchiveFileAccess archiveFileAccess = ctx;
        Assert.False(archiveFileAccess.FileExists("nonexistent.json"));
    }

    [Fact]
    public void SndContext_ViaSessionManager_CanCreateAndDestroyBackgroundSessions()
    {
        var runtime = CreateSimpleOrigoRuntime();
        var fs = new InMemoryFileSystem();

        fs.SeedFile("entry.json", "[]");
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial", "entry.json"));

        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        var bg = ctx.SessionManager.CreateBackgroundSession("bg_sess", "bg_level");
        bg.SessionBlackboard.Set("bg_data", "bg_value");

        var (found, val) = bg.SessionBlackboard.TryGet<string>("bg_data");
        Assert.True(found);
        Assert.Equal("bg_value", val);

        Assert.True(ctx.SessionManager.Contains("bg_sess"));
        ctx.SessionManager.DestroySession("bg_sess");
        Assert.False(ctx.SessionManager.Contains("bg_sess"));

        bg.Dispose();
    }

    private static OrigoRuntime CreateSimpleOrigoRuntime()
    {
        var logger = new InMemoryLogger();
        var host = new InMemorySndSceneHost();
        var tm = new TypeStringMapping();
        var reg = DataSourceFactory.CreateDefaultRegistry(tm);
        var fs = new InMemoryFileSystem();
        var io = DataSourceFactory.CreateDefaultIoGateway(fs);
        var systemBb = new Blackboard();
        var meta = new OrigoMeta("Origo", "test", string.Empty);

        return new OrigoRuntime(meta, logger, host, tm, reg, io, systemBb);
    }

    private sealed class InMemoryLogger : ILogger
    {
        public void Log(LogLevel level, string tag, string message)
        {
        }
    }

    private sealed class InMemorySndSceneHost : ISndSceneHost
    {
        private readonly List<ISndEntity> _entities = new();
        public IReadOnlyCollection<ISndEntity> GetEntities() => _entities;
        public ISndEntity? FindByName(string name) => null;
        public IReadOnlyList<SndMetaData> BuildMetaList() => Array.Empty<SndMetaData>();

        public void RecoverFromMetaList(IEnumerable<SndMetaData> metaList)
        {
            _entities.Clear();
            foreach (var _ in metaList)
                _entities.Add(new InMemorySndEntity("loaded"));
        }

        public void RemoveAllEntities() => _entities.Clear();

        public void ProcessAll(double delta)
        {
        }

        public void RemoveEntity(string name)
        {
        }

        public void RequestKillEntity(string name)
        {
        }

        public ISndEntity CreateEntity(SndMetaData metaData)
        {
            var entity = new InMemorySndEntity(metaData.Name ?? "unnamed");
            _entities.Add(entity);
            return entity;
        }
    }

    private sealed class InMemorySndEntity : ISndEntity
    {
        private readonly Dictionary<string, object?> _data = new(StringComparer.Ordinal);

        public InMemorySndEntity(string name)
        {
            _data["name"] = name;
        }

        public string Name => (string)_data["name"]!;
        public bool IsPendingKill { get; set; }

        public void SetData<T>(string name, T value) => _data[name] = value;
        public T GetData<T>(string name) => _data.TryGetValue(name, out var v) && v is T c ? c : default!;

        public (bool found, T? value) TryGetData<T>(string name) =>
            _data.TryGetValue(name, out var v) && v is T c ? (true, c) : (false, default);

        public void Subscribe(string name, Action<ISndEntity, ISndEntity, TypedData, TypedData> cb,
            Func<ISndEntity, ISndEntity, TypedData, TypedData, bool>? filter = null)
        {
        }

        public void Unsubscribe(string name, Action<ISndEntity, ISndEntity, TypedData, TypedData> cb)
        {
        }

        public void SubscribeLifecycle(Action<ISndEntity, ISndEntity, EntityLifecycleEvent> callback)
        {
        }

        public void UnsubscribeLifecycle(Action<ISndEntity, ISndEntity, EntityLifecycleEvent> callback)
        {
        }

        public void ObserveData(ISndEntity target, string dataName,
            Action<ISndEntity, ISndEntity, TypedData, TypedData> callback,
            Func<ISndEntity, ISndEntity, TypedData, TypedData, bool>? filter = null)
        {
        }

        public void UnobserveData(ISndEntity target, string dataName,
            Action<ISndEntity, ISndEntity, TypedData, TypedData> callback)
        {
        }

        public void ObserveLifecycle(ISndEntity target,
            Action<ISndEntity, ISndEntity, EntityLifecycleEvent> callback)
        {
        }

        public void UnobserveLifecycle(ISndEntity target,
            Action<ISndEntity, ISndEntity, EntityLifecycleEvent> callback)
        {
        }

        public INodeHandle GetNode(string name) =>
            throw new InvalidOperationException($"Node '{name}' not found.");

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
    }

    private sealed class InMemoryFileSystem : IFileSystem
    {
        private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
        public bool Exists(string path) => _files.ContainsKey(Normalize(path));

        public bool DirectoryExists(string path) =>
            _files.Keys.Any(f => f.StartsWith(Normalize(path).TrimEnd('/') + "/", StringComparison.Ordinal));

        public string ReadAllText(string path) => _files[Normalize(path)];

        public void WriteAllText(string path, string content, bool overwrite)
        {
            var n = Normalize(path);
            if (!overwrite && _files.ContainsKey(n)) throw new IOException($"File exists: {n}");
            _files[n] = content;
        }

        public void Copy(string s, string d, bool o) => WriteAllText(d, ReadAllText(s), o);
        public IEnumerable<string> EnumerateFiles(string dir, string pattern, bool recursive) => Array.Empty<string>();

        public void CreateDirectory(string path)
        {
        }

        public IEnumerable<string> EnumerateDirectories(string directoryPath) => Array.Empty<string>();
        public void Delete(string path) => _files.Remove(Normalize(path));

        public void DeleteDirectory(string path)
        {
            var prefix = Normalize(path).TrimEnd('/') + "/";
            foreach (var f in _files.Keys.Where(f => f.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
                _files.Remove(f);
        }

        public void Rename(string s, string d)
        {
            var sp = Normalize(s).TrimEnd('/') + "/";
            foreach (var f in _files.Keys.Where(f => f.StartsWith(sp, StringComparison.Ordinal) || f == Normalize(s))
                         .ToArray())
            {
                _files[string.Concat(Normalize(d), f.AsSpan(Normalize(s).Length))] = _files[f];
                _files.Remove(f);
            }
        }

        public string CombinePath(string b, string r) => Normalize(b).TrimEnd('/') + "/" + r;

        public string GetParentDirectory(string p)
        {
            var n = Normalize(p).TrimEnd('/');
            var i = n.LastIndexOf('/');
            return i <= 0 ? "" : n[..i];
        }

        public void SeedFile(string path, string content) => _files[Normalize(path)] = content;
        private static string Normalize(string p) => p.Replace('\\', '/').Trim();
    }
}

public class CommandHandlerBaseVisibilityTests
{
    [Fact]
    public void CommandHandlerBase_ShouldBePublic_SoExternalProjectsCanExtendIt()
    {
        var type = typeof(Origo.GodotAdapter.Console.CommandHandlerBase);
        Assert.True(type.IsPublic || type.IsNestedPublic,
            "CommandHandlerBase must be public so external projects " +
            "(such as origo.demo) can derive custom adapter console command handlers.");
    }
}
