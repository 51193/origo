using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Snd;
using Origo.Core.Save;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Tests that verify save-then-dispose round-trip, background session
///     switching, and BeforeQuit strategy edge cases.
/// </summary>
[Collection("StrategyStateTests")]
public class DisposeSemanticsTestsRoundTrip
{
    [Fact]
    public void ExplicitSave_ThenDispose_ThenContinue_LoadsSavedState()
    {
        var (ctx, fs) = DisposeSemanticsTestInfrastructure.CreateForegroundContext();

        var fg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);

        fg.Spawn(DisposeSemanticsTestInfrastructure.CreateMeta("SavedEntity"));
        fg.SessionBlackboard.SetValue("save_key", "save_value");

        ctx.Save.RequestSaveGame("test_001");
        ctx.FlushFrame();

        Assert.True(fs.Exists("root/save_test_001/progress.json"));
        Assert.True(fs.Exists("root/save_test_001/level_test_level/snd_scene.json"));

        ctx.EnsureProgressRun().Dispose();
        ctx.SetProgressRun(null);

        ctx.Save.RequestLoadGame("test_001");
        ctx.FlushFrame();

        var restoredFg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(restoredFg);

        var entities = restoredFg.GetEntities();
        Assert.Single(entities);
        Assert.Equal("SavedEntity", entities.First().Name);

