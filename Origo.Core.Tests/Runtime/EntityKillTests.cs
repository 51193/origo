using Origo.Core.Runtime.Lifecycle;
using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.DataSource;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Console;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

[Collection("StrategyStateTests")]
public class EntityKillTests
{
    // ── RequestKillEntity ──────────────────────────────────────────────
    [Fact]
    public void RequestKillEntity_TriggersBeforeDead_ViaFlush()
    {
        var (ctx, _, _) = SetupKillTest(registerKillProbe: true);
        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        SpawnEntity(session, "E");

        KillProbeStrategy.Events = [];
        try
        {
            session.RequestKillEntity("E");
            Assert.True(session.FindByName("E")!.IsPendingKill);
            Assert.DoesNotContain("before_dead", KillProbeStrategy.Events); // deferred: not triggered yet

            ctx.Runtime.SessionManager.KillPendingAllSessions();
            Assert.Contains("before_dead", KillProbeStrategy.Events);
            Assert.Null(session.FindByName("E"));
        }
        finally
        {
            KillProbeStrategy.Events = null;
        }
    }

    // ── Manual iteration kill-all (RequestKillAll was removed from ISndContext) ──

    [Fact]
    public void ManualIterateAndRequestKillEntity_MarksAllAliveEntities()
    {
        var (ctx, _) = Setup();
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        session.Spawn(new SndMetaData
        {
            Name = "A",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        });
        session.Spawn(new SndMetaData
        {
            Name = "B",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        });

        foreach (var e in session.GetEntities())
            if (!e.IsPendingKill)
                session.RequestKillEntity(e.Name);

        Assert.True(session.FindByName("E")!.IsPendingKill);
        Assert.True(session.FindByName("A")!.IsPendingKill);
        Assert.True(session.FindByName("B")!.IsPendingKill);
    }

    [Fact]
    public void ManualKillAll_SkipsAlreadyPendingEntities()
    {
        var (ctx, _) = Setup();
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        session.Spawn(new SndMetaData
        {
            Name = "A",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        });

        session.RequestKillEntity("E");
        Assert.True(session.FindByName("E")!.IsPendingKill);

        foreach (var e in session.GetEntities())
            if (!e.IsPendingKill)
                session.RequestKillEntity(e.Name);

        Assert.True(session.FindByName("A")!.IsPendingKill);
    }

    [Fact]
    public void ManualKillAll_RemovesAllAfterFlush()
    {
        var (ctx, host, _) = SetupKillTest();
        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        SpawnEntityWithoutStrategy(session, "A");
        SpawnEntityWithoutStrategy(session, "B");

        foreach (var e in session.GetEntities())
            if (!e.IsPendingKill)
                session.RequestKillEntity(e.Name);
        ctx.Runtime.SessionManager.KillPendingAllSessions();

        Assert.Null(host.FindByName("A"));
        Assert.Null(host.FindByName("B"));
    }

    [Fact]
    public void ManualKillAll_EmptyScene_DoesNotThrow()
    {
        var (ctx, _) = Setup();
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        foreach (var e in session.GetEntities())
            session.RequestKillEntity(e.Name);
        ctx.Runtime.SessionManager.KillPendingAllSessions();
        Assert.Empty(session.GetEntities());

        var ex = Record.Exception(() =>
        {
            foreach (var e in session.GetEntities())
                if (!e.IsPendingKill)
                    session.RequestKillEntity(e.Name);
        });
        Assert.Null(ex);
    }

    // ── KillPendingEntities (unified sweep) ────────────────────────────

    [Fact]
    public void KillPendingEntities_FiresBeforeDead()
    {
        var (ctx, host, _) = SetupKillTest(registerKillProbe: true);
        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        SpawnEntity(session, "E");

        KillProbeStrategy.Events = [];
        try
        {
            session.RequestKillEntity("E");
            ctx.Runtime.SessionManager.KillPendingAllSessions();

            Assert.Contains("before_dead", KillProbeStrategy.Events);
            Assert.Null(host.FindByName("E"));
        }
        finally
        {
            KillProbeStrategy.Events = null;
        }
    }

