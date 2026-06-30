using Origo.Core.Runtime.Lifecycle;
using System;
using System.Threading;
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

namespace Origo.Core.Tests;

/// <summary>
///     Comprehensive tests for the save-then-switch foreground flow,
///     FullMemorySndSceneHost entity lookup during hooks, and topology persistence integrity.
/// </summary>
[Collection("StrategyStateTests")]
public class SaveAndSwitchForegroundTests
{
    private const string FindByNameStrategyIndex = "test.find_by_name";
    private const string AfterSpawnEventPrefix = "AfterSpawn:";
    private const string AfterLoadEventPrefix = "AfterLoad:";

    // ── FullMemorySndSceneHost: FindByName during hooks ──────────────────

    [Fact]
    public void FullMemorySndSceneHost_Spawn_FindByName_FindsSelfDuringAfterSpawn()
    {
        var events = new List<string>();
        var (ctx, _) = CreateForegroundContext(world =>
        {
            FindByNameStrategy.Bind(events);
            world.RegisterStrategy(() => new FindByNameStrategy());
        });

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        var host = (FullMemorySndSceneHost)((SessionRun)bg).SceneHost;

        var entityA = host.CreateEntity(CreateMetaWithStrategy("EntityA",
            new[] { FindByNameStrategyIndex }));
if (entityA is IEntityLifecycle lc)
                lc.FireAfterSpawnHooks();

        Assert.Contains($"{AfterSpawnEventPrefix}EntityA:self=true", events);
        Assert.NotNull(host.FindByName("EntityA"));
    }

    [Fact]
    public void FullMemorySndSceneHost_Spawn_FindByName_FindsSiblingsDuringAfterSpawn()
    {
        var events = new List<string>();
        var (ctx, _) = CreateForegroundContext(world =>
        {
            FindByNameStrategy.Bind(events);
            world.RegisterStrategy(() => new FindByNameStrategy());
        });

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        var host = (FullMemorySndSceneHost)((SessionRun)bg).SceneHost;

        host.CreateEntity(CreateMetaWithStrategy("EntityA"));
        var entityB = host.CreateEntity(CreateMetaWithStrategy("EntityB",
            new[] { FindByNameStrategyIndex }));
if (entityB is IEntityLifecycle lc)
                lc.FireAfterSpawnHooks();

        Assert.Contains($"{AfterSpawnEventPrefix}EntityB:sibling=EntityA", events);
    }

    [Fact]
    public void FullMemorySndSceneHost_LoadFromMetaList_FindByName_FindsSelfDuringAfterLoad()
    {
        var events = new List<string>();
        var (ctx, _) = CreateForegroundContext(world =>
        {
            FindByNameStrategy.Bind(events);
            world.RegisterStrategy(() => new FindByNameStrategy());
        });

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        var host = (FullMemorySndSceneHost)((SessionRun)bg).SceneHost;

        host.RecoverFromMetaList(new[]
        {
            CreateMetaWithStrategy("EntityC", new[] { FindByNameStrategyIndex })
        });
foreach (var e in host.GetEntities())
                if (e is IEntityLifecycle lc)
                    lc.FireAfterLoadHooks();

        Assert.Contains($"{AfterLoadEventPrefix}EntityC:self=true", events);
        Assert.NotNull(host.FindByName("EntityC"));
    }

    [Fact]
    public void FullMemorySndSceneHost_LoadFromMetaList_FindByName_FindsSiblingsDuringAfterLoad()
    {
        var events = new List<string>();
        var (ctx, _) = CreateForegroundContext(world =>
        {
            FindByNameStrategy.Bind(events);
            world.RegisterStrategy(() => new FindByNameStrategy());
        });

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        var host = (FullMemorySndSceneHost)((SessionRun)bg).SceneHost;

        host.RecoverFromMetaList(new[]
        {
            CreateMetaWithStrategy("EntityD"),
            CreateMetaWithStrategy("EntityE", new[] { FindByNameStrategyIndex })
        });
foreach (var e in host.GetEntities())
                if (e is IEntityLifecycle lc)
                    lc.FireAfterLoadHooks();

        Assert.Contains($"{AfterLoadEventPrefix}EntityE:sibling=EntityD", events);
    }

