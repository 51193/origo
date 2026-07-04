using System.Collections.Generic;
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

    [Fact]
    public void CrossSession_EntityReadsPeerInAnotherSession()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new FrameCounterStrategy())
            .Build();

        var bgSession = harness.CreateBackgroundSession("bg_peer", "peer_level");
        var bgEntity = new SndMetaData
        {
            Name = "peer",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData { LifecycleIndices = ["test.frame_counter"] },
            DataMetaData = new DataMetaData()
        };
        var peer = bgSession.Spawn(bgEntity);
        peer.SetData("peer_value", 999);
        harness.RunFrames(2);

        var fgEntity = harness.SpawnEntity("seeker", ["test.frame_counter"]);
        harness.DriveFrame();

        var foundSession = harness.TryGetSession("bg_peer");
        Assert.NotNull(foundSession);

        var foundPeer = foundSession.FindByName("peer");
        Assert.NotNull(foundPeer);

        var (found, value) = foundPeer.TryGetData<int>("peer_value");
        Assert.True(found);
        Assert.Equal(999, value);

        var pCount = foundPeer.GetData<int>("count");
        Assert.Equal(3, pCount);
    }

    [Fact]
    public void BackgroundSession_SaveLoad_IndependentEntityState()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new FrameCounterStrategy())
            .Build();

        var fgEntity = harness.SpawnEntity("fg_player", ["test.frame_counter"]);
        harness.RunFrames(3);

        var bgSession = harness.CreateBackgroundSession("bg_independent", "indep_level");
        var bgMeta = new SndMetaData
        {
            Name = "bg_npc",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData { LifecycleIndices = ["test.frame_counter"] },
            DataMetaData = new DataMetaData()
        };
        var bgNpc = bgSession.Spawn(bgMeta);
        bgNpc.SetData("count", 5);
        bgSession.SessionBlackboard.SetValue("bg_flag", "alive");

        var saveId = harness.SaveAndReload("cross_session_save");

        var restoredFg = harness.Context.Runtime.SessionManager.TryGet("game");
        Assert.NotNull(restoredFg);

        var fgPlayer = restoredFg.FindByName("fg_player");
        Assert.NotNull(fgPlayer);
        Assert.Equal(3, fgPlayer.GetData<int>("count"));

        var restoredBg = harness.Context.Runtime.SessionManager.TryGet("bg_independent");
        Assert.NotNull(restoredBg);

        var (foundFlag, flagValue) = restoredBg.SessionBlackboard.TryGet<string>("bg_flag");
        Assert.True(foundFlag);
        Assert.Equal("alive", flagValue);

        var bgNpcRestored = restoredBg.FindByName("bg_npc");
        Assert.NotNull(bgNpcRestored);
        Assert.Equal(5, bgNpcRestored.GetData<int>("count"));
    }

    [Fact]
    public void BackgroundSession_KillEntities_DuringForegroundPlay()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new KillableTestStrategy())
            .WithStrategy(() => new FrameCounterStrategy())
            .Build();

        harness.SpawnEntity("fg_player", ["test.frame_counter"]);

        var bgSession = harness.CreateBackgroundSession("bg_victims", "victim_level");
        var bgMeta = new SndMetaData
        {
            Name = "bg_target",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData { LifecycleIndices = ["test.killable"] },
            DataMetaData = new DataMetaData()
        };
        bgSession.Spawn(bgMeta);

        var events = new List<string>();
        KillableTestStrategy.Events = events;
        try
        {
            bgSession.RequestKillEntity("bg_target");
            harness.DriveFrame();

            Assert.Contains(events, e => e == "before_dead");
            Assert.Null(bgSession.FindByName("bg_target"));

            var fgPlayer = harness.FindEntity("fg_player");
            Assert.NotNull(fgPlayer);
        }
        finally
        {
            KillableTestStrategy.Events = null;
        }
    }

    [Fact]
    public void MultipleBackgroundSessions_SaveLoadCycle()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new FrameCounterStrategy())
            .Build();

        var bg1 = harness.CreateBackgroundSession("bg_a", "level_a");
        var bg2 = harness.CreateBackgroundSession("bg_b", "level_b");

        var e1 = new SndMetaData
        {
            Name = "npc_a",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData { LifecycleIndices = ["test.frame_counter"] },
            DataMetaData = new DataMetaData()
        };
        bg1.Spawn(e1);

        var e2 = new SndMetaData
        {
            Name = "npc_b",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData { LifecycleIndices = ["test.frame_counter"] },
            DataMetaData = new DataMetaData()
        };
        bg2.Spawn(e2);

        harness.DriveFrame();

        bg1.SessionBlackboard.SetValue("a_key", 1);
        bg2.SessionBlackboard.SetValue("b_key", 2);

        harness.SaveAndReload("multi_bg_save");

        var r1 = harness.Context.Runtime.SessionManager.TryGet("bg_a");
        var r2 = harness.Context.Runtime.SessionManager.TryGet("bg_b");
        var rFg = harness.Context.Runtime.SessionManager.TryGet("game");

        Assert.NotNull(r1);
        Assert.NotNull(r2);
        Assert.NotNull(rFg);

        var (fa, va) = r1.SessionBlackboard.TryGet<int>("a_key");
        Assert.True(fa);
        Assert.Equal(1, va);

        var (fb, vb) = r2.SessionBlackboard.TryGet<int>("b_key");
        Assert.True(fb);
        Assert.Equal(2, vb);

        var npcA = r1.FindByName("npc_a");
        var npcB = r2.FindByName("npc_b");
        Assert.NotNull(npcA);
        Assert.NotNull(npcB);
        Assert.Equal(1, npcA.GetData<int>("count"));
        Assert.Equal(1, npcB.GetData<int>("count"));
    }

    [StrategyIndex("test.bb_marker")]
    private sealed class BlackboardMarkerStrategy : SharedNoopLifecycleStrategy { }

    [StrategyIndex("test.killable")]
    private sealed class KillableTestStrategy : SharedKillProbeStrategy { }

    [StrategyIndex("test.frame_counter")]
    private sealed class FrameCounterStrategy : SharedFrameCounterStrategy { }
}
