using System;
using System.Collections.Generic;
using Origo.Core;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
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
        var fs = new MemoryFileSystem();

        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial", "entry.json"));

        ISndBlackboardAccess bb = ctx;
        bb.SystemBlackboard.SetValue("k", 1);
        Assert.Equal(1, bb.SystemBlackboard.TryGet<int>("k").value);

        ISndDeferredActions def = ctx;
        var ran = false;
        def.EnqueueBusinessDeferred(() => ran = true);
        def.FlushDeferredActionsForCurrentFrame();
        Assert.True(ran);

        Assert.NotNull(ctx.Runtime.SessionManager);

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
        var fs = new MemoryFileSystem();

        fs.WriteAllText("entry.json", "[]", true);
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial", "entry.json"));

        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg_sess", "bg_level");
        bg.SessionBlackboard.SetValue("bg_data", "bg_value");

        var (found, val) = bg.SessionBlackboard.TryGet<string>("bg_data");
        Assert.True(found);
        Assert.Equal("bg_value", val);

        Assert.True(ctx.Runtime.SessionManager.Contains("bg_sess"));
        ctx.Runtime.SessionManager.DestroySession("bg_sess");
        Assert.False(ctx.Runtime.SessionManager.Contains("bg_sess"));

        bg.Dispose();
    }

    private static OrigoRuntime CreateSimpleOrigoRuntime()
    {
        var logger = new InMemoryLogger();
        var host = new InMemorySndSceneHost();
        var tm = new TypeStringMapping();
        var reg = DataSourceFactory.CreateDefaultRegistry(tm);
        var fs = new MemoryFileSystem();
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

        public ISessionRun OwningSession { get; set; } = null!;

        public void SetData<T>(string name, T value) => _data[name] = value;
        public T GetData<T>(string name) => _data.TryGetValue(name, out var v) && v is T c ? c : default!;

        public (bool found, T? value) TryGetData<T>(string name) =>
            _data.TryGetValue(name, out var v) && v is T c ? (true, c) : (false, default);

        public void MountObserverStrategy(string targetName, string observerIndex) { }

        public void UnmountObserverStrategy(string targetName, string observerIndex) { }
    public void MountObserverStrategy(Origo.Core.Abstractions.Entity.ISndEntity target, string observerIndex) { }
    public void UnmountObserverStrategy(Origo.Core.Abstractions.Entity.ISndEntity target, string observerIndex) { }

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
