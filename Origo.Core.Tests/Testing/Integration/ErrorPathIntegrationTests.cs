using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class ErrorPathIntegrationTests
{
    [Fact]
    public void DeferredAction_ExecutesAndFlushesThroughDriveFrame()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new DeferredCounterStrategy())
            .Build();

        harness.SpawnEntity("deferred_counter", ["test.int.err.deferred_counter"]);
        harness.RunFrames(3);

        var count = harness.GetEntityData<int>("deferred_counter", "count");
        Assert.Equal(3, count);

        var (found, deferredRan) = harness.TryGetEntityData<bool>("deferred_counter", "deferred_ran");
        Assert.True(found);
        Assert.True(deferredRan);
    }

    [Fact]
    public void ErrorPath_LoadSaveWithCorruptedSndScene_Throws()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new ErrorPathFrameCounterStrategy())
            .Build();

        harness.SpawnEntity("player", ["test.int.err.frame_counter"]);
        harness.RunFrames(3);

        var saveId = harness.Context.Save.RequestSaveGameAuto("corrupt_scene");
        harness.Context.Deferred.FlushDeferredActionsForCurrentFrame();

        harness.FileSystem.SeedFile(
            $"root/save_{saveId}/level_game_level/snd_scene.json", "{corrupted");

        foreach (var key in harness.Context.Runtime.SessionManager.Keys)
            harness.Context.Runtime.SessionManager.DestroySession(key);
        harness.Context.SetProgressRun(null);

        harness.Context.Save.RequestLoadGame(saveId);
        Assert.ThrowsAny<Exception>(
            () => harness.Context.Deferred.FlushDeferredActionsForCurrentFrame());
    }

    [Fact]
    public void ErrorPath_LoadNonexistentSave_Throws()
    {
        var harness = GameplaySimulationHarness.Create()
            .Build();

        harness.Context.Save.RequestLoadGame("nonexistent_save_id");
        var ex = Assert.ThrowsAny<Exception>(
            () => harness.Context.Deferred.FlushDeferredActionsForCurrentFrame());
        Assert.Contains("nonexistent", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ErrorPath_LoadSaveWithCorruptedSessionFile_Throws()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new ErrorPathFrameCounterStrategy())
            .Build();

        harness.SpawnEntity("player", ["test.int.err.frame_counter"]);
        harness.RunFrames(3);

        var saveId = harness.Context.Save.RequestSaveGameAuto("corrupt_session");
        harness.Context.Deferred.FlushDeferredActionsForCurrentFrame();

        harness.FileSystem.SeedFile(
            $"root/save_{saveId}/level_game_level/session.json", "{corrupted");

        foreach (var key in harness.Context.Runtime.SessionManager.Keys)
            harness.Context.Runtime.SessionManager.DestroySession(key);
        harness.Context.SetProgressRun(null);

        harness.Context.Save.RequestLoadGame(saveId);
        Assert.ThrowsAny<Exception>(
            () => harness.Context.Deferred.FlushDeferredActionsForCurrentFrame());
    }

    // ── test strategies ──────────────────────────────────────────────

    [StrategyIndex("test.int.err.frame_counter")]
    private sealed class ErrorPathFrameCounterStrategy : SharedFrameCounterStrategy { }

    [StrategyIndex("test.int.err.deferred_counter")]
    private sealed class DeferredCounterStrategy : LifecycleStrategyBase
    {
        public override void AfterSpawn(ISndEntity entity, ISndContext ctx) => entity.SetData("count", 0);

        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            var count = entity.GetData<int>("count");
            entity.SetData("count", count + 1);

            ctx.Deferred.EnqueueBusinessDeferred(() =>
            {
                if (count > 0)
                    entity.SetData("deferred_ran", true);
            });
        }
    }
}
