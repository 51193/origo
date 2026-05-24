using Origo.Core.Snd;
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
        var entity = new MemorySndEntity("e");
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

    private sealed class TestEntityStrategy : EntityStrategyBase
    {
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 6. StateMachineStrategyBase — virtual hooks coverage
// ─────────────────────────────────────────────────────────────────────────────
