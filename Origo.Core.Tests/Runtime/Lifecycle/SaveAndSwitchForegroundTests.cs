using Origo.Core.Runtime.Lifecycle;
using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.DataSource;
using Origo.Core.Save;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Xunit;
using Origo.Core.Abstractions.Lifecycle;
using static Origo.Core.Tests.SaveAndSwitchForegroundTestInfrastructure;

namespace Origo.Core.Tests;

/// <summary>
///     Tests for topology persistence integrity, edge cases, and
///     background session collision handling during foreground switch.
/// </summary>
[Collection("StrategyStateTests")]
public class SaveAndSwitchForegroundTests
{
    // ── Topology correctness ────────────────────────────────────────────

    [Fact]
    public void PersistProgress_WritesFullTopologyIncludingBackgroundSessions()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg1", "bg_level", true);

        ctx.Save.RequestSaveGame("topology_test");
        ctx.FlushFrame();

        Assert.True(fs.Exists("root/current/progress.json"));
        Assert.True(fs.Exists("root/current/progress_state_machines.json"));

        var (found, topology) = ctx.Blackboard.ProgressBlackboard!
            .TryGet<string>(WellKnownKeys.SessionTopology);
        Assert.True(found);
        Assert.Contains("bg1=bg_level=true", topology);
        Assert.Contains("__foreground__=test_level=false", topology);
    }

    [Fact]
    public void SwitchForeground_PreservesBackgroundSessionsInTopology()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg1", "bg_level", true);
        fs.SeedFile("root/current/level_other/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_other/session.json", "{}");
        fs.SeedFile("root/current/level_other/session_state_machines.json",
            "{\"machines\":[]}");

        var progressRun = ctx.EnsureProgressRun();
        ctx.Save.RequestSwitchForegroundLevel("other");
        ctx.FlushFrame();

        var (found, topology) = progressRun.ProgressBlackboard
            .TryGet<string>(WellKnownKeys.SessionTopology);
        Assert.True(found);
        Assert.Contains("__foreground__=other=false", topology);
        Assert.Contains("bg1=bg_level=true", topology);
    }

    [Fact]
    public void SwitchForeground_WithoutBackgroundSessions_TopologyIsForegroundOnly()
    {
        var (ctx, fs) = CreateForegroundContext();

        fs.SeedFile("root/current/level_other/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_other/session.json", "{}");
        fs.SeedFile("root/current/level_other/session_state_machines.json",
            "{\"machines\":[]}");

        var progressRun = ctx.EnsureProgressRun();
        ctx.Save.RequestSwitchForegroundLevel("other");
        ctx.FlushFrame();

        var (found, topology) = progressRun.ProgressBlackboard
            .TryGet<string>(WellKnownKeys.SessionTopology);
        Assert.True(found);
        Assert.Matches("^__foreground__=other=false$", topology);
    }

    [Fact]
    public void SwitchForeground_WithMultipleBackgroundSessions_PreservesAllInTopology()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg1 = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("sim1", "sim_level", true);
        using var bg2 = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("sim2", "other_sim");
        fs.SeedFile("root/current/level_new_fg/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_new_fg/session.json", "{}");
        fs.SeedFile("root/current/level_new_fg/session_state_machines.json",
            "{\"machines\":[]}");

        var progressRun = ctx.EnsureProgressRun();
        ctx.Save.RequestSwitchForegroundLevel("new_fg");
        ctx.FlushFrame();

        var (found, topology) = progressRun.ProgressBlackboard
            .TryGet<string>(WellKnownKeys.SessionTopology);
        Assert.True(found);
        Assert.Contains("__foreground__=new_fg=false", topology);
        Assert.Contains("sim1=sim_level=true", topology);
        Assert.Contains("sim2=other_sim=false", topology);
    }

    // ── Disk consistency ────────────────────────────────────────────────

    [Fact]
    public void SaveBackgroundSession_ThenSwitch_WritesAllLevelDataToCurrent()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "game", true);
        bg.Spawn(CreateMeta("DiskEntity"));

        ctx.Save.RequestSaveGameAuto();
        ctx.FlushFrame();

        Assert.True(fs.Exists("root/current/level_game/snd_scene.json"));
        Assert.True(fs.Exists("root/current/level_game/session.json"));
        Assert.True(fs.Exists("root/current/level_game/session_state_machines.json"));

        ctx.Runtime.SessionManager.DestroySession("bg");
        ctx.Save.RequestSwitchForegroundLevel("game");
        ctx.FlushFrame();

        var fg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.NotEmpty(fg.GetEntities());
    }

    [Fact]
    public void SaveBackgroundSession_ThenSwitch_ProgressJsonHasCorrectActiveLevel()
    {
        var (ctx, _) = CreateForegroundContext();

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "game", true);
        bg.Spawn(CreateMeta("Entity"));

        ctx.Save.RequestSaveGameAuto();
        ctx.FlushFrame();

        ctx.Runtime.SessionManager.DestroySession("bg");
        ctx.Save.RequestSwitchForegroundLevel("game");
        ctx.FlushFrame();

        var (found, topology) = ctx.EnsureProgressRun().ProgressBlackboard
            .TryGet<string>(WellKnownKeys.SessionTopology);
        Assert.True(found);

        var activeLevel = SessionTopologyCodec.ExtractForegroundLevelId(topology);
        Assert.Equal("game", activeLevel);
    }

    // ── Full round-trip: save → dispose → reload → verify ───────────────

    [Fact]
    public void SaveBackgroundSession_ThenSwitch_ThenReloadFromSnapshot_EntitiesPreserved()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "game", true);
        bg.Spawn(CreateMeta("Entity1"));
        bg.Spawn(CreateMeta("Entity2"));
        bg.SessionBlackboard.SetValue("round_key", "round_value");

        var saveId = ctx.Save.RequestSaveGameAuto();
        ctx.FlushFrame();

        ctx.Runtime.SessionManager.DestroySession("bg");
        ctx.Save.RequestSwitchForegroundLevel("game");
        ctx.FlushFrame();

        ctx.Save.RequestSaveGame(saveId);
        ctx.FlushFrame();

        ctx.EnsureProgressRun().Dispose();
        ctx.SetProgressRun(null);

        ctx.Save.RequestLoadGame(saveId);
        ctx.FlushFrame();

        var fg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("game", fg.LevelId);

        var entities = fg.GetEntities();
        Assert.Equal(2, entities.Count);

        var (found, val) = fg.SessionBlackboard.TryGet<string>("round_key");
        Assert.True(found);
        Assert.Equal("round_value", val);

        var restoredBg = ctx.Runtime.SessionManager.TryGet("bg");
        Assert.Null(restoredBg);
    }

    // ── Direct switch (no prior save) ───────────────────────────────────

    [Fact]
    public void SwitchForeground_ToBackgroundSessionLevel_ReloadsFromCurrent()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "game", true);
        bg.Spawn(CreateMeta("DirectEntity"));
        bg.SessionBlackboard.SetValue("direct_key", 77);

        ctx.Save.RequestSaveGameAuto();
        ctx.FlushFrame();

        fs.SeedFile("root/current/level_game/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_game/session.json", "{}");
        fs.SeedFile("root/current/level_game/session_state_machines.json",
            "{\"machines\":[]}");

        ctx.Runtime.SessionManager.DestroySession("bg");
        ctx.Save.RequestSwitchForegroundLevel("game");
        ctx.FlushFrame();

        var fg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("game", fg.LevelId);
    }

    [Fact]
    public void SwitchForeground_WithoutSave_WhenTargetMissing_EntersEmptySession()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "game", true);
        bg.Spawn(CreateMeta("DirectEntity"));

        var progressRun = ctx.EnsureProgressRun();
        ctx.Save.RequestSwitchForegroundLevel("missing_level");
        ctx.FlushFrame();

        var fg = (SessionRun?)progressRun.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("missing_level", fg.LevelId);
        Assert.Empty(fg.GetEntities());
    }

    // ── Deferred queue ordering ─────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RequestSaveGameAuto_ThenRequestSwitchForeground_EntitiesLoadRegardlessOfFlushOrder(
        bool flushBetween)
    {
        var (ctx, _) = CreateForegroundContext();

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "game", true);
        bg.Spawn(CreateMeta("QueueEntity"));
        bg.SessionBlackboard.SetValue("queue_val", 123);

        ctx.Save.RequestSaveGameAuto();
        ctx.FlushFrame();

        if (flushBetween)
            ctx.FlushFrame();

        ctx.Runtime.SessionManager.DestroySession("bg");
        ctx.Save.RequestSwitchForegroundLevel("game");
        ctx.FlushFrame();

        var fg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("game", fg.LevelId);

        var entities = fg.GetEntities();
        Assert.Single(entities);
        Assert.Equal("QueueEntity", entities.First().Name);

        var (found, val) = fg.SessionBlackboard.TryGet<int>("queue_val");
        Assert.True(found);
        Assert.Equal(123, val);
    }

    // ── Old foreground auto-persist on switch ───────────────────────────

    [Fact]
    public void SwitchForeground_AutoPersistsOldForegroundSessionToCurrent()
    {
        var (ctx, fs) = CreateForegroundContext();

        var fgBefore = (SessionRun?)ctx.EnsureProgressRun().SessionManager.ForegroundSession;
        Assert.NotNull(fgBefore);
        fgBefore.Spawn(CreateMeta("OldFgEntity"));
        fgBefore.SessionBlackboard.SetValue("old_key", "old_value");

        fs.SeedFile("root/current/level_new/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_new/session.json", "{}");
        fs.SeedFile("root/current/level_new/session_state_machines.json",
            "{\"machines\":[]}");

        ctx.Save.RequestSwitchForegroundLevel("new");
        ctx.FlushFrame();

        Assert.True(fs.Exists("root/current/level_test_level/snd_scene.json"));
        Assert.True(fs.Exists("root/current/level_test_level/session.json"));
        Assert.True(fs.Exists("root/current/level_test_level/session_state_machines.json"));
    }

    // ── Background session unchanged during switch ──────────────────────

    [Fact]
    public void SwitchForeground_BackgroundSessionEntitiesUntouched()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level", true);
        bg.Spawn(CreateMeta("BgSurvivor1"));
        bg.Spawn(CreateMeta("BgSurvivor2"));

        fs.SeedFile("root/current/level_new/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_new/session.json", "{}");
        fs.SeedFile("root/current/level_new/session_state_machines.json",
            "{\"machines\":[]}");

        ctx.Save.RequestSwitchForegroundLevel("new");
        ctx.FlushFrame();

        var bgAlive = ctx.Runtime.SessionManager.TryGet("bg");
        Assert.NotNull(bgAlive);
        Assert.Equal(2, bgAlive.GetEntities().Count);
        Assert.Contains(bgAlive.GetEntities(), e => e.Name == "BgSurvivor1");
        Assert.Contains(bgAlive.GetEntities(), e => e.Name == "BgSurvivor2");
    }

    [Fact]
    public void SwitchForeground_BackgroundSessionTickStatePreserved()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bgTick = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("ticker", "tick_level", true);
        using var bgNoTick = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("noticker", "no_tick");

        fs.SeedFile("root/current/level_new/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_new/session.json", "{}");
        fs.SeedFile("root/current/level_new/session_state_machines.json",
            "{\"machines\":[]}");

        ctx.Save.RequestSwitchForegroundLevel("new");
        ctx.FlushFrame();

        var (found, topology) = ctx.EnsureProgressRun().ProgressBlackboard
            .TryGet<string>(WellKnownKeys.SessionTopology);
        Assert.True(found);
        Assert.Contains("ticker=tick_level=true", topology);
        Assert.Contains("noticker=no_tick=false", topology);
    }

    // ── Deferred queue: SwitchForeground is now system deferred ─────────

    [Fact]
    public void RequestSwitchForegroundLevel_ExecutesInSystemDeferredQueue()
    {
        var (ctx, fs) = CreateForegroundContext();
        fs.SeedFile("root/current/level_after/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_after/session.json", "{}");
        fs.SeedFile("root/current/level_after/session_state_machines.json",
            "{\"machines\":[]}");

        ctx.Save.RequestSwitchForegroundLevel("after");

        var fgBeforeFlush = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fgBeforeFlush);
        Assert.Equal("test_level", fgBeforeFlush.LevelId);

        ctx.FlushFrame();

        var fgAfterFlush = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fgAfterFlush);
        Assert.Equal("after", fgAfterFlush.LevelId);
    }

    [Fact]
    public void RequestSwitchForegroundLevel_RunsAfterBusinessDeferred()
    {
        var (ctx, fs) = CreateForegroundContext();
        fs.SeedFile("root/current/level_after/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_after/session.json", "{}");
        fs.SeedFile("root/current/level_after/session_state_machines.json",
            "{\"machines\":[]}");

        var executionOrder = new List<string>();
        ctx.Deferred.EnqueueBusinessDeferred(() => executionOrder.Add("business"));
        ctx.Save.RequestSwitchForegroundLevel("after");
        ctx.FlushFrame();

        Assert.Equal("business", executionOrder[0]);
        Assert.Equal("after", ctx.Runtime.SessionManager.ForegroundSession!.LevelId);
    }

    // ── Entity count edge cases ─────────────────────────────────────────

    [Fact]
    public void SaveBackgroundSession_WithNoEntities_ThenSwitch_LoadsEmptyForeground()
    {
        var (ctx, _) = CreateForegroundContext();

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "empty_game", true);
        bg.SessionBlackboard.SetValue("note", "empty");

        ctx.Save.RequestSaveGameAuto();
        ctx.FlushFrame();

        ctx.Runtime.SessionManager.DestroySession("bg");
        ctx.Save.RequestSwitchForegroundLevel("empty_game");
        ctx.FlushFrame();

        var fg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("empty_game", fg.LevelId);
        Assert.Empty(fg.GetEntities());

        var (found, val) = fg.SessionBlackboard.TryGet<string>("note");
        Assert.True(found);
        Assert.Equal("empty", val);
    }

    [Fact]
    public void SaveBackgroundSession_ManyEntities_ThenSwitch_AllLoaded()
    {
        var (ctx, _) = CreateForegroundContext();
        const int EntityCount = 50;

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "massive", true);
        for (var i = 0; i < EntityCount; i++)
            bg.Spawn(CreateMeta($"Entity_{i:D3}"));

        ctx.Save.RequestSaveGameAuto();
        ctx.FlushFrame();

        ctx.Runtime.SessionManager.DestroySession("bg");
        ctx.Save.RequestSwitchForegroundLevel("massive");
        ctx.FlushFrame();

        var fg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal(EntityCount, fg.GetEntities().Count);

        for (var i = 0; i < EntityCount; i++)
            Assert.NotNull(fg.FindByName($"Entity_{i:D3}"));
    }

    // ── SwitchForeground explicit persist contract ─────────────────────

    [Fact]
    public void SwitchForeground_AutoPersistsOldForeground_IncludingProgress()
    {
        var (ctx, fs) = CreateForegroundContext();

        var oldFg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(oldFg);
        oldFg.Spawn(CreateMeta("OldEntity"));
        oldFg.SessionBlackboard.SetValue("old_key", "old_value");

        fs.SeedFile("root/current/level_new/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_new/session.json", "{}");
        fs.SeedFile("root/current/level_new/session_state_machines.json",
            "{\"machines\":[]}");

        ctx.Save.RequestSwitchForegroundLevel("new");
        ctx.FlushFrame();

        // Explicit PersistForegroundLevelState before ResetForeground writes old fg data
        Assert.True(fs.Exists("root/current/level_test_level/snd_scene.json"));
        Assert.True(fs.Exists("root/current/level_test_level/session.json"));
        Assert.True(fs.Exists("root/current/level_test_level/session_state_machines.json"));

        // Progress files are also written
        Assert.True(fs.Exists("root/current/progress.json"));
        Assert.True(fs.Exists("root/current/progress_state_machines.json"));
    }

    [Fact]
    public void SwitchForeground_BackgroundSessionStateIsNotAutoPersisted()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level", true);
        bg.Spawn(CreateMeta("BgEntity"));
        bg.SessionBlackboard.SetValue("bg_key", "bg_value");

        fs.SeedFile("root/current/level_new/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_new/session.json", "{}");
        fs.SeedFile("root/current/level_new/session_state_machines.json",
            "{\"machines\":[]}");

        var progressRun = ctx.EnsureProgressRun();
        ctx.Save.RequestSwitchForegroundLevel("new");
        ctx.FlushFrame();

        // Background session's level data is NOT auto-persisted by SwitchForeground
        Assert.False(fs.Exists("root/current/level_bg_level/snd_scene.json"));
        Assert.False(fs.Exists("root/current/level_bg_level/session.json"));
        Assert.False(fs.Exists("root/current/level_bg_level/session_state_machines.json"));
    }

    [Fact]
    public void SwitchForeground_BackgroundSessionStateCanBeExplicitlyPersisted()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level", true);
        bg.Spawn(CreateMeta("BgEntity"));
        bg.SessionBlackboard.SetValue("bg_key", "bg_value");

        // Explicitly persist all session state (including the background session) before switch
        ctx.Save.RequestSaveGameAuto();
        ctx.FlushFrame();

        fs.SeedFile("root/current/level_new/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_new/session.json", "{}");
        fs.SeedFile("root/current/level_new/session_state_machines.json",
            "{\"machines\":[]}");

        var progressRun = ctx.EnsureProgressRun();
        ctx.Save.RequestSwitchForegroundLevel("new");
        ctx.FlushFrame();

        Assert.True(fs.Exists("root/current/level_bg_level/snd_scene.json"));
        Assert.True(fs.Exists("root/current/level_bg_level/session.json"));
        Assert.True(fs.Exists("root/current/level_bg_level/session_state_machines.json"));
    }

    // ── BuildSavePayload collision detection ────────────────────────────

    [Fact]
    public void BuildSavePayload_LevelIdCollision_CaughtAtSessionCreation()
    {
        var (ctx, _) = CreateForegroundContext();

        // CreateBackgroundSession with the same levelId as foreground is rejected immediately,
        // so it never reaches AppendBackgroundPayloads
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "test_level", true));
        Assert.Contains("bg", ex.Message);
        Assert.Contains("test_level", ex.Message);
        Assert.Contains("already manages this level", ex.Message);
    }

    // ── SwitchForeground edge cases ─────────────────────────────────────

    [Fact]
    public void SwitchForeground_ToSameLevel_ReloadsFromCurrent()
    {
        var (ctx, fs) = CreateForegroundContext();

        var oldFg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(oldFg);
        oldFg.Spawn(CreateMeta("SameEntity"));
        oldFg.SessionBlackboard.SetValue("same_level_data", 42);

        var progressRun = ctx.EnsureProgressRun();
        ctx.Save.RequestSwitchForegroundLevel("test_level");
        ctx.FlushFrame();

        var fg = (SessionRun?)progressRun.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("test_level", fg.LevelId);

        var entities = fg.GetEntities();
        Assert.Single(entities);
        Assert.Equal("SameEntity", entities.First().Name);

        var (found, val) = fg.SessionBlackboard.TryGet<int>("same_level_data");
        Assert.True(found);
        Assert.Equal(42, val);
    }

    [Fact]
    public void BuildSavePayload_WithoutForegroundSession_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("res://entry/entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var tm = new TypeStringMapping();
        var systemBb = new Blackboard.Blackboard();
        var runtime = TestFactory.CreateRuntime(logger, host, tm, systemBb, dataSourceIo);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial",
            "res://entry/entry.json"));

        var progressRun = TestFactory.CreateProgressRun(
            "no_fg", logger, metaAccess, pathResolver, "root", runtime, ctx, sharedDataSourceIo: dataSourceIo);
        ctx.SetProgressRun(progressRun);

        ctx.Save.RequestSaveGame("no_fg");
        Assert.Throws<InvalidOperationException>(() => ctx.FlushFrame());
    }

    // ── Auto-handle background session collision during switch ───────────

    [Fact]
    public void SwitchForeground_BackgroundCollision_AutoDestroysBackground()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "game", true);
        bg.Spawn(CreateMeta("BgEntity"));
        bg.SessionBlackboard.SetValue("auto_key", 42);

        fs.SeedFile("root/current/level_game/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_game/session.json", "{}");
        fs.SeedFile("root/current/level_game/session_state_machines.json",
            "{\"machines\":[]}");

        var progressRun = ctx.EnsureProgressRun();
        ctx.Save.RequestSwitchForegroundLevel("game");
        ctx.FlushFrame();

        Assert.Null(ctx.Runtime.SessionManager.TryGet("bg"));

        var fg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("game", fg.LevelId);
    }

    [Fact]
    public void SwitchForeground_BackgroundCollision_PreservesBackgroundData()
    {
        var (ctx, _) = CreateForegroundContext();

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "game", true);
        bg.Spawn(CreateMeta("PreservedEntity"));
        bg.SessionBlackboard.SetValue("preserved_int", 123);
        bg.SessionBlackboard.SetValue("preserved_str", "data");

        var progressRun = ctx.EnsureProgressRun();
        ctx.Save.RequestSwitchForegroundLevel("game");
        ctx.FlushFrame();

        var fg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("game", fg.LevelId);

        var entities = fg.GetEntities();
        Assert.Single(entities);
        Assert.Equal("PreservedEntity", entities.First().Name);

        var (foundInt, intVal) = fg.SessionBlackboard.TryGet<int>("preserved_int");
        Assert.True(foundInt);
        Assert.Equal(123, intVal);

        var (foundStr, strVal) = fg.SessionBlackboard.TryGet<string>("preserved_str");
        Assert.True(foundStr);
        Assert.Equal("data", strVal);
    }

    [Fact]
    public void SwitchForeground_BackgroundCollision_ManyEntitiesAllPreserved()
    {
        var (ctx, _) = CreateForegroundContext();
        const int EntityCount = 30;

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "massive", true);
        for (var i = 0; i < EntityCount; i++)
            bg.Spawn(CreateMeta($"Entity_{i:D3}"));
        bg.SessionBlackboard.SetValue("count", EntityCount);

        var progressRun = ctx.EnsureProgressRun();
        ctx.Save.RequestSwitchForegroundLevel("massive");
        ctx.FlushFrame();

        var fg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal(EntityCount, fg.GetEntities().Count);

        for (var i = 0; i < EntityCount; i++)
            Assert.NotNull(fg.FindByName($"Entity_{i:D3}"));

        var (found, val) = fg.SessionBlackboard.TryGet<int>("count");
        Assert.True(found);
        Assert.Equal(EntityCount, val);
    }

    [Fact]
    public void SwitchForeground_BackgroundCollision_EmptyBackgroundWorks()
    {
        var (ctx, _) = CreateForegroundContext();

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("empty_bg", "empty_level", true);

        var progressRun = ctx.EnsureProgressRun();
        ctx.Save.RequestSwitchForegroundLevel("empty_level");
        ctx.FlushFrame();

        Assert.Null(ctx.Runtime.SessionManager.TryGet("empty_bg"));

        var fg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("empty_level", fg.LevelId);
        Assert.Empty(fg.GetEntities());
    }

    [Fact]
    public void SwitchForeground_BackgroundCollision_OtherBackgroundsUntouched()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var target = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("target", "game", true);
        target.Spawn(CreateMeta("TargetEntity"));
        using var survivor = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("survivor", "other_level", true);
        survivor.Spawn(CreateMeta("SurvivorEntity"));
        survivor.SessionBlackboard.SetValue("survivor_data", 999);

        var progressRun = ctx.EnsureProgressRun();
        ctx.Save.RequestSwitchForegroundLevel("game");
        ctx.FlushFrame();

        Assert.Null(ctx.Runtime.SessionManager.TryGet("target"));

        var alive = ctx.Runtime.SessionManager.TryGet("survivor");
        Assert.NotNull(alive);
        Assert.Single(alive.GetEntities());
        Assert.Equal("SurvivorEntity", alive.GetEntities().First().Name);

        var (found, val) = alive.SessionBlackboard.TryGet<int>("survivor_data");
        Assert.True(found);
        Assert.Equal(999, val);
    }

    [Fact]
    public void SwitchForeground_BackgroundCollision_TopologyCorrectAfterAutoDestroy()
    {
        var (ctx, _) = CreateForegroundContext();

        using var target = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("target", "game", true);
        using var survivor = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("survivor", "other_level");
        target.Spawn(CreateMeta("TargetEntity"));

        var progressRun = ctx.EnsureProgressRun();
        ctx.Save.RequestSwitchForegroundLevel("game");
        ctx.FlushFrame();

        var (found, topology) = progressRun.ProgressBlackboard
            .TryGet<string>(WellKnownKeys.SessionTopology);
        Assert.True(found);
        Assert.Contains("__foreground__=game=false", topology);
        Assert.Contains("survivor=other_level=false", topology);
        Assert.DoesNotContain("target=game", topology);
    }

    [Fact]
    public void SwitchForeground_BackgroundCollision_WithForegroundActive()
    {
        var (ctx, _) = CreateForegroundContext();

        var oldFg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(oldFg);
        oldFg.Spawn(CreateMeta("OldFgEntity"));
        oldFg.SessionBlackboard.SetValue("old_fg_data", "preserved");

        using var target = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("target", "game", true);
        target.Spawn(CreateMeta("TargetEntity"));
        target.SessionBlackboard.SetValue("target_data", "from_bg");

        var progressRun = ctx.EnsureProgressRun();
        ctx.Save.RequestSwitchForegroundLevel("game");
        ctx.FlushFrame();

        Assert.Null(ctx.Runtime.SessionManager.TryGet("target"));

        var fg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("game", fg.LevelId);

        var entities = fg.GetEntities();
        Assert.Single(entities);
        Assert.Equal("TargetEntity", entities.First().Name);

        var (found, val) = fg.SessionBlackboard.TryGet<string>("target_data");
        Assert.True(found);
        Assert.Equal("from_bg", val);

        Assert.False(fg.SessionBlackboard.TryGet<string>("old_fg_data").found);
    }

    [Fact]
    public void SwitchForeground_BackgroundCollision_ProgressPersistedAtEnd()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var target = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("target", "game", true);
        target.Spawn(CreateMeta("TargetEntity"));

        var progressRun = ctx.EnsureProgressRun();
        ctx.Save.RequestSwitchForegroundLevel("game");
        ctx.FlushFrame();

        Assert.True(fs.Exists("root/current/progress.json"));
        Assert.True(fs.Exists("root/current/progress_state_machines.json"));

        Assert.True(fs.Exists("root/current/level_game/snd_scene.json"));
        Assert.True(fs.Exists("root/current/level_game/session.json"));
        Assert.True(fs.Exists("root/current/level_game/session_state_machines.json"));
    }

    [Fact]
    public void SwitchForeground_BackgroundCollision_NoDataLossRoundTrip()
    {
        var (ctx, fs) = CreateForegroundContext();

        var oldFg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(oldFg);
        oldFg.Spawn(CreateMeta("OldFgEntity"));
        oldFg.SessionBlackboard.SetValue("fg_key", "fg_value");

        using var target = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("target", "game", true);
        target.Spawn(CreateMeta("TargetEntity1"));
        target.Spawn(CreateMeta("TargetEntity2"));
        target.SessionBlackboard.SetValue("bg_key", "bg_value");

        var progressRun = ctx.EnsureProgressRun();
        ctx.Save.RequestSwitchForegroundLevel("game");
        ctx.FlushFrame();

        var fg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        var entities = fg.GetEntities();
        Assert.Equal(2, entities.Count);
        Assert.Contains(entities, e => e.Name == "TargetEntity1");
        Assert.Contains(entities, e => e.Name == "TargetEntity2");

        var (foundBg, bgVal) = fg.SessionBlackboard.TryGet<string>("bg_key");
        Assert.True(foundBg);
        Assert.Equal("bg_value", bgVal);

        var (foundFg, fgVal) = fg.SessionBlackboard.TryGet<string>("fg_key");
        Assert.False(foundFg);

        ctx.Save.RequestSaveGame("roundtrip");
        ctx.FlushFrame();

        ctx.EnsureProgressRun().Dispose();
        ctx.SetProgressRun(null);

        ctx.Save.RequestLoadGame("roundtrip");
        ctx.FlushFrame();

        var restoredFg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(restoredFg);
        Assert.Equal("game", restoredFg.LevelId);

        var restoredEntities = restoredFg.GetEntities();
        Assert.Equal(2, restoredEntities.Count);

        var (rfBg, rvBg) = restoredFg.SessionBlackboard.TryGet<string>("bg_key");
        Assert.True(rfBg);
        Assert.Equal("bg_value", rvBg);
    }

    [Fact]
    public void SwitchForeground_BackgroundCollision_DeferredQueueHandling()
    {
        var (ctx, _) = CreateForegroundContext();

        using var target = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("target", "game", true);
        target.Spawn(CreateMeta("DeferredEntity"));
        target.SessionBlackboard.SetValue("deferred_key", 55);

        ctx.Save.RequestSwitchForegroundLevel("game");
        ctx.FlushFrame();

        Assert.Null(ctx.Runtime.SessionManager.TryGet("target"));

        var fg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("game", fg.LevelId);

        var entities = fg.GetEntities();
        Assert.Single(entities);
        Assert.Equal("DeferredEntity", entities.First().Name);

        var (found, val) = fg.SessionBlackboard.TryGet<int>("deferred_key");
        Assert.True(found);
        Assert.Equal(55, val);
    }

    [Fact]
    public void SwitchForeground_BackgroundCollision_SubsequentSwitchStillWorks()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg1 = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg1", "level_a", true);
        bg1.Spawn(CreateMeta("EntityA"));
        bg1.SessionBlackboard.SetValue("which", "a");

        using var bg2 = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg2", "level_b", true);
        bg2.Spawn(CreateMeta("EntityB"));
        bg2.SessionBlackboard.SetValue("which", "b");

        var progressRun = ctx.EnsureProgressRun();

        ctx.Save.RequestSwitchForegroundLevel("level_a");
        ctx.FlushFrame();
        var fgA = (SessionRun?)progressRun.SessionManager.ForegroundSession;
        Assert.NotNull(fgA);
        Assert.Equal("level_a", fgA.LevelId);
        Assert.Equal("a", fgA.SessionBlackboard.TryGet<string>("which").value);
        Assert.Null(ctx.Runtime.SessionManager.TryGet("bg1"));
        Assert.NotNull(ctx.Runtime.SessionManager.TryGet("bg2"));

        ctx.Save.RequestSwitchForegroundLevel("level_b");
        ctx.FlushFrame();
        var fgB = (SessionRun?)progressRun.SessionManager.ForegroundSession;
        Assert.NotNull(fgB);
        Assert.Equal("level_b", fgB.LevelId);
        Assert.Equal("b", fgB.SessionBlackboard.TryGet<string>("which").value);
        Assert.Null(ctx.Runtime.SessionManager.TryGet("bg2"));
    }
}
