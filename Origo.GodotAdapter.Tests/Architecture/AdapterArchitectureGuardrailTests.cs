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

namespace Origo.GodotAdapter.Tests;

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

        var bb = ctx.Blackboard;
        bb.SystemBlackboard.SetValue("k", 1);
        Assert.Equal(1, bb.SystemBlackboard.TryGet<int>("k").value);

        var def = ctx.Deferred;
        var ran = false;
        def.EnqueueBusinessDeferred(() => ran = true);
        def.FlushDeferredActionsForCurrentFrame();
        Assert.True(ran);

        Assert.NotNull(ctx.Runtime.SessionManager);

        var save = ctx.Save;
        Assert.Empty(save.ListSaves());

        var lifecycle = ctx.Lifecycle;
        Assert.False(lifecycle.HasContinueData());

        var console = ctx.ConsoleAccess;
        Assert.False(console.TrySubmitConsoleCommand(""));

        ISndFileAccess fileAccess = ctx.FileAccess;
        Assert.False(fileAccess.FileExists("nonexistent.json"));

        ISndArchiveFileAccess archiveFileAccess = ctx.ArchiveFileAccess;
        Assert.False(archiveFileAccess.FileExists("nonexistent.json"));

        var template = ctx.Template;
        Assert.NotNull(template);

        var sm = ctx.StateMachines;
        Assert.Null(sm.GetProgressStateMachines());

        var smCtx = ctx.StateMachineContext;
        Assert.Same(bb.SystemBlackboard, smCtx.SystemBlackboard);
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

        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

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
        private readonly List<ISndEntity> _entities = [];
        public IReadOnlyCollection<ISndEntity> GetEntities() => _entities;
        public ISndEntity? FindByName(string name) => null;
        public IReadOnlyList<SndMetaData> BuildMetaList() => [];

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
