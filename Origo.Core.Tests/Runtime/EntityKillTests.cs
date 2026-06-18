using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
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
    public void RequestKillEntity_RemovesEntity_AfterFlush()
    {
        var (ctx, _) = Setup();
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.NotNull(ctx.CurrentSession);
        var host = ctx.CurrentSession!.SceneHost;
        Assert.NotNull(host.FindByName("E"));

        ctx.RequestKillEntity("E");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.Null(host.FindByName("E"));
    }

    [Fact]
    public void RequestKillEntity_MissingEntity_Throws()
    {
        var (ctx, _) = Setup();
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.Throws<InvalidOperationException>(() => { ctx.RequestKillEntity("not.exist"); });
    }

    [Fact]
    public void RequestKillEntity_AlreadyPending_Throws()
    {
        var (ctx, _) = Setup();
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        ctx.RequestKillEntity("E");
        Assert.Throws<InvalidOperationException>(() => { ctx.RequestKillEntity("E"); });
    }

    [Fact]
    public void RequestKillEntity_MarksIsPendingKill_Immediately()
    {
        var (ctx, _) = Setup();
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        var host = ctx.CurrentSession!.SceneHost;
        var entity = host.FindByName("E");
        Assert.NotNull(entity);
        Assert.False(entity.IsPendingKill);

        ctx.RequestKillEntity("E");

        Assert.True(entity.IsPendingKill);
        Assert.NotNull(host.FindByName("E")); // entity still in collection
    }

    [Fact]
    public void RequestKillEntity_NullOrWhiteSpaceName_Throws()
    {
        var (ctx, _) = Setup();
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.Throws<ArgumentException>(() => ctx.RequestKillEntity(null!));
        Assert.Throws<ArgumentException>(() => ctx.RequestKillEntity(""));
        Assert.Throws<ArgumentException>(() => ctx.RequestKillEntity("  "));
    }

    [Fact]
    public void RequestKillEntity_TriggersBeforeDead_ViaFlush()
    {
        var logger = new TestLogger();
        var host = CreateFullMemoryHostWithEntity(logger);

        KillProbeStrategy.Events = new List<string>();
        try
        {
            host.RequestKillEntity("E");
            Assert.True(host.FindByName("E")!.IsPendingKill);
            Assert.DoesNotContain("before_dead", KillProbeStrategy.Events); // not triggered yet

            var entity = host.FindByName("E");
            if (entity is IEntityLifecycle lc)
                lc.FireBeforeDeadHooks();
            host.RemoveEntity("E");
            Assert.Contains("before_dead", KillProbeStrategy.Events);
        }
        finally
        {
            KillProbeStrategy.Events = null;
        }
    }

    // ── RequestKillAll ─────────────────────────────────────────────────

    [Fact]
    public void RequestKillAll_MarksAllAliveEntities()
    {
        var (ctx, _) = Setup();
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        var host = ctx.CurrentSession!.SceneHost;
        host.CreateEntity(new SndMetaData
        {
            Name = "A", NodeMetaData = new NodeMetaData(), StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        });
        host.CreateEntity(new SndMetaData
        {
            Name = "B", NodeMetaData = new NodeMetaData(), StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        });

        ctx.RequestKillAll();

        Assert.True(host.FindByName("E")!.IsPendingKill);
        Assert.True(host.FindByName("A")!.IsPendingKill);
        Assert.True(host.FindByName("B")!.IsPendingKill);
    }

    [Fact]
    public void RequestKillAll_SkipsAlreadyPendingEntities()
    {
        var (ctx, _) = Setup();
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        var host = ctx.CurrentSession!.SceneHost;
        host.CreateEntity(new SndMetaData
        {
            Name = "A", NodeMetaData = new NodeMetaData(), StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        });

        ctx.RequestKillEntity("E");
        Assert.True(host.FindByName("E")!.IsPendingKill);

        ctx.RequestKillAll(); // should not throw for "E"

        Assert.True(host.FindByName("A")!.IsPendingKill);
    }

    [Fact]
    public void RequestKillAll_RemovesAllAfterFlush()
    {
        var (ctx, _) = Setup();
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        var host = ctx.CurrentSession!.SceneHost;
        host.CreateEntity(new SndMetaData
        {
            Name = "A", NodeMetaData = new NodeMetaData(), StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        });
        host.CreateEntity(new SndMetaData
        {
            Name = "B", NodeMetaData = new NodeMetaData(), StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        });

        ctx.RequestKillAll();
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.Null(host.FindByName("E"));
        Assert.Null(host.FindByName("A"));
        Assert.Null(host.FindByName("B"));
    }

    [Fact]
    public void RequestKillAll_EmptyScene_DoesNotThrow()
    {
        var (ctx, _) = Setup();
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        var host = ctx.CurrentSession!.SceneHost;
        host.RemoveAllEntities(); // remove all

        var ex = Record.Exception(() => ctx.RequestKillAll());
        Assert.Null(ex);
    }

    // ── KillPendingEntities (unified sweep) ────────────────────────────

    [Fact]
    public void KillPendingEntities_RemovesMarkedEntities()
    {
        var logger = new TestLogger();
        var host = CreateFullMemoryHostWithEntity(logger);

        Assert.NotNull(host.FindByName("E"));
        host.RequestKillEntity("E");
        Assert.True(host.FindByName("E")!.IsPendingKill);

        var runtime = CreateKillPendingRuntime(logger, host);
        runtime.FlushEndOfFrameDeferred(); // triggers KillPendingEntities

        Assert.Null(host.FindByName("E"));
    }

    [Fact]
    public void KillPendingEntities_FiresBeforeDead()
    {
        var logger = new TestLogger();
        var host = CreateFullMemoryHostWithEntity(logger);

        KillProbeStrategy.Events = new List<string>();
        try
        {
            host.RequestKillEntity("E");

            var runtime = CreateKillPendingRuntime(logger, host);
            runtime.FlushEndOfFrameDeferred();

            Assert.Contains("before_dead", KillProbeStrategy.Events);
        }
        finally
        {
            KillProbeStrategy.Events = null;
        }
    }

    [Fact]
    public void KillPendingEntities_Order_BusinessBeforeKill()
    {
        var logger = new TestLogger();
        var host = CreateFullMemoryHostWithMultiple(logger, "A", "B");
        var events = new List<string>();

        KillProbeStrategy.Events = new List<string>();
        try
        {
            // Mark A via business deferred
            var runtime = CreateKillPendingRuntime(logger, host);
            runtime.EnqueueBusinessDeferred(() =>
            {
                events.Add("business:mark_a");
                host.RequestKillEntity("A");
            });
            runtime.EnqueueBusinessDeferred(() =>
            {
                events.Add("business:mark_b");
                host.RequestKillEntity("B");
            });
            runtime.EnqueueBusinessDeferred(() => events.Add("business:after_marks"));

            runtime.EnqueueSystemDeferred(() => events.Add("system"));

            runtime.FlushEndOfFrameDeferred();

            // business marks ran first, then KillPendingEntities (triggers BeforeDead), then system
            Assert.Equal("business:mark_a", events[0]);
            Assert.Equal("business:mark_b", events[1]);
            Assert.Equal("business:after_marks", events[2]);
            Assert.Contains("before_dead", KillProbeStrategy.Events);
            Assert.Equal("system", events[3]);
        }
        finally
        {
            KillProbeStrategy.Events = null;
        }
    }

    [Fact]
    public void KillPendingEntities_NoPendingEntities_DoesNotThrow()
    {
        var logger = new TestLogger();
        var host = CreateFullMemoryHostWithEntity(logger);

        var runtime = CreateKillPendingRuntime(logger, host);
        var ex = Record.Exception(() => runtime.FlushEndOfFrameDeferred());
        Assert.Null(ex);
        Assert.NotNull(host.FindByName("E")); // entity still alive
    }

    // ── DeadByName ─────────────────────────────────────────────────────

    [Fact]
    public void DeadByName_TriggersBeforeDead()
    {
        var logger = new TestLogger();
        var host = CreateFullMemoryHostWithEntity(logger);
        Assert.NotNull(host.FindByName("E"));

        KillProbeStrategy.Events = new List<string>();
        try
        {
            var entity = host.FindByName("E");
            if (entity is IEntityLifecycle lc)
                lc.FireBeforeDeadHooks();
            host.RemoveEntity("E");

            Assert.Contains("before_dead", KillProbeStrategy.Events);
        }
        finally
        {
            KillProbeStrategy.Events = null;
        }
    }

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
    public void StubSndSceneHost_DeadByName_MissingEntity_NoError()
    {
        var host = new StubSndSceneHost();
        var ex = Record.Exception(() => host.RemoveEntity("not.exist"));
        Assert.Null(ex);
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
        var logger = new TestLogger();
        var host = CreateFullMemoryHostWithEntity(logger);

        host.RequestKillEntity("E");
        var entity = host.FindByName("E");
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
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        host.BindContext(ctx);
        host.RecoverFromMetaList(new[]
        {
            new SndMetaData
            {
                Name = "E",
                NodeMetaData = new NodeMetaData(),
                StrategyMetaData = new StrategyMetaData
                    { EntityIndices = new List<string> { "quit.test.probe" }, ActiveIndices = new List<string>() },
                DataMetaData = new DataMetaData()
            }
        });

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
        host.CreateEntity(new SndMetaData { Name = "A" });
        host.CreateEntity(new SndMetaData { Name = "B" });

        var runtime = CreateRuntimeWithConsole(logger, host);
        runtime.ConsoleInput!.Enqueue("kill_all");
        runtime.Console!.ProcessPending();

        Assert.True(host.FindByName("A")!.IsPendingKill);
        Assert.True(host.FindByName("B")!.IsPendingKill);
    }

    [Fact]
    public void KillAllCommand_SkipsAlreadyPending()
    {
        var logger = new TestLogger();
        var host = new StubSndSceneHost();
        host.CreateEntity(new SndMetaData { Name = "A" });
        host.CreateEntity(new SndMetaData { Name = "B" });
        host.RequestKillEntity("A");

        var runtime = CreateRuntimeWithConsole(logger, host);
        runtime.ConsoleInput!.Enqueue("kill_all");
        runtime.Console!.ProcessPending();

        Assert.True(host.FindByName("A")!.IsPendingKill);
        Assert.True(host.FindByName("B")!.IsPendingKill);
    }

    // ── Full integration: Process → RequestKill → Flush ────────────────

    [Fact]
    public void FullCycle_ProcessMarksThenFlushRemoves()
    {
        var logger = new TestLogger();
        var host = CreateFullMemoryHostWithEntity(logger);

        // Simulate a frame: strategy Process calls RequestKillEntity
        var entity = host.FindByName("E");
        Assert.NotNull(entity);

        entity.SetData("should_kill", true);

        // Frame: Process runs, then Flush removes
        host.ProcessAll(0.016);
        host.RequestKillEntity("E");
        Assert.True(entity.IsPendingKill);

        // End of frame: KillPendingEntities + DeadByName
        var runtime = CreateKillPendingRuntime(logger, host);
        runtime.FlushEndOfFrameDeferred();

        Assert.Null(host.FindByName("E"));
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static (SndContext ctx, TestLogger logger) Setup()
    {
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var fs = new TestFileSystem();
        var entryJson = """
                        [
                          {
                            "name": "E",
                            "node": { "pairs": {} },
                            "strategy": { "entity_indices": [], "active_indices": [] },
                            "data": { "pairs": {} }
                          }
                        ]
                        """;
        fs.SeedFile("entry.json", entryJson);
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        return (ctx, logger);
    }

    private static FullMemorySndSceneHost CreateFullMemoryHostWithEntity(TestLogger logger) =>
        CreateFullMemoryHostWithMultiple(logger, "E");

    private static FullMemorySndSceneHost CreateFullMemoryHostWithMultiple(TestLogger logger, params string[] names)
    {
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        world.RegisterStrategy(() => new KillProbeStrategy());
        host.BindWorld(world);
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        host.BindContext(ctx);

        var metas = names.Select(n => new SndMetaData
        {
            Name = n,
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData
            {
                EntityIndices = new List<string> { "kill.test.lifecycle" },
                ActiveIndices = new List<string>()
            },
            DataMetaData = new DataMetaData()
        }).ToArray();

        host.RecoverFromMetaList(metas);
        return host;
    }

    private static OrigoRuntime CreateKillPendingRuntime(ILogger logger, ISndSceneHost host) =>
        TestFactory.CreateRuntime(logger, host);

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
            DataSourceFactory.CreateDefaultIoGateway(new TestFileSystem()),
            new Blackboard.Blackboard(),
            input,
            output);
    }

    [StrategyIndex("kill.test.lifecycle")]
    private sealed class KillProbeStrategy : LifecycleStrategyBase
    {
        public static List<string>? Events { get; set; }

        public override void BeforeDead(ISndEntity entity, ISndContext ctx) => Events?.Add("before_dead");
    }

    [StrategyIndex("quit.test.probe")]
    private sealed class QuitProbeStrategy : LifecycleStrategyBase
    {
        public static List<string>? Events { get; set; }

        public override void BeforeQuit(ISndEntity entity, ISndContext ctx) => Events?.Add("before_quit");
    }
}
