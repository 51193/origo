using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// 1. SndContext save / load / continue workflows
// ─────────────────────────────────────────────────────────────────────────────

public class EntityStrategyBaseTests
{
    [Fact]
    public void DefaultHooks_DoNotMutateEntityData()
    {
        var strategy = new TestEntityStrategy();
        var entity = new StubSndEntity("e");
        entity.SetData("score", 7);
        ISndContext ctx = NullSndContext.Instance;

        strategy.Process(entity, 0.016, ctx);
        strategy.AfterSpawn(entity, ctx);
        strategy.AfterLoad(entity, ctx);
        strategy.AfterAdd(entity, ctx);
        strategy.BeforeRemove(entity, ctx);
        strategy.BeforeSave(entity, ctx);
        strategy.BeforeQuit(entity, ctx);
        strategy.BeforeDead(entity, ctx);

        Assert.Equal(7, entity.GetData<int>("score"));
    }

    [Fact]
    public void Process_AddsNewStrategy_DoesNotThrow()
    {
        var strategy = new TestEntityStrategyWithAdd();
        var entity = new StubSndEntity("e");
        ISndContext ctx = NullSndContext.Instance;

        var ex = Record.Exception(() => strategy.Process(entity, 0.016, ctx));
        Assert.Null(ex);
    }

    [Fact]
    public void Process_KillsItself_MarksEntity()
    {
        var host = new StubSndSceneHost();
        var meta = new SndMetaData { Name = "e" };
        var entity = host.CreateEntity(meta);
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        fs.SeedFile("entry.json", "[]");
        var ctx = new SndContext(new SndContextParameters(runtime, fs, "root", "res://initial", "entry.json"));

        var strategy = new TestEntityStrategyKillSelf();
        strategy.Process(entity, 0.016, ctx);

        Assert.True(entity.IsPendingKill);
    }

    [Fact]
    public void Remove_NonexistentStrategy_DoesNotThrow()
    {
        var entity = new StubSndEntity("e");

        var ex = Record.Exception(() => entity.RemoveStrategy("nonexistent.index"));
        Assert.Null(ex);
    }

    private sealed class TestEntityStrategy : EntityStrategyBase
    {
    }

    private sealed class TestEntityStrategyWithAdd : EntityStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            entity.AddStrategy("some.new.strategy");
        }
    }

    private sealed class TestEntityStrategyKillSelf : EntityStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            ctx.RequestKillEntity(entity.Name);
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 6. StateMachineStrategyBase — virtual hooks coverage
// ─────────────────────────────────────────────────────────────────────────────