    [Fact]
    public void KillPendingEntities_BusinessDeferredBeforeKillSweep()
    {
        var (ctx, host, _) = SetupKillTest(registerKillProbe: true);
        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        SpawnEntity(session, "A");
        SpawnEntity(session, "B");
        var events = new List<string>();

        KillProbeStrategy.Events = [];
        try
        {
            ctx.Deferred.EnqueueBusinessDeferred(() =>
            {
                events.Add("business:mark_a");
                session.RequestKillEntity("A");
            });
            ctx.Deferred.EnqueueBusinessDeferred(() =>
            {
                events.Add("business:mark_b");
                session.RequestKillEntity("B");
            });
            ctx.Deferred.EnqueueBusinessDeferred(() => events.Add("business:after_marks"));

            ctx.Deferred.FlushDeferredActionsForCurrentFrame();

            Assert.Equal("business:mark_a", events[0]);
            Assert.Equal("business:mark_b", events[1]);
            Assert.Equal("business:after_marks", events[2]);
            Assert.Contains("before_dead", KillProbeStrategy.Events);
        }
        finally
        {
            KillProbeStrategy.Events = null;
        }
    }

    [Fact]
    public void KillPendingEntities_NoPendingEntities_DoesNotThrow()
    {
        var (ctx, host, _) = SetupKillTest();
        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        SpawnEntityWithoutStrategy(session, "E");

        var ex = Record.Exception(() => ctx.Runtime.SessionManager.KillPendingAllSessions());
        Assert.Null(ex);
        Assert.NotNull(host.FindByName("E"));
    }

    // ── KillPendingAllSessions ──────────────────────────────────────────

    [Fact]
    public void KillPendingAllSessions_RemovesPendingEntities()
    {
        var (ctx, host, _) = SetupKillTest();
        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        SpawnEntityWithoutStrategy(session, "E");

        session.RequestKillEntity("E");
        Assert.True(host.FindByName("E")!.IsPendingKill);

        ctx.Runtime.SessionManager.KillPendingAllSessions();

        Assert.Null(host.FindByName("E"));
    }

    // ── DeadByName ─────────────────────────────────────────────────────

    [Fact]
    public void DeadByName_RemovesEntity()
    {
        var logger = new TestLogger();
        var host = CreateFullMemoryHostWithEntity(logger);

        host.RemoveEntity("E");
        Assert.Null(host.FindByName("E"));
    }

    [Fact]
    public void StubSndSceneHost_DeadByName_RemovesEntity()
    {
        var host = new StubSndSceneHost();
        host.CreateEntity(new SndMetaData { Name = "E" });
        Assert.NotNull(host.FindByName("E"));

        host.RemoveEntity("E");
        Assert.Null(host.FindByName("E"));
    }

    [Fact]
    public void StubSndSceneHost_DeadByName_MissingEntity_Throws()
    {
        var host = new StubSndSceneHost();
        Assert.Throws<InvalidOperationException>(() => host.RemoveEntity("not.exist"));
    }

    // ── StubSndSceneHost RequestKillEntity ───────────────────────────

    [Fact]
    public void StubSndSceneHost_RequestKillEntity_MarksPendingKill()
    {
        var host = new StubSndSceneHost();
        host.CreateEntity(new SndMetaData { Name = "E" });

        var entity = host.FindByName("E");
        Assert.NotNull(entity);
        Assert.False(entity.IsPendingKill);

        host.RequestKillEntity("E");

        Assert.True(entity.IsPendingKill);
        Assert.NotNull(host.FindByName("E")); // still in collection
    }

    [Fact]
    public void StubSndSceneHost_RequestKillEntity_Missing_Throws()
    {
        var host = new StubSndSceneHost();
        Assert.Throws<InvalidOperationException>(() => host.RequestKillEntity("not.exist"));
    }

    [Fact]
    public void StubSndSceneHost_RequestKillEntity_AlreadyPending_Throws()
    {
        var host = new StubSndSceneHost();
        host.CreateEntity(new SndMetaData { Name = "E" });
        host.RequestKillEntity("E");

        Assert.Throws<InvalidOperationException>(() => host.RequestKillEntity("E"));
    }

