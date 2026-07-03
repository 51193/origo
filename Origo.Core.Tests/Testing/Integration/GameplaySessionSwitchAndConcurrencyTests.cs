using System.Collections.Generic;
using System.Threading;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class GameplaySessionSwitchAndConcurrencyTests
{
    [Fact]
    public void SwitchSession_BackgroundSessionBlackboard_Isolated()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new BlackboardMarkerStrategy())
            .Build();

        harness.SessionBlackboard.SetValue("foreground_key", "fg_value");

        var bgSession = harness.CreateBackgroundSession("bg1", "bg_level");
        bgSession.SessionBlackboard.SetValue("background_key", "bg_value");

        harness.SpawnEntity("marker", ["test.bb_marker"]);

        harness.DriveFrame();

        var (fgFound, fgValue) = harness.SessionBlackboard.TryGet<string>("foreground_key");
        Assert.True(fgFound);
        Assert.Equal("fg_value", fgValue);

        var (bgFound, bgValue) = bgSession.SessionBlackboard.TryGet<string>("background_key");
        Assert.True(bgFound);
        Assert.Equal("bg_value", bgValue);

        var (fgHasBg, _) = harness.SessionBlackboard.TryGet<string>("background_key");
        Assert.False(fgHasBg);
    }

    [Fact]
    public void ConcurrentSpawnKill_SameFrame_AllCleanedUp()
    {
        var events = new List<string>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new KillableTestStrategy())
            .Build();

        KillableTestStrategy.Events = events;
        try
        {
            var entity1 = harness.SpawnEntity("target_a", ["test.killable"]);
            var entity2 = harness.SpawnEntity("target_b", ["test.killable"]);

            harness.RequestKillEntity("target_a");
            harness.RequestKillEntity("target_b");

            harness.DriveFrame();

            Assert.Null(harness.FindEntity("target_a"));
            Assert.Null(harness.FindEntity("target_b"));
            Assert.Equal(2, events.FindAll(e => e == "before_dead").Count);
        }
        finally
        {
            KillableTestStrategy.Events = null;
        }
    }

    [Fact]
    public void KillEntity_ThenRespawn_NewEntityIndependent()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new FrameCounterStrategy())
            .Build();

        var first = harness.SpawnEntity("player", ["test.frame_counter"]);
        first.SetData("hp", 100);
        harness.RunFrames(3);

        harness.RequestKillEntity("player");
        harness.DriveFrame();
        Assert.Null(harness.FindEntity("player"));

        var second = harness.SpawnEntity("player", ["test.frame_counter"]);
        harness.RunFrames(2);

        var count = harness.GetEntityData<int>("player", "count");
        Assert.Equal(2, count);

        var (found, _) = second.TryGetData<int>("hp");
        Assert.False(found);
    }

    [Fact]
    public void MultipleBackgroundSessions_EntitiesProcessedInParallel()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new FrameCounterStrategy())
            .Build();

        var bg1 = harness.CreateBackgroundSession("bg1", "bg_level_1");
        var bg2 = harness.CreateBackgroundSession("bg2", "bg_level_2");

        var entity1 = harness.SpawnEntity("player", ["test.frame_counter"]);
        entity1.SetData("count", 0);

        var s1e1 = new SndMetaData
        {
            Name = "npc1",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData { LifecycleIndices = ["test.frame_counter"] },
            DataMetaData = new DataMetaData()
        };
        var bg1Entity = bg1.Spawn(s1e1);
        bg1Entity.SetData("count", 0);

        var s2e1 = new SndMetaData
        {
            Name = "npc2",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData { LifecycleIndices = ["test.frame_counter"] },
            DataMetaData = new DataMetaData()
        };
        var bg2Entity = bg2.Spawn(s2e1);
        bg2Entity.SetData("count", 0);

        harness.DriveFrame();

        var fgCount = harness.GetEntityData<int>("player", "count");
        var bg1Count = bg1.FindByName("npc1")!.GetData<int>("count");
        var bg2Count = bg2.FindByName("npc2")!.GetData<int>("count");

        Assert.Equal(1, fgCount);
        Assert.Equal(1, bg1Count);
        Assert.Equal(1, bg2Count);
    }

    [StrategyIndex("test.bb_marker")]
    private sealed class BlackboardMarkerStrategy : LifecycleStrategyBase
    {
    }

    [StrategyIndex("test.killable")]
    private sealed class KillableTestStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _events = new();
        public static List<string>? Events { get => _events.Value; set => _events.Value = value; }

        public override void BeforeDead(ISndEntity entity, ISndContext ctx) =>
            Events?.Add("before_dead");
    }

    [StrategyIndex("test.frame_counter")]
    private sealed class FrameCounterStrategy : LifecycleStrategyBase
    {
        public override void AfterSpawn(ISndEntity entity, ISndContext ctx) =>
            entity.SetData("count", 0);

        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            var count = entity.GetData<int>("count");
            entity.SetData("count", count + 1);
        }
    }
}
