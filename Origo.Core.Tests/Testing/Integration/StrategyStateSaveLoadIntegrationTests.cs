using System;
using System.Collections.Generic;
using System.Threading;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class StrategyStateSaveLoadIntegrationTests
{
    [Fact]
    public void LifecycleStrategy_StateSurvivesSaveLoad()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new StateFrameCounterStrategy())
            .Build();

        var entity = harness.SpawnEntity("player", ["test.int.state.frame_counter"]);
        entity.SetData("hp", 100);
        harness.RunFrames(5);

        Assert.Equal(5, harness.GetEntityData<int>("player", "count"));

        var saveId = harness.Context.Save.RequestSaveGameAuto("state_lifecycle_save");
        harness.Context.Deferred.FlushDeferredActionsForCurrentFrame();

        foreach (var key in harness.Context.Runtime.SessionManager.Keys)
            harness.Context.Runtime.SessionManager.DestroySession(key);
        harness.Context.SetProgressRun(null);

        harness.Context.Save.RequestLoadGame(saveId);
        harness.Context.Deferred.FlushDeferredActionsForCurrentFrame();

        var gameSession = harness.Context.Runtime.SessionManager.TryGet("game");
        Assert.NotNull(gameSession);

        var loaded = gameSession.FindByName("player");
        Assert.NotNull(loaded);
        Assert.Equal(5, loaded.GetData<int>("count"));
        Assert.Equal(100, loaded.GetData<int>("hp"));

        harness.DriveFrame();
        Assert.Equal(6, loaded.GetData<int>("count"));
    }

    [Fact]
    public void EntityDataAndBlackboard_BothSurviveSaveLoad()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new StateFrameCounterStrategy())
            .Build();

        harness.SpawnEntity("player", ["test.int.state.frame_counter"]);
        harness.SessionBlackboard.SetValue("score", 999);
        harness.SessionBlackboard.SetValue("level_name", "dungeon_1");
        harness.RunFrames(2);

        var saveId = harness.Context.Save.RequestSaveGameAuto("state_data_save");
        harness.Context.Deferred.FlushDeferredActionsForCurrentFrame();

        foreach (var key in harness.Context.Runtime.SessionManager.Keys)
            harness.Context.Runtime.SessionManager.DestroySession(key);
        harness.Context.SetProgressRun(null);

        harness.Context.Save.RequestLoadGame(saveId);
        harness.Context.Deferred.FlushDeferredActionsForCurrentFrame();

        var gameSession = harness.Context.Runtime.SessionManager.TryGet("game");
        Assert.NotNull(gameSession);

        var (foundScore, score) = gameSession.SessionBlackboard.TryGet<int>("score");
        Assert.True(foundScore);
        Assert.Equal(999, score);

        var (foundLevel, level) = gameSession.SessionBlackboard.TryGet<string>("level_name");
        Assert.True(foundLevel);
        Assert.Equal("dungeon_1", level);

        var player = gameSession.FindByName("player");
        Assert.NotNull(player);
        Assert.Equal(2, player.GetData<int>("count"));
    }

    [Fact]
    public void SaveLoad_ThenContinue_EntityStillProcesses()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new StateFrameCounterStrategy())
            .Build();

        harness.SpawnEntity("player", ["test.int.state.frame_counter"]);
        harness.RunFrames(3);

        var saveId = harness.Context.Save.RequestSaveGameAuto("state_continue_save");
        harness.Context.Deferred.FlushDeferredActionsForCurrentFrame();

        foreach (var key in harness.Context.Runtime.SessionManager.Keys)
            harness.Context.Runtime.SessionManager.DestroySession(key);
        harness.Context.SetProgressRun(null);

        harness.Context.Save.RequestLoadGame(saveId);
        harness.Context.Deferred.FlushDeferredActionsForCurrentFrame();

        var gameSession = harness.Context.Runtime.SessionManager.TryGet("game");
        Assert.NotNull(gameSession);

        harness.RunFrames(4);

        var player = gameSession.FindByName("player");
        Assert.NotNull(player);
        Assert.Equal(7, player.GetData<int>("count"));
    }

    [Fact]
    public void SaveLoad_NoLossOfEntities()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new StateFrameCounterStrategy())
            .Build();

        for (var i = 0; i < 20; i++)
        {
            var entity = harness.SpawnEntity($"unit_{i}", ["test.int.state.frame_counter"]);
            entity.SetData("id", i);
        }

        harness.RunFrames(1);

        var saveId = harness.Context.Save.RequestSaveGameAuto("state_many_save");
        harness.Context.Deferred.FlushDeferredActionsForCurrentFrame();

        foreach (var key in harness.Context.Runtime.SessionManager.Keys)
            harness.Context.Runtime.SessionManager.DestroySession(key);
        harness.Context.SetProgressRun(null);

        harness.Context.Save.RequestLoadGame(saveId);
        harness.Context.Deferred.FlushDeferredActionsForCurrentFrame();

        var gameSession = harness.Context.Runtime.SessionManager.TryGet("game");
        Assert.NotNull(gameSession);

        var entities = gameSession.GetEntities();
        Assert.Equal(20, entities.Count);

        foreach (var e in entities)
        {
            Assert.Equal(1, e.GetData<int>("count"));
            var id = e.GetData<int>("id");
            Assert.InRange(id, 0, 19);
        }
    }

    [Fact]
    public void SaveTwice_SecondOverwrites_StateCorrect()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new StateFrameCounterStrategy())
            .Build();

        harness.SpawnEntity("player", ["test.int.state.frame_counter"]);
        harness.SessionBlackboard.SetValue("version", 1);
        harness.RunFrames(2);

        var saveId1 = harness.Context.Save.RequestSaveGameAuto("state_overwrite");
        harness.Context.Deferred.FlushDeferredActionsForCurrentFrame();

        harness.RunFrames(3);
        harness.SessionBlackboard.SetValue("version", 2);

        var saveId2 = harness.Context.Save.RequestSaveGameAuto("state_overwrite");
        harness.Context.Deferred.FlushDeferredActionsForCurrentFrame();

        foreach (var key in harness.Context.Runtime.SessionManager.Keys)
            harness.Context.Runtime.SessionManager.DestroySession(key);
        harness.Context.SetProgressRun(null);

        harness.Context.Save.RequestLoadGame(saveId2);
        harness.Context.Deferred.FlushDeferredActionsForCurrentFrame();

        var gameSession = harness.Context.Runtime.SessionManager.TryGet("game");
        Assert.NotNull(gameSession);

        var player = gameSession.FindByName("player");
        Assert.NotNull(player);
        Assert.Equal(5, player.GetData<int>("count"));

        var (found, version) = gameSession.SessionBlackboard.TryGet<int>("version");
        Assert.True(found);
        Assert.Equal(2, version);
    }

    [Fact]
    public void SaveLoad_MultipleSessions_AllStatePreserved()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new StateFrameCounterStrategy())
            .Build();

        harness.SpawnEntity("fg_unit", ["test.int.state.frame_counter"]);
        harness.SessionBlackboard.SetValue("fg_key", "foreground");

        var bg = harness.CreateBackgroundSession("bg_state", "bg_state_level");
        var bgMeta = new SndMetaData
        {
            Name = "bg_unit",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData { LifecycleIndices = ["test.int.state.frame_counter"] },
            DataMetaData = new DataMetaData()
        };
        bg.Spawn(bgMeta);
        bg.SessionBlackboard.SetValue("bg_key", "background");

        harness.RunFrames(3);

        var saveId = harness.Context.Save.RequestSaveGameAuto("multi_session_state");
        harness.Context.Deferred.FlushDeferredActionsForCurrentFrame();

        foreach (var key in harness.Context.Runtime.SessionManager.Keys)
            harness.Context.Runtime.SessionManager.DestroySession(key);
        harness.Context.SetProgressRun(null);

        harness.Context.Save.RequestLoadGame(saveId);
        harness.Context.Deferred.FlushDeferredActionsForCurrentFrame();

        var fg = harness.Context.Runtime.SessionManager.TryGet("game");
        Assert.NotNull(fg);
        Assert.Equal(3, fg.FindByName("fg_unit")!.GetData<int>("count"));
        var (ff, fv) = fg.SessionBlackboard.TryGet<string>("fg_key");
        Assert.True(ff);
        Assert.Equal("foreground", fv);

        var restoredBg = harness.Context.Runtime.SessionManager.TryGet("bg_state");
        Assert.NotNull(restoredBg);
        Assert.Equal(3, restoredBg.FindByName("bg_unit")!.GetData<int>("count"));
        var (bf, bv) = restoredBg.SessionBlackboard.TryGet<string>("bg_key");
        Assert.True(bf);
        Assert.Equal("background", bv);
    }

    // ── test strategies ──────────────────────────────────────────────

    [StrategyIndex("test.int.state.frame_counter")]
    private sealed class StateFrameCounterStrategy : LifecycleStrategyBase
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