    // ── IsPendingKill semantics ────────────────────────────────────────

    [Fact]
    public void IsPendingKill_DefaultFalse()
    {
        var host = new StubSndSceneHost();
        host.CreateEntity(new SndMetaData { Name = "E" });

        Assert.False(host.FindByName("E")!.IsPendingKill);
    }

    [Fact]
    public void IsPendingKill_CanBeCheckedByStrategy()
    {
        var (ctx, _, _) = SetupKillTest();
        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        SpawnEntityWithoutStrategy(session, "E");

        session.RequestKillEntity("E");
        var entity = session.FindByName("E");
        Assert.True(entity!.IsPendingKill);

        // Strategy can read it to skip operations on pending-kill entities
        Assert.True(entity.IsPendingKill);
    }

    // ── ClearAll (internal lifecycle) ──────────────────────────────────

    [Fact]
    public void ClearAll_TriggersBeforeQuit()
    {
        var events = new List<string>();
        QuitProbeStrategy.Events = events;
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        world.RegisterStrategy(() => new QuitProbeStrategy());
        host.BindWorld(world);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        host.BindContext(ctx);
        host.RecoverFromMetaList(
        [
            new SndMetaData
            {
                Name = "E",
                NodeMetaData = new NodeMetaData(),
                StrategyMetaData = new StrategyMetaData
                    { LifecycleIndices = ["quit.test.probe"], ActiveIndices = [] },
                DataMetaData = new DataMetaData()
            }
        ]);

        try
        {
            foreach (var e in host.GetEntities())
                if (e is IEntityLifecycle lc)
                {
                    lc.FireBeforeQuitHooks();
                    lc.ReleaseStrategiesOnly();
                    lc.TeardownOnly();
                }

            host.RemoveAllEntities();
            Assert.Contains("before_quit", events);
            Assert.Null(host.FindByName("E"));
        }
        finally
        {
            QuitProbeStrategy.Events = null;
        }
    }

    // ── Console kill_all command ───────────────────────────────────────

    [Fact]
    public void KillAllCommand_MarksAllEntities()
    {
        var logger = new TestLogger();
        var host = new StubSndSceneHost();

        var runtime = CreateRuntimeWithConsole(logger, host);
        TestFactory.BootstrapForegroundSession(runtime);
        var session = runtime.SessionManager.ForegroundSession!;

        session.Spawn(new SndMetaData { Name = "A" });
        session.Spawn(new SndMetaData { Name = "B" });

        runtime.ConsoleInput!.Enqueue("kill_all");
        runtime.Console!.ProcessPending();

        Assert.True(session.FindByName("A")!.IsPendingKill);
        Assert.True(session.FindByName("B")!.IsPendingKill);
    }

    [Fact]
    public void KillAllCommand_SkipsAlreadyPending()
    {
        var logger = new TestLogger();
        var host = new StubSndSceneHost();

        var runtime = CreateRuntimeWithConsole(logger, host);
        TestFactory.BootstrapForegroundSession(runtime);
        var session = runtime.SessionManager.ForegroundSession!;

        session.Spawn(new SndMetaData { Name = "A" });
        session.Spawn(new SndMetaData { Name = "B" });
        session.RequestKillEntity("A");

        runtime.ConsoleInput!.Enqueue("kill_all");
        runtime.Console!.ProcessPending();

        Assert.True(session.FindByName("A")!.IsPendingKill);
        Assert.True(session.FindByName("B")!.IsPendingKill);
    }

    // ── Full integration: Process → RequestKill → Flush ────────────────

    [Fact]
    public void FullCycle_ProcessMarksThenFlushRemoves()
    {
        var (ctx, host, _) = SetupKillTest();
        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        SpawnEntityWithoutStrategy(session, "E");

        var entity = host.FindByName("E");
        Assert.NotNull(entity);
        entity.SetData("should_kill", true);

        ctx.Runtime.SessionManager.ProcessAllSessions(0.016, includeForeground: true);
        session.RequestKillEntity("E");
        Assert.True(entity.IsPendingKill);

        ctx.Runtime.SessionManager.KillPendingAllSessions();

        Assert.Null(host.FindByName("E"));
    }