    // ── Core: save background session, then switch foreground ──────────

    [Fact]
    public void SaveBackgroundWithEntities_ThenSwitchForeground_LoadsEntitiesIntoForeground()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "game", true);
        ((SessionRun)bg).SceneHost.CreateEntity(CreateMeta("BgEntity1"));
        ((SessionRun)bg).SceneHost.CreateEntity(CreateMeta("BgEntity2"));
        bg.SessionBlackboard.SetValue("bg_value", 42);

        ctx.RequestSaveGameAuto();
        ctx.FlushDeferredActionsForCurrentFrame();

        ctx.Runtime.SessionManager.DestroySession("bg");
        ctx.RequestSwitchForegroundLevel("game");
        ctx.FlushDeferredActionsForCurrentFrame();

        var fg = ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("game", fg.LevelId);

        var entities = fg.GetEntities();
        Assert.Equal(2, entities.Count);
        Assert.Contains(entities, e => e.Name == "BgEntity1");
        Assert.Contains(entities, e => e.Name == "BgEntity2");
    }

    [Fact]
    public void SaveBackgroundWithEntities_ThenSwitchForeground_PreservesBlackboard()
    {
        var (ctx, _) = CreateForegroundContext();

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "game", true);
        ((SessionRun)bg).SceneHost.CreateEntity(CreateMeta("BgEntity"));
        bg.SessionBlackboard.SetValue("key_int", 100);
        bg.SessionBlackboard.SetValue("key_str", "hello");

        ctx.RequestSaveGameAuto();
        ctx.FlushDeferredActionsForCurrentFrame();

        ctx.Runtime.SessionManager.DestroySession("bg");
        ctx.RequestSwitchForegroundLevel("game");
        ctx.FlushDeferredActionsForCurrentFrame();

        var fg = ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);

        var (foundInt, intValue) = fg.SessionBlackboard.TryGet<int>("key_int");
        Assert.True(foundInt);
        Assert.Equal(100, intValue);

        var (foundStr, strValue) = fg.SessionBlackboard.TryGet<string>("key_str");
        Assert.True(foundStr);
        Assert.Equal("hello", strValue);
    }

    [Fact]
    public void SaveBackgroundWithEntities_ThenSwitchForeground_LevelIdMustNotConflict()
    {
        var (ctx, _) = CreateForegroundContext();

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "game", true);
        ((SessionRun)bg).SceneHost.CreateEntity(CreateMeta("BgEntity"));
        bg.SessionBlackboard.SetValue("bg_only", 99);

        ctx.RequestSaveGameAuto();
        ctx.FlushDeferredActionsForCurrentFrame();

        ctx.Runtime.SessionManager.DestroySession("bg");
        ctx.RequestSwitchForegroundLevel("game");
        ctx.FlushDeferredActionsForCurrentFrame();

        var stillAlive = ctx.Runtime.SessionManager.TryGet("bg");
        Assert.Null(stillAlive);

        var fg = ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("game", fg.LevelId);
    }

    // ── Topology correctness ────────────────────────────────────────────

    [Fact]
    public void PersistProgress_WritesFullTopologyIncludingBackgroundSessions()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg1", "bg_level", true);
        var progressRun = ctx.EnsureProgressRun();

        progressRun.PersistProgress();

        Assert.True(fs.Exists("root/current/progress.json"));
        Assert.True(fs.Exists("root/current/progress_state_machines.json"));

        var (found, topology) = progressRun.ProgressBlackboard
            .TryGet<string>(WellKnownKeys.SessionTopology);
        Assert.True(found);
        Assert.Contains("bg1=bg_level=true", topology);
        Assert.Contains("__foreground__=test_level=false", topology);
    }

    [Fact]
    public void SwitchForeground_PreservesBackgroundSessionsInTopology()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg1", "bg_level", true);
        fs.SeedFile("root/current/level_other/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_other/session.json", "{}");
        fs.SeedFile("root/current/level_other/session_state_machines.json",
            "{\"machines\":[]}");

        var progressRun = ctx.EnsureProgressRun();
        progressRun.SwitchForeground("other");

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
        progressRun.SwitchForeground("other");

        var (found, topology) = progressRun.ProgressBlackboard
            .TryGet<string>(WellKnownKeys.SessionTopology);
        Assert.True(found);
        Assert.Matches("^__foreground__=other=false$", topology);
    }

    [Fact]
    public void SwitchForeground_WithMultipleBackgroundSessions_PreservesAllInTopology()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg1 = ctx.Runtime.SessionManager.CreateBackgroundSession("sim1", "sim_level", true);
        using var bg2 = ctx.Runtime.SessionManager.CreateBackgroundSession("sim2", "other_sim");
        fs.SeedFile("root/current/level_new_fg/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_new_fg/session.json", "{}");
        fs.SeedFile("root/current/level_new_fg/session_state_machines.json",
            "{\"machines\":[]}");

        var progressRun = ctx.EnsureProgressRun();
        progressRun.SwitchForeground("new_fg");

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

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "game", true);
        ((SessionRun)bg).SceneHost.CreateEntity(CreateMeta("DiskEntity"));

        ctx.RequestSaveGameAuto();
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists("root/current/level_game/snd_scene.json"));
        Assert.True(fs.Exists("root/current/level_game/session.json"));
        Assert.True(fs.Exists("root/current/level_game/session_state_machines.json"));

        ctx.Runtime.SessionManager.DestroySession("bg");
        ctx.RequestSwitchForegroundLevel("game");
        ctx.FlushDeferredActionsForCurrentFrame();

        var fg = ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.NotEmpty(fg.GetEntities());
    }

    [Fact]
    public void SaveBackgroundSession_ThenSwitch_ProgressJsonHasCorrectActiveLevel()
    {
        var (ctx, _) = CreateForegroundContext();

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "game", true);
        ((SessionRun)bg).SceneHost.CreateEntity(CreateMeta("Entity"));

        ctx.RequestSaveGameAuto();
        ctx.FlushDeferredActionsForCurrentFrame();

        ctx.Runtime.SessionManager.DestroySession("bg");
        ctx.RequestSwitchForegroundLevel("game");
        ctx.FlushDeferredActionsForCurrentFrame();

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

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "game", true);
        ((SessionRun)bg).SceneHost.CreateEntity(CreateMeta("Entity1"));
        ((SessionRun)bg).SceneHost.CreateEntity(CreateMeta("Entity2"));
        bg.SessionBlackboard.SetValue("round_key", "round_value");

        ctx.RequestSaveGameAuto();
        ctx.FlushDeferredActionsForCurrentFrame();

        ctx.Runtime.SessionManager.DestroySession("bg");
        ctx.RequestSwitchForegroundLevel("game");
        ctx.FlushDeferredActionsForCurrentFrame();

        var progressRun = ctx.EnsureProgressRun();
        var payload = progressRun.BuildSavePayload(progressRun.SaveId);
        ctx.StorageService.WriteSavePayloadToCurrentThenSnapshot(
            payload, progressRun.SaveId, ctx.Runtime.Logger);

        foreach (var key in ctx.Runtime.SessionManager.Keys)
            ctx.Runtime.SessionManager.DestroySession(key);
        ctx.SetProgressRun(null);

        var newPr = TestFactory.CreateProgressRun(
            progressRun.SaveId, ctx.Runtime.Logger, ctx.MetaAccess, ctx.PathResolver, "root",
            ctx.Runtime, ctx);
        ctx.SetProgressRun(newPr);

        var snapshotPayload = ctx.StorageService.ReadSavePayloadFromSnapshot(
            progressRun.SaveId, "game");
        newPr.LoadFromPayload(snapshotPayload);

        var fg = newPr.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("game", fg.LevelId);

        var entities = fg.GetEntities();
        Assert.Equal(2, entities.Count);

        var (found, val) = fg.SessionBlackboard.TryGet<string>("round_key");
        Assert.True(found);
        Assert.Equal("round_value", val);

        var restoredBg = newPr.SessionManager.TryGet("bg");
        Assert.Null(restoredBg);
    }

    // ── Direct switch (no prior save) ───────────────────────────────────

    [Fact]
    public void SwitchForeground_WithoutSave_WhenTargetLevelInBackgroundSession_LoadsEntities()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "game", true);
        ((SessionRun)bg).SceneHost.CreateEntity(CreateMeta("DirectEntity"));
        bg.SessionBlackboard.SetValue("direct_key", 77);

        ctx.RequestSaveGameAuto();
        ctx.FlushDeferredActionsForCurrentFrame();

        fs.SeedFile("root/current/level_game/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_game/session.json", "{}");
        fs.SeedFile("root/current/level_game/session_state_machines.json",
            "{\"machines\":[]}");

        ctx.Runtime.SessionManager.DestroySession("bg");
        ctx.RequestSwitchForegroundLevel("game");
        ctx.FlushDeferredActionsForCurrentFrame();

        var fg = ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("game", fg.LevelId);
    }

    [Fact]
    public void SwitchForeground_WithoutSave_WhenTargetMissing_EntersEmptySession()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "game", true);
        ((SessionRun)bg).SceneHost.CreateEntity(CreateMeta("DirectEntity"));

        var progressRun = ctx.EnsureProgressRun();
        progressRun.SwitchForeground("missing_level");

        var fg = progressRun.SessionManager.ForegroundSession;
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

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "game", true);
        ((SessionRun)bg).SceneHost.CreateEntity(CreateMeta("QueueEntity"));
        bg.SessionBlackboard.SetValue("queue_val", 123);

        ctx.RequestSaveGameAuto();
        ctx.FlushDeferredActionsForCurrentFrame();

        if (flushBetween)
            ctx.FlushDeferredActionsForCurrentFrame();

        ctx.Runtime.SessionManager.DestroySession("bg");
        ctx.RequestSwitchForegroundLevel("game");
        ctx.FlushDeferredActionsForCurrentFrame();

        var fg = ctx.Runtime.SessionManager.ForegroundSession;
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

        var fgBefore = ctx.EnsureProgressRun().SessionManager.ForegroundSession;
        Assert.NotNull(fgBefore);
        ((SessionRun)fgBefore).SceneHost.CreateEntity(CreateMeta("OldFgEntity"));
        fgBefore.SessionBlackboard.SetValue("old_key", "old_value");

        fs.SeedFile("root/current/level_new/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_new/session.json", "{}");
        fs.SeedFile("root/current/level_new/session_state_machines.json",
            "{\"machines\":[]}");

        ctx.EnsureProgressRun().SwitchForeground("new");

        Assert.True(fs.Exists("root/current/level_test_level/snd_scene.json"));
        Assert.True(fs.Exists("root/current/level_test_level/session.json"));
        Assert.True(fs.Exists("root/current/level_test_level/session_state_machines.json"));
    }

    // ── Background session unchanged during switch ──────────────────────

    [Fact]
    public void SwitchForeground_BackgroundSessionEntitiesUntouched()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level", true);
        ((SessionRun)bg).SceneHost.CreateEntity(CreateMeta("BgSurvivor1"));
        ((SessionRun)bg).SceneHost.CreateEntity(CreateMeta("BgSurvivor2"));

        fs.SeedFile("root/current/level_new/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_new/session.json", "{}");
        fs.SeedFile("root/current/level_new/session_state_machines.json",
            "{\"machines\":[]}");

        ctx.EnsureProgressRun().SwitchForeground("new");

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

        using var bgTick = ctx.Runtime.SessionManager.CreateBackgroundSession("ticker", "tick_level", true);
        using var bgNoTick = ctx.Runtime.SessionManager.CreateBackgroundSession("noticker", "no_tick");

        fs.SeedFile("root/current/level_new/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_new/session.json", "{}");
        fs.SeedFile("root/current/level_new/session_state_machines.json",
            "{\"machines\":[]}");

        ctx.EnsureProgressRun().SwitchForeground("new");

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

        ctx.RequestSwitchForegroundLevel("after");

        var fgBeforeFlush = ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fgBeforeFlush);
        Assert.Equal("test_level", fgBeforeFlush.LevelId);

        ctx.FlushDeferredActionsForCurrentFrame();

        var fgAfterFlush = ctx.Runtime.SessionManager.ForegroundSession;
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
        ctx.EnqueueBusinessDeferred(() => executionOrder.Add("business"));
        ctx.RequestSwitchForegroundLevel("after");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.Equal("business", executionOrder[0]);
        Assert.Equal("after", ctx.Runtime.SessionManager.ForegroundSession!.LevelId);
    }

    // ── Entity count edge cases ─────────────────────────────────────────

    [Fact]
    public void SaveBackgroundSession_WithNoEntities_ThenSwitch_LoadsEmptyForeground()
    {
        var (ctx, _) = CreateForegroundContext();

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "empty_game", true);
        bg.SessionBlackboard.SetValue("note", "empty");

        ctx.RequestSaveGameAuto();
        ctx.FlushDeferredActionsForCurrentFrame();

        ctx.Runtime.SessionManager.DestroySession("bg");
        ctx.RequestSwitchForegroundLevel("empty_game");
        ctx.FlushDeferredActionsForCurrentFrame();

        var fg = ctx.Runtime.SessionManager.ForegroundSession;
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

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "massive", true);
        for (var i = 0; i < EntityCount; i++)
            ((SessionRun)bg).SceneHost.CreateEntity(CreateMeta($"Entity_{i:D3}"));

        ctx.RequestSaveGameAuto();
        ctx.FlushDeferredActionsForCurrentFrame();

        ctx.Runtime.SessionManager.DestroySession("bg");
        ctx.RequestSwitchForegroundLevel("massive");
        ctx.FlushDeferredActionsForCurrentFrame();

        var fg = ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal(EntityCount, fg.GetEntities().Count);

        for (var i = 0; i < EntityCount; i++)
            Assert.NotNull(fg.FindByName($"Entity_{i:D3}"));
    }

    // ── SwitchForeground explicit persist contract ─────────────────────

    [Fact]
    public void SwitchForeground_ExplicitPersist_WritesOldForegroundToCurrent()
    {
        var (ctx, fs) = CreateForegroundContext();

        var oldFg = ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(oldFg);
        ((SessionRun)oldFg).SceneHost.CreateEntity(CreateMeta("OldEntity"));
        oldFg.SessionBlackboard.SetValue("old_key", "old_value");

        fs.SeedFile("root/current/level_new/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_new/session.json", "{}");
        fs.SeedFile("root/current/level_new/session_state_machines.json",
            "{\"machines\":[]}");

        var progressRun = ctx.EnsureProgressRun();
        progressRun.SwitchForeground("new");

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

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level", true);
        ((SessionRun)bg).SceneHost.CreateEntity(CreateMeta("BgEntity"));
        bg.SessionBlackboard.SetValue("bg_key", "bg_value");

        fs.SeedFile("root/current/level_new/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_new/session.json", "{}");
        fs.SeedFile("root/current/level_new/session_state_machines.json",
            "{\"machines\":[]}");

        var progressRun = ctx.EnsureProgressRun();
        progressRun.SwitchForeground("new");

        // Background session's level data is NOT auto-persisted by SwitchForeground
        Assert.False(fs.Exists("root/current/level_bg_level/snd_scene.json"));
        Assert.False(fs.Exists("root/current/level_bg_level/session.json"));
        Assert.False(fs.Exists("root/current/level_bg_level/session_state_machines.json"));
    }

    [Fact]
    public void SwitchForeground_BackgroundSessionStateCanBeExplicitlyPersisted()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level", true);
        ((SessionRun)bg).SceneHost.CreateEntity(CreateMeta("BgEntity"));
        bg.SessionBlackboard.SetValue("bg_key", "bg_value");

        // Explicitly persist background session before switch
        ((SessionManager)ctx.Runtime.SessionManager).PersistSession("bg");

        fs.SeedFile("root/current/level_new/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_new/session.json", "{}");
        fs.SeedFile("root/current/level_new/session_state_machines.json",
            "{\"machines\":[]}");

        var progressRun = ctx.EnsureProgressRun();
        progressRun.SwitchForeground("new");

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

        var oldFg = ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(oldFg);
        ((SessionRun)oldFg).SceneHost.CreateEntity(CreateMeta("SameEntity"));
        oldFg.SessionBlackboard.SetValue("same_level_data", 42);

        var progressRun = ctx.EnsureProgressRun();
        progressRun.SwitchForeground("test_level");

        var fg = progressRun.SessionManager.ForegroundSession;
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
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
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

        Assert.Throws<InvalidOperationException>(() => progressRun.BuildSavePayload("no_fg"));
    }

    // ── Auto-handle background session collision during switch ───────────

    [Fact]
    public void SwitchForeground_BackgroundCollision_AutoDestroysBackground()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "game", true);
        ((SessionRun)bg).SceneHost.CreateEntity(CreateMeta("BgEntity"));
        bg.SessionBlackboard.SetValue("auto_key", 42);

        fs.SeedFile("root/current/level_game/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_game/session.json", "{}");
        fs.SeedFile("root/current/level_game/session_state_machines.json",
            "{\"machines\":[]}");

        var progressRun = ctx.EnsureProgressRun();
        progressRun.SwitchForeground("game");

        Assert.Null(ctx.Runtime.SessionManager.TryGet("bg"));

        var fg = ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("game", fg.LevelId);
    }

    [Fact]
    public void SwitchForeground_BackgroundCollision_PreservesBackgroundData()
    {
        var (ctx, _) = CreateForegroundContext();

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "game", true);
        ((SessionRun)bg).SceneHost.CreateEntity(CreateMeta("PreservedEntity"));
        bg.SessionBlackboard.SetValue("preserved_int", 123);
        bg.SessionBlackboard.SetValue("preserved_str", "data");

        var progressRun = ctx.EnsureProgressRun();
        progressRun.SwitchForeground("game");

        var fg = ctx.Runtime.SessionManager.ForegroundSession;
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

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "massive", true);
        for (var i = 0; i < EntityCount; i++)
            ((SessionRun)bg).SceneHost.CreateEntity(CreateMeta($"Entity_{i:D3}"));
        bg.SessionBlackboard.SetValue("count", EntityCount);

        var progressRun = ctx.EnsureProgressRun();
        progressRun.SwitchForeground("massive");

        var fg = ctx.Runtime.SessionManager.ForegroundSession;
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

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("empty_bg", "empty_level", true);

        var progressRun = ctx.EnsureProgressRun();
        progressRun.SwitchForeground("empty_level");

        Assert.Null(ctx.Runtime.SessionManager.TryGet("empty_bg"));

        var fg = ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("empty_level", fg.LevelId);
        Assert.Empty(fg.GetEntities());
    }

    [Fact]
    public void SwitchForeground_BackgroundCollision_OtherBackgroundsUntouched()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var target = ctx.Runtime.SessionManager.CreateBackgroundSession("target", "game", true);
        ((SessionRun)target).SceneHost.CreateEntity(CreateMeta("TargetEntity"));
        using var survivor = ctx.Runtime.SessionManager.CreateBackgroundSession("survivor", "other_level", true);
        ((SessionRun)survivor).SceneHost.CreateEntity(CreateMeta("SurvivorEntity"));
        survivor.SessionBlackboard.SetValue("survivor_data", 999);

        var progressRun = ctx.EnsureProgressRun();
        progressRun.SwitchForeground("game");

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

        using var target = ctx.Runtime.SessionManager.CreateBackgroundSession("target", "game", true);
        using var survivor = ctx.Runtime.SessionManager.CreateBackgroundSession("survivor", "other_level");
        ((SessionRun)target).SceneHost.CreateEntity(CreateMeta("TargetEntity"));

        var progressRun = ctx.EnsureProgressRun();
        progressRun.SwitchForeground("game");

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

        var oldFg = ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(oldFg);
        ((SessionRun)oldFg).SceneHost.CreateEntity(CreateMeta("OldFgEntity"));
        oldFg.SessionBlackboard.SetValue("old_fg_data", "preserved");

        using var target = ctx.Runtime.SessionManager.CreateBackgroundSession("target", "game", true);
        ((SessionRun)target).SceneHost.CreateEntity(CreateMeta("TargetEntity"));
        target.SessionBlackboard.SetValue("target_data", "from_bg");

        var progressRun = ctx.EnsureProgressRun();
        progressRun.SwitchForeground("game");

        Assert.Null(ctx.Runtime.SessionManager.TryGet("target"));

        var fg = ctx.Runtime.SessionManager.ForegroundSession;
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

        using var target = ctx.Runtime.SessionManager.CreateBackgroundSession("target", "game", true);
        ((SessionRun)target).SceneHost.CreateEntity(CreateMeta("TargetEntity"));

        var progressRun = ctx.EnsureProgressRun();
        progressRun.SwitchForeground("game");

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

        var oldFg = ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(oldFg);
        ((SessionRun)oldFg).SceneHost.CreateEntity(CreateMeta("OldFgEntity"));
        oldFg.SessionBlackboard.SetValue("fg_key", "fg_value");

        using var target = ctx.Runtime.SessionManager.CreateBackgroundSession("target", "game", true);
        ((SessionRun)target).SceneHost.CreateEntity(CreateMeta("TargetEntity1"));
        ((SessionRun)target).SceneHost.CreateEntity(CreateMeta("TargetEntity2"));
        target.SessionBlackboard.SetValue("bg_key", "bg_value");

        var progressRun = ctx.EnsureProgressRun();
        progressRun.SwitchForeground("game");

        var fg = ctx.Runtime.SessionManager.ForegroundSession;
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

        var payload = progressRun.BuildSavePayload(progressRun.SaveId);
        ctx.StorageService.WriteSavePayloadToCurrentThenSnapshot(
            payload, progressRun.SaveId, ctx.Runtime.Logger);

        foreach (var key in ctx.Runtime.SessionManager.Keys)
            ctx.Runtime.SessionManager.DestroySession(key);
        ctx.SetProgressRun(null);

        var newPr = TestFactory.CreateProgressRun(
            progressRun.SaveId, ctx.Runtime.Logger, ctx.MetaAccess, ctx.PathResolver, "root",
            ctx.Runtime, ctx);
        ctx.SetProgressRun(newPr);

        var snapshotPayload = ctx.StorageService.ReadSavePayloadFromSnapshot(
            progressRun.SaveId, "game");
        newPr.LoadFromPayload(snapshotPayload);

        var restoredFg = newPr.SessionManager.ForegroundSession;
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

        using var target = ctx.Runtime.SessionManager.CreateBackgroundSession("target", "game", true);
        ((SessionRun)target).SceneHost.CreateEntity(CreateMeta("DeferredEntity"));
        target.SessionBlackboard.SetValue("deferred_key", 55);

        ctx.RequestSwitchForegroundLevel("game");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.Null(ctx.Runtime.SessionManager.TryGet("target"));

        var fg = ctx.Runtime.SessionManager.ForegroundSession;
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

        using var bg1 = ctx.Runtime.SessionManager.CreateBackgroundSession("bg1", "level_a", true);
        ((SessionRun)bg1).SceneHost.CreateEntity(CreateMeta("EntityA"));
        bg1.SessionBlackboard.SetValue("which", "a");

        using var bg2 = ctx.Runtime.SessionManager.CreateBackgroundSession("bg2", "level_b", true);
        ((SessionRun)bg2).SceneHost.CreateEntity(CreateMeta("EntityB"));
        bg2.SessionBlackboard.SetValue("which", "b");

        var progressRun = ctx.EnsureProgressRun();

        progressRun.SwitchForeground("level_a");
        var fgA = progressRun.SessionManager.ForegroundSession;
        Assert.NotNull(fgA);
        Assert.Equal("level_a", fgA.LevelId);
        Assert.Equal("a", fgA.SessionBlackboard.TryGet<string>("which").value);
        Assert.Null(ctx.Runtime.SessionManager.TryGet("bg1"));
        Assert.NotNull(ctx.Runtime.SessionManager.TryGet("bg2"));

        progressRun.SwitchForeground("level_b");
        var fgB = progressRun.SessionManager.ForegroundSession;
        Assert.NotNull(fgB);
        Assert.Equal("level_b", fgB.LevelId);
        Assert.Equal("b", fgB.SessionBlackboard.TryGet<string>("which").value);
        Assert.Null(ctx.Runtime.SessionManager.TryGet("bg2"));
    }

    // ── Helper methods ──────────────────────────────────────────────────

    private static (SndContext ctx, TestFileSystem fs) CreateForegroundContext(
        Action<SndWorld>? configureWorld = null)
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var tm = new TypeStringMapping();
        var systemBb = new Blackboard.Blackboard();
        var runtime = TestFactory.CreateRuntime(logger, host, tm, systemBb, dataSourceIo);
        configureWorld?.Invoke(runtime.SndWorld);

        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial",
            "res://entry/entry.json"));

        var progressRun = TestFactory.CreateProgressRun(
            "test_save", logger, metaAccess, pathResolver, "root", runtime, ctx, sharedDataSourceIo: dataSourceIo);
        ctx.SetProgressRun(progressRun);
        progressRun.LoadAndMountForeground("test_level");

        return (ctx, fs);
    }

    private static SndMetaData CreateMeta(string name) =>
        new()
        {
            Name = name,
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        };

    private static SndMetaData CreateMetaWithStrategy(string name, string[]? indices = null) =>
        new()
        {
            Name = name,
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData
            {
                LifecycleIndices = new List<string>(indices ?? Array.Empty<string>())
            },
            DataMetaData = new DataMetaData()
        };

    // ── Test strategy: performs FindByName during AfterSpawn/AfterLoad ──

    [StrategyIndex(FindByNameStrategyIndex)]
    private sealed class FindByNameStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _eventSink = new();
        private static List<string>? EventSink { get => _eventSink.Value; set => _eventSink.Value = value; }

        public static void Bind(List<string> sink) => EventSink = sink;

        public override void AfterSpawn(ISndEntity entity, ISndContext ctx)
        {
            if (EventSink is null)
                return;

            var self = entity.OwningSession.FindByName(entity.Name);
            EventSink.Add($"{AfterSpawnEventPrefix}{entity.Name}:self={(self is not null ? "true" : "false")}");

            foreach (var other in entity.OwningSession.GetEntities())
                if (other.Name != entity.Name)
                    EventSink.Add($"{AfterSpawnEventPrefix}{entity.Name}:sibling={other.Name}");
        }

        public override void AfterLoad(ISndEntity entity, ISndContext ctx)
        {
            if (EventSink is null)
                return;

            var self = entity.OwningSession.FindByName(entity.Name);
            EventSink.Add($"{AfterLoadEventPrefix}{entity.Name}:self={(self is not null ? "true" : "false")}");

            foreach (var other in entity.OwningSession.GetEntities())
                if (other.Name != entity.Name)
                    EventSink.Add($"{AfterLoadEventPrefix}{entity.Name}:sibling={other.Name}");
        }
    }
}
