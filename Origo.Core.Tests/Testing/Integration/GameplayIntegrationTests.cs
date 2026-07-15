using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class GameplayIntegrationTests
{
    [Fact]
    public void MultiFrameProcessing_AccumulatesData()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new FrameCounterStrategy())
            .Build();

        var entity = harness.SpawnEntity("counter", ["test.frame_counter"]);

        harness.RunFrames(10);

        var count = harness.GetEntityData<int>("counter", "count");
        Assert.Equal(10, count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(100)]
    public void MultiFrameProcessing_VariousFrameCounts_AccumulatesCorrectly(int frameCount)
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new FrameCounterStrategy())
            .Build();

        harness.SpawnEntity("counter", ["test.frame_counter"]);

        harness.RunFrames(frameCount);

        var count = harness.GetEntityData<int>("counter", "count");
        Assert.Equal(frameCount, count);
    }

    [Fact]
    public void EntityInteraction_FindByName_ReadsPeerData()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new PeerLookupStrategy())
            .Build();

        var peer = harness.SpawnEntity("peer", []);
        peer.SetData("peer_value", 42);

        var lookupEntity = harness.SpawnEntity("lookup", ["test.peer_lookup"]);

        harness.DriveFrame();

        var result = harness.GetEntityData<int>("lookup", "peer_result");
        Assert.Equal(42, result);
    }

    [Fact]
    public void EntityInteraction_ViaBlackboard_TransfersDataBetweenFrames()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new BbWriterStrategy())
            .WithStrategy(() => new BbReaderStrategy())
            .Build();

        harness.SpawnEntity("writer", ["test.bb_writer"]);
        harness.SpawnEntity("reader", ["test.bb_reader"]);

        harness.DriveFrame();

        var (found, value) = harness.TryGetEntityData<string>("reader", "read_value");
        Assert.True(found);
        Assert.Equal("from_writer", value);
    }

    [Fact]
    public void DeferredAction_ExecutesAfterFlush()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new DeferredProbeStrategy())
            .Build();

        var entity = harness.SpawnEntity("deferred_probe", ["test.deferred_probe"]);

        harness.DriveFrame();

        var (found, ran) = harness.TryGetEntityData<bool>("deferred_probe", "deferred_ran");
        Assert.True(found);
        Assert.True(ran);
    }

    [Fact]
    public void SaveDuringGameplay_PersistsToDisk()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new FrameCounterStrategy())
            .Build();

        var entity = harness.SpawnEntity("player", ["test.frame_counter"]);
        entity.SetData("hp", 80);
        harness.RunFrames(5);

        var saveId = harness.Context.Save.RequestSaveGameAuto("gameplay_save");
        harness.Context.Deferred.FlushDeferredActionsForCurrentFrame();

        Assert.True(harness.FileSystem.Exists($"root/save_{saveId}/progress.json"));
        Assert.True(harness.FileSystem.Exists($"root/save_{saveId}/level_game_level/snd_scene.json"));

        var count = harness.GetEntityData<int>("player", "count");
        Assert.Equal(5, count);

        var hp = harness.GetEntityData<int>("player", "hp");
        Assert.Equal(80, hp);
    }

    [Fact]
    public void EntityKill_BeforeDeadAndRemoval()
    {
        var events = new List<string>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new KillProbeIntegrationStrategy())
            .Build();

        KillProbeIntegrationStrategy.Events = events;
        try
        {
            var entity = harness.SpawnEntity("victim", ["test.kill_probe_int"]);

            harness.GameSession.RequestKillEntity("victim");
            Assert.True(entity.IsPendingKill);

            harness.DriveFrame();

            Assert.Null(harness.FindEntity("victim"));
            Assert.Contains("before_dead", events);
        }
        finally
        {
            KillProbeIntegrationStrategy.Events = null;
        }
    }

    [Fact]
    public void ConsoleCommand_DuringFrame()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new ConsoleCommandStrategy())
            .Build();

        harness.SpawnEntity("logger", ["test.console_cmd"]);

        harness.DriveFrame();

        Assert.Contains(harness.ConsoleOutput, s => s.Contains("Snd count:"));
    }

    [Fact]
    public void FullGameLoopRoundTrip_SaveDisposeReload()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new FrameCounterStrategy())
            .Build();

        var entity = harness.SpawnEntity("player", ["test.frame_counter"]);
        entity.SetData("hp", 100);
        harness.RunFrames(3);
        harness.SessionBlackboard.SetValue("game_flag", "surviving");

        var saveId = harness.SaveAndReload("roundtrip_test");

        Assert.True(harness.FileSystem.Exists($"root/save_{saveId}/progress.json"));

        var gameSession = harness.Context.Runtime.SessionManager.TryGet("game");
        Assert.NotNull(gameSession);

        var (foundFlag, flagValue) = gameSession.SessionBlackboard
            .TryGet<string>("game_flag");
        Assert.True(foundFlag);
        Assert.Equal("surviving", flagValue);
    }

    [Fact]
    public void ObserverStrategy_MountAndNotify()
    {
        var events = new List<TestObserverEvent>();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new HpObserverIntegrationStrategy())
            .Build();

        EventCollector.Events = events;
        try
        {
            var target = harness.SpawnEntity("target", []);
            var observer = harness.SpawnEntity("observer", []);

            observer.MountObserverStrategy(target, "test.hp_observer_int");

            harness.SetEntityData("target", "hp", 50);

            Assert.Contains(events, e => e.EventType == "on_data_changed"
                                          && e.DataKey == "hp"
                                          && e.NewValue != null && e.NewValue.Value.AsInt32() == 50);
        }
        finally
        {
            EventCollector.Events = null;
        }
    }

    [StrategyIndex(TestStrategyIndices.FrameCounter)]
    private sealed class FrameCounterStrategy : SharedFrameCounterStrategy { }

    [StrategyIndex("test.peer_lookup")]
    private sealed class PeerLookupStrategy : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            var peer = entity.OwningSession.FindByName("peer");
            if (peer is not null)
            {
                var (found, value) = peer.TryGetData<int>("peer_value");
                if (found)
                    entity.SetData("peer_result", value);
            }
        }
    }

    [StrategyIndex("test.bb_writer")]
    private sealed class BbWriterStrategy : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx) =>
            entity.OwningSession.SessionBlackboard.SetValue("bridge_value", "from_writer");
    }

    [StrategyIndex(TestStrategyIndices.BlackboardReader)]
    private sealed class BbReaderStrategy : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            var (found, value) = entity.OwningSession.SessionBlackboard.TryGet<string>("bridge_value");
            if (found)
                entity.SetData("read_value", value);
        }
    }

    [StrategyIndex("test.deferred_probe")]
    private sealed class DeferredProbeStrategy : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx) =>
            ctx.Deferred.EnqueueBusinessDeferred(() => entity.SetData("deferred_ran", true));
    }

    [StrategyIndex("test.kill_probe_int")]
    private sealed class KillProbeIntegrationStrategy : SharedKillProbeStrategy { }

    [StrategyIndex("test.console_cmd")]
    private sealed class ConsoleCommandStrategy : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx) =>
            ctx.ConsoleAccess.TrySubmitConsoleCommand("snd_count");
    }

    [StrategyIndex("test.hp_observer_int")]
    [ObserveData("hp")]
    private sealed class HpObserverIntegrationStrategy : SharedDataChangeObserverStrategy { }
}