    // ── Helpers ────────────────────────────────────────────────────────

    /// <summary>
    ///     创建完整 SndContext + FullMemorySndSceneHost 作为前台,经 RequestLoadMainMenuEntrySave
    ///     建立前台会话。所有实体生命周期（请求 / 收割）经 ctx 走 SessionManager 管线。
    /// </summary>
    private static (SndContext ctx, FullMemorySndSceneHost host, TestLogger logger) SetupKillTest(
        bool registerKillProbe = false)
    {
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        if (registerKillProbe)
            world.RegisterStrategy(() => new KillProbeStrategy());
        host.BindWorld(world);
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
        var io = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "initial", "entry.json"));
        host.BindContext(ctx);
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();
        return (ctx, host, logger);
    }

    private static ISndEntity SpawnEntityWithoutStrategy(ISessionRun session, string name)
    {
        var meta = new SndMetaData
        {
            Name = name,
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        };
        return session.Spawn(meta);
    }

    private static ISndEntity SpawnEntity(ISessionRun session, string name)
    {
        var meta = new SndMetaData
        {
            Name = name,
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData
            {
                LifecycleIndices = ["kill.test.lifecycle"],
                ActiveIndices = []
            },
            DataMetaData = new DataMetaData()
        };
        return session.Spawn(meta);
    }

    private static FullMemorySndSceneHost CreateFullMemoryHostWithEntity(TestLogger logger)
    {
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        world.RegisterStrategy(() => new KillProbeStrategy());
        host.BindWorld(world);
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
        var io = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "initial", "entry.json"));
        host.BindContext(ctx);
        host.RecoverFromMetaList(
        [
            new SndMetaData
            {
                Name = "E",
                NodeMetaData = new NodeMetaData(),
                StrategyMetaData = new StrategyMetaData
                    { LifecycleIndices = ["kill.test.lifecycle"], ActiveIndices = [] },
                DataMetaData = new DataMetaData()
            }
        ]);
        return host;
    }

    private static (SndContext ctx, TestLogger logger) Setup()
    {
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var fs = new TestMemoryFileSystem();
        var entryJson = """
                        {
                          "levels": {
                            "main_menu": { "snd_scene": "res://levels/main_menu.json", "type": "main_menu" }
                          },
                          "main_menu_level": "main_menu"
                        }
                        """;
        fs.SeedFile("entry.json", entryJson);
        fs.SeedFile(
            "res://levels/main_menu.json",
            """
            [
              {
                "name": "E",
                "node": { "pairs": {} },
                "strategy": { "lifecycle_indices": [], "active_indices": [] },
                "data": { "pairs": {} }
              }
            ]
            """);
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        return (ctx, logger);
    }

    private static OrigoRuntime CreateRuntimeWithConsole(ILogger logger, ISndSceneHost host)
    {
        var input = new ConsoleInputBuffer();
        var output = new ConsoleOutputChannel();
        return new OrigoRuntime(
            new OrigoMeta("Origo", "test", string.Empty),
            logger,
            host,
            new TypeStringMapping(),
            DataSourceFactory.CreateDefaultRegistry(new TypeStringMapping()),
            DataSourceFactory.CreateDefaultIoGateway(new TestMemoryFileSystem()),
            new Blackboard.Blackboard(),
            input,
            output);
    }

    [StrategyIndex("kill.test.lifecycle")]
    private sealed class KillProbeStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _events = new();
        public static List<string>? Events { get => _events.Value; set => _events.Value = value; }

        public override void BeforeDead(ISndEntity entity, ISndContext ctx) => Events?.Add("before_dead");
    }

    [StrategyIndex("quit.test.probe")]
    private sealed class QuitProbeStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _events = new();
        public static List<string>? Events { get => _events.Value; set => _events.Value = value; }

        public override void BeforeQuit(ISndEntity entity, ISndContext ctx) => Events?.Add("before_quit");
    }
}