        var (found, val) = restoredFg.SessionBlackboard.TryGet<string>("save_key");
        Assert.True(found);
        Assert.Equal("save_value", val);
    }

    [Fact]
    public void Save_ThenDispose_ThenContinue_ProgressBlackboardPreserved()
    {
        var (ctx, fs) = DisposeSemanticsTestInfrastructure.CreateForegroundContext();
        ctx.Blackboard.ProgressBlackboard!.SetValue("global_score", 9001);

        ctx.Save.RequestSaveGame("score_save");
        ctx.FlushFrame();

        ctx.EnsureProgressRun().Dispose();
        ctx.SetProgressRun(null);

        ctx.Save.RequestLoadGame("score_save");
        ctx.FlushFrame();

        var (found, score) = ctx.Blackboard.ProgressBlackboard!.TryGet<int>("global_score");
        Assert.True(found);
        Assert.Equal(9001, score);
    }

    [Fact]
    public void SaveAfterSwitch_HasCorrectActiveLevel()
    {
        var (ctx, fs) = DisposeSemanticsTestInfrastructure.CreateForegroundContext();

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("gen", "game", true);
        bg.Spawn(DisposeSemanticsTestInfrastructure.CreateMeta("GameEntity"));

        ctx.Save.RequestSaveGameAuto();
        ctx.FlushFrame();

        ctx.Runtime.SessionManager.DestroySession("gen");
        ctx.Save.RequestSwitchForegroundLevel("game");
        ctx.FlushFrame();

        var fg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("game", fg.LevelId);

        var saveId = "after_switch";
        ctx.Save.RequestSaveGame(saveId);
        ctx.FlushFrame();

        var payload = ctx.StorageService.ReadSavePayloadFromSnapshot(saveId, "game");
        Assert.Equal("game", payload.ActiveLevelId);
        Assert.True(payload.Levels.ContainsKey("game"));
    }

    [Fact]
    public void SaveSwitchDisposeReload_RestoresToSavedState()
    {
        var (ctx, fs) = DisposeSemanticsTestInfrastructure.CreateForegroundContext();

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("gen", "game", true);
        bg.Spawn(DisposeSemanticsTestInfrastructure.CreateMeta("GameEntity1"));
        bg.Spawn(DisposeSemanticsTestInfrastructure.CreateMeta("GameEntity2"));
        bg.SessionBlackboard.SetValue("map_seed", 42);

        ctx.Save.RequestSaveGameAuto();
        ctx.FlushFrame();

        ctx.Runtime.SessionManager.DestroySession("gen");
        ctx.Save.RequestSwitchForegroundLevel("game");
        ctx.FlushFrame();

        var fgBefore = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fgBefore);
        Assert.Equal("game", fgBefore.LevelId);

        var saveId = "reload_test";
        ctx.Save.RequestSaveGame(saveId);
        ctx.FlushFrame();

        ctx.EnsureProgressRun().Dispose();
        ctx.SetProgressRun(null);

        ctx.Save.RequestLoadGame(saveId);
        ctx.FlushFrame();

        var restoredFg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(restoredFg);
        Assert.Equal("game", restoredFg.LevelId);
        Assert.Equal(2, restoredFg.GetEntities().Count);

        var (foundSeed, seed) = restoredFg.SessionBlackboard.TryGet<int>("map_seed");
        Assert.True(foundSeed);
        Assert.Equal(42, seed);
    }

    [Fact]
    public void FullRoundTrip_SwitchForeground_OldLevelDataPersistedImplicitly()
    {
        var (ctx, fs) = DisposeSemanticsTestInfrastructure.CreateForegroundContext();

        var oldFg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(oldFg);
        oldFg.SessionBlackboard.SetValue("old_data", "old_value");
        oldFg.Spawn(DisposeSemanticsTestInfrastructure.CreateMeta("OldEntity"));

        fs.SeedFile("root/current/level_game/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_game/session.json", "{}");
        fs.SeedFile("root/current/level_game/session_state_machines.json",
            "{\"machines\":[]}");

        ctx.Save.RequestSwitchForegroundLevel("game");
        ctx.FlushFrame();

        Assert.True(fs.Exists("root/current/level_test_level/snd_scene.json"));
        Assert.True(fs.Exists("root/current/level_test_level/session.json"));
        Assert.True(fs.Exists("root/current/level_test_level/session_state_machines.json"));
    }

    [Fact]
    public void SessionRun_Dispose_BeforeQuit_CanAccessSceneHost()
    {
        var events = new List<string>();
        var (ctx, _) = DisposeSemanticsTestInfrastructure.CreateForegroundContext(world =>
        {
            DisposeSemanticsTestInfrastructure.SessionAccessQuitStrategy.Bind(events);
            world.RegisterStrategy(() => new DisposeSemanticsTestInfrastructure.SessionAccessQuitStrategy());
        });

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Spawn(DisposeSemanticsTestInfrastructure.CreateMetaWithIndex("Entity",
            DisposeSemanticsTestInfrastructure.SessionAccessStrategyIndex));

        events.Clear();
        var ex = Record.Exception(() => bg.Dispose());

        Assert.Null(ex);
        Assert.Contains("SceneHostAccess:OK", events);
        Assert.Contains("BlackboardAccess:OK", events);
    }

    [Fact]
    public void SessionRun_Dispose_BeforeQuitThrows_EntitiesStillRemoved()
    {
        var (ctx, _) = DisposeSemanticsTestInfrastructure.CreateForegroundContext(world =>
        {
            world.RegisterStrategy(() => new DisposeSemanticsTestInfrastructure.ThrowingQuitStrategy());
        });

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Spawn(DisposeSemanticsTestInfrastructure.CreateMetaWithIndex("Entity",
            DisposeSemanticsTestInfrastructure.ThrowingQuitStrategyIndex));

        var ex = Record.Exception(() => bg.Dispose());

        Assert.NotNull(ex);
        Assert.Throws<ObjectDisposedException>(() => bg.FindByName("any"));
    }

    [Fact]
    public void SessionRun_Dispose_BeforeQuitThrows_DoubleDisposeStillIdempotent()
    {
        var (ctx, _) = DisposeSemanticsTestInfrastructure.CreateForegroundContext(world =>
        {
            world.RegisterStrategy(() => new DisposeSemanticsTestInfrastructure.ThrowingQuitStrategy());
        });

        var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Spawn(DisposeSemanticsTestInfrastructure.CreateMetaWithIndex("Entity",
            DisposeSemanticsTestInfrastructure.ThrowingQuitStrategyIndex));

        Record.Exception(() => bg.Dispose());

        var ex2 = Record.Exception(() => bg.Dispose());
        Assert.Null(ex2);
    }
}
