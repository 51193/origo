using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class EntityKillTests
{
    [Fact]
    public void RequestKillEntity_RemovesEntity()
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
    public void RequestKillEntity_MissingEntity_NoError()
    {
        var (ctx, _) = Setup();
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        var ex = Record.Exception(() =>
        {
            ctx.RequestKillEntity("not.exist");
            ctx.FlushDeferredActionsForCurrentFrame();
        });
        Assert.Null(ex);
    }

    [Fact]
    public void EntityKill_TriggersBeforeDead()
    {
        var logger = new TestLogger();
        var host = CreateFullMemoryHostWithEntity(logger);
        var entity = host.FindByName("E");
        Assert.NotNull(entity);

        KillProbeStrategy.Events = new List<string>();
        try
        {
            entity.Kill();

            Assert.Contains("before_dead", KillProbeStrategy.Events);
        }
        finally
        {
            KillProbeStrategy.Events = null;
        }
    }

    [Fact]
    public void DeadByName_CallsEntityKill()
    {
        var logger = new TestLogger();
        var host = CreateFullMemoryHostWithEntity(logger);

        KillProbeStrategy.Events = new List<string>();
        try
        {
            host.DeadByName("E");
            Assert.Null(host.FindByName("E"));
            Assert.Contains("before_dead", KillProbeStrategy.Events);
        }
        finally
        {
            KillProbeStrategy.Events = null;
        }
    }

    [Fact]
    public void MemorySndSceneHost_DeadByName_RemovesEntity()
    {
        var host = new MemorySndSceneHost();
        host.Spawn(new SndMetaData { Name = "E" });
        Assert.NotNull(host.FindByName("E"));

        host.DeadByName("E");
        Assert.Null(host.FindByName("E"));
    }

    [Fact]
    public void MemorySndSceneHost_DeadByName_MissingEntity_NoError()
    {
        var host = new MemorySndSceneHost();
        var ex = Record.Exception(() => host.DeadByName("not.exist"));
        Assert.Null(ex);
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
        var ctx = new SndContext(new SndContextParameters(runtime, fs, "root", "initial", "entry.json"));
        return (ctx, logger);
    }

    private static FullMemorySndSceneHost CreateFullMemoryHostWithEntity(TestLogger logger)
    {
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        world.RegisterStrategy(() => new KillProbeStrategy());
        host.BindWorld(world);
        var fs = new TestFileSystem();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var ctx = new SndContext(new SndContextParameters(runtime, fs, "root", "initial", "entry.json"));
        host.BindContext(ctx);
        host.LoadFromMetaList(new[]
        {
            new SndMetaData
            {
                Name = "E",
                NodeMetaData = new NodeMetaData(),
                StrategyMetaData = new StrategyMetaData
                {
                    EntityIndices = new List<string> { "kill.test.lifecycle" },
                    ActiveIndices = new List<string>()
                },
                DataMetaData = new DataMetaData()
            }
        });
        return host;
    }

    [StrategyIndex("kill.test.lifecycle")]
    private sealed class KillProbeStrategy : EntityStrategyBase
    {
        public static List<string>? Events { get; set; }

        public override void BeforeDead(ISndEntity entity, ISndContext ctx)
        {
            Events?.Add("before_dead");
        }
    }
}