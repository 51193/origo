using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Comprehensive tests for the save-then-switch foreground flow,
///     FullMemorySndSceneHost entity lookup during hooks, and topology persistence integrity.
/// </summary>
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

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg_level");
        var host = (FullMemorySndSceneHost)bg.SceneHost;

        host.Spawn(CreateMetaWithStrategy("EntityA",
            new[] { FindByNameStrategyIndex }));

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

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg_level");
        var host = (FullMemorySndSceneHost)bg.SceneHost;

        host.Spawn(CreateMetaWithStrategy("EntityA"));
        host.Spawn(CreateMetaWithStrategy("EntityB",
            new[] { FindByNameStrategyIndex }));

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

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg_level");
        var host = (FullMemorySndSceneHost)bg.SceneHost;

        host.LoadFromMetaList(new[]
        {
            CreateMetaWithStrategy("EntityC", new[] { FindByNameStrategyIndex })
        });

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

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg_level");
        var host = (FullMemorySndSceneHost)bg.SceneHost;

        host.LoadFromMetaList(new[]
        {
            CreateMetaWithStrategy("EntityD"),
            CreateMetaWithStrategy("EntityE", new[] { FindByNameStrategyIndex })
        });

        Assert.Contains($"{AfterLoadEventPrefix}EntityE:sibling=EntityD", events);
    }

    // ── Core: save background session, then switch foreground ──────────

    [Fact]
    public void SaveBackgroundWithEntities_ThenSwitchForeground_LoadsEntitiesIntoForeground()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "game", true);
        bg.SceneHost.Spawn(CreateMeta("BgEntity1"));
        bg.SceneHost.Spawn(CreateMeta("BgEntity2"));
        bg.SessionBlackboard.Set("bg_value", 42);

        ctx.RequestSaveGameAuto();
        ctx.RequestSwitchForegroundLevel("game");
        ctx.FlushDeferredActionsForCurrentFrame();

        var fg = ctx.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("game", fg.LevelId);

        var entities = fg.SceneHost.GetEntities();
        Assert.Equal(2, entities.Count);
        Assert.Contains(entities, e => e.Name == "BgEntity1");
        Assert.Contains(entities, e => e.Name == "BgEntity2");
    }

    [Fact]
    public void SaveBackgroundWithEntities_ThenSwitchForeground_PreservesBlackboard()
    {
        var (ctx, _) = CreateForegroundContext();

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "game", true);
        bg.SceneHost.Spawn(CreateMeta("BgEntity"));
        bg.SessionBlackboard.Set("key_int", 100);
        bg.SessionBlackboard.Set("key_str", "hello");

        ctx.RequestSaveGameAuto();
        ctx.RequestSwitchForegroundLevel("game");
        ctx.FlushDeferredActionsForCurrentFrame();

        var fg = ctx.SessionManager.ForegroundSession;
        Assert.NotNull(fg);

        var (foundInt, intValue) = fg.SessionBlackboard.TryGet<int>("key_int");
        Assert.True(foundInt);
        Assert.Equal(100, intValue);

        var (foundStr, strValue) = fg.SessionBlackboard.TryGet<string>("key_str");
        Assert.True(foundStr);
        Assert.Equal("hello", strValue);
    }

    [Fact]
    public void SaveBackgroundWithEntities_ThenSwitchForeground_BackgroundSessionSurvives()
    {
        var (ctx, _) = CreateForegroundContext();

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "game", true);
        bg.SceneHost.Spawn(CreateMeta("BgEntity"));
        bg.SessionBlackboard.Set("bg_only", 99);

        ctx.RequestSaveGameAuto();
        ctx.RequestSwitchForegroundLevel("game");
        ctx.FlushDeferredActionsForCurrentFrame();

        var stillAlive = ctx.SessionManager.TryGet("bg");
        Assert.NotNull(stillAlive);
        Assert.Equal("game", stillAlive.LevelId);

        var (found, val) = stillAlive.SessionBlackboard.TryGet<int>("bg_only");
        Assert.True(found);
        Assert.Equal(99, val);
    }

    // ── Topology correctness ────────────────────────────────────────────

    [Fact]
    public void PersistProgress_WritesFullTopologyIncludingBackgroundSessions()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg1", "bg_level", true);
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

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg1", "bg_level", true);
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

        using var bg1 = ctx.SessionManager.CreateBackgroundSession("sim1", "sim_level", true);
        using var bg2 = ctx.SessionManager.CreateBackgroundSession("sim2", "other_sim");
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

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "game", true);
        bg.SceneHost.Spawn(CreateMeta("DiskEntity"));

        ctx.RequestSaveGameAuto();
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists("root/current/level_game/snd_scene.json"));
        Assert.True(fs.Exists("root/current/level_game/session.json"));
        Assert.True(fs.Exists("root/current/level_game/session_state_machines.json"));

        ctx.RequestSwitchForegroundLevel("game");
        ctx.FlushDeferredActionsForCurrentFrame();

        var fg = ctx.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.NotEmpty(fg.SceneHost.GetEntities());
    }

    [Fact]
    public void SaveBackgroundSession_ThenSwitch_ProgressJsonHasCorrectActiveLevel()
    {
        var (ctx, _) = CreateForegroundContext();

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "game", true);
        bg.SceneHost.Spawn(CreateMeta("Entity"));

        ctx.RequestSaveGameAuto();
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

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "game", true);
        bg.SceneHost.Spawn(CreateMeta("Entity1"));
        bg.SceneHost.Spawn(CreateMeta("Entity2"));
        bg.SessionBlackboard.Set("round_key", "round_value");

        ctx.RequestSaveGameAuto();
        ctx.RequestSwitchForegroundLevel("game");
        ctx.FlushDeferredActionsForCurrentFrame();

        var progressRun = ctx.EnsureProgressRun();
        var payload = progressRun.BuildSavePayload(progressRun.SaveId);
        ctx.StorageService.WriteSavePayloadToCurrentThenSnapshot(
            payload, progressRun.SaveId, ctx.Runtime.Logger);

        foreach (var key in ctx.SessionManager.Keys)
            ctx.SessionManager.DestroySession(key);
        ctx.SetProgressRun(null);

        var newPr = TestFactory.CreateProgressRun(
            progressRun.SaveId, ctx.Runtime.Logger, fs, "root",
            ctx.Runtime, ctx);
        ctx.SetProgressRun(newPr);

        var snapshotPayload = ctx.StorageService.ReadSavePayloadFromSnapshot(
            progressRun.SaveId, "game");
        newPr.LoadFromPayload(snapshotPayload);

        var fg = newPr.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("game", fg.LevelId);

        var entities = fg.SceneHost.GetEntities();
        Assert.Equal(2, entities.Count);

        var (found, val) = fg.SessionBlackboard.TryGet<string>("round_key");
        Assert.True(found);
        Assert.Equal("round_value", val);

        var restoredBg = ctx.SessionManager.TryGet("bg");
        Assert.NotNull(restoredBg);
    }

    // ── Direct switch (no prior save) ───────────────────────────────────

    [Fact]
    public void SwitchForeground_WithoutSave_WhenTargetLevelInBackgroundSession_LoadsEntities()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "game", true);
        bg.SceneHost.Spawn(CreateMeta("DirectEntity"));
        bg.SessionBlackboard.Set("direct_key", 77);

        fs.SeedFile("root/current/level_game/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_game/session.json", "{}");
        fs.SeedFile("root/current/level_game/session_state_machines.json",
            "{\"machines\":[]}");

        var progressRun = ctx.EnsureProgressRun();
        progressRun.SwitchForeground("game");

        var fg = progressRun.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("game", fg.LevelId);
    }

    [Fact]
    public void SwitchForeground_WithoutSave_WhenTargetMissing_EntersEmptySession()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "game", true);
        bg.SceneHost.Spawn(CreateMeta("DirectEntity"));

        var progressRun = ctx.EnsureProgressRun();
        progressRun.SwitchForeground("missing_level");

        var fg = progressRun.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("missing_level", fg.LevelId);
        Assert.Empty(fg.SceneHost.GetEntities());
    }

    // ── Deferred queue ordering ─────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RequestSaveGameAuto_ThenRequestSwitchForeground_EntitiesLoadRegardlessOfFlushOrder(
        bool flushBetween)
    {
        var (ctx, _) = CreateForegroundContext();

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "game", true);
        bg.SceneHost.Spawn(CreateMeta("QueueEntity"));
        bg.SessionBlackboard.Set("queue_val", 123);

        ctx.RequestSaveGameAuto();

        if (flushBetween)
            ctx.FlushDeferredActionsForCurrentFrame();

        ctx.RequestSwitchForegroundLevel("game");
        ctx.FlushDeferredActionsForCurrentFrame();

        var fg = ctx.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("game", fg.LevelId);

        var entities = fg.SceneHost.GetEntities();
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

        var fgBefore = ctx.EnsureProgressRun().ForegroundSession;
        Assert.NotNull(fgBefore);
        fgBefore.SceneHost.Spawn(CreateMeta("OldFgEntity"));
        fgBefore.SessionBlackboard.Set("old_key", "old_value");

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

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg_level", true);
        bg.SceneHost.Spawn(CreateMeta("BgSurvivor1"));
        bg.SceneHost.Spawn(CreateMeta("BgSurvivor2"));

        fs.SeedFile("root/current/level_new/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_new/session.json", "{}");
        fs.SeedFile("root/current/level_new/session_state_machines.json",
            "{\"machines\":[]}");

        ctx.EnsureProgressRun().SwitchForeground("new");

        var bgAlive = ctx.SessionManager.TryGet("bg");
        Assert.NotNull(bgAlive);
        Assert.Equal(2, bgAlive.SceneHost.GetEntities().Count);
        Assert.Contains(bgAlive.SceneHost.GetEntities(), e => e.Name == "BgSurvivor1");
        Assert.Contains(bgAlive.SceneHost.GetEntities(), e => e.Name == "BgSurvivor2");
    }

    [Fact]
    public void SwitchForeground_BackgroundSessionTickStatePreserved()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bgTick = ctx.SessionManager.CreateBackgroundSession("ticker", "tick_level", true);
        using var bgNoTick = ctx.SessionManager.CreateBackgroundSession("noticker", "no_tick");

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

        var fgBeforeFlush = ctx.SessionManager.ForegroundSession;
        Assert.NotNull(fgBeforeFlush);
        Assert.Equal("test_level", fgBeforeFlush.LevelId);

        ctx.FlushDeferredActionsForCurrentFrame();

        var fgAfterFlush = ctx.SessionManager.ForegroundSession;
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
        Assert.Equal("after", ctx.SessionManager.ForegroundSession!.LevelId);
    }

    // ── Entity count edge cases ─────────────────────────────────────────

    [Fact]
    public void SaveBackgroundSession_WithNoEntities_ThenSwitch_LoadsEmptyForeground()
    {
        var (ctx, _) = CreateForegroundContext();

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "empty_game", true);
        bg.SessionBlackboard.Set("note", "empty");

        ctx.RequestSaveGameAuto();
        ctx.RequestSwitchForegroundLevel("empty_game");
        ctx.FlushDeferredActionsForCurrentFrame();

        var fg = ctx.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("empty_game", fg.LevelId);
        Assert.Empty(fg.SceneHost.GetEntities());

        var (found, val) = fg.SessionBlackboard.TryGet<string>("note");
        Assert.True(found);
        Assert.Equal("empty", val);
    }

    [Fact]
    public void SaveBackgroundSession_ManyEntities_ThenSwitch_AllLoaded()
    {
        var (ctx, _) = CreateForegroundContext();
        const int EntityCount = 50;

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "massive", true);
        for (var i = 0; i < EntityCount; i++)
            bg.SceneHost.Spawn(CreateMeta($"Entity_{i:D3}"));

        ctx.RequestSaveGameAuto();
        ctx.RequestSwitchForegroundLevel("massive");
        ctx.FlushDeferredActionsForCurrentFrame();

        var fg = ctx.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal(EntityCount, fg.SceneHost.GetEntities().Count);

        for (var i = 0; i < EntityCount; i++)
            Assert.NotNull(fg.SceneHost.FindByName($"Entity_{i:D3}"));
    }

    // ── Helper methods ──────────────────────────────────────────────────

    private static (SndContext ctx, TestFileSystem fs) CreateForegroundContext(
        Action<SndWorld>? configureWorld = null)
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        configureWorld?.Invoke(runtime.SndWorld);

        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var ctx = new SndContext(new SndContextParameters(runtime, fs, "root", "res://initial",
            "res://entry/entry.json"));

        var progressRun = TestFactory.CreateProgressRun(
            "test_save", logger, fs, "root", runtime, ctx);
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
                EntityIndices = new List<string>(indices ?? Array.Empty<string>())
            },
            DataMetaData = new DataMetaData()
        };

    // ── Test strategy: performs FindByName during AfterSpawn/AfterLoad ──

    [StrategyIndex(FindByNameStrategyIndex)]
    private sealed class FindByNameStrategy : EntityStrategyBase
    {
        private static ICollection<string>? EventSink { get; set; }

        public static void Bind(ICollection<string> sink) => EventSink = sink;

        public override void AfterSpawn(ISndEntity entity, ISndContext ctx)
        {
            if (EventSink is null || ctx.CurrentSession is null)
                return;

            var self = ctx.CurrentSession.SceneHost.FindByName(entity.Name);
            EventSink.Add($"{AfterSpawnEventPrefix}{entity.Name}:self={(self is not null ? "true" : "false")}");

            foreach (var other in ctx.CurrentSession.SceneHost.GetEntities())
                if (other.Name != entity.Name)
                    EventSink.Add($"{AfterSpawnEventPrefix}{entity.Name}:sibling={other.Name}");
        }

        public override void AfterLoad(ISndEntity entity, ISndContext ctx)
        {
            if (EventSink is null || ctx.CurrentSession is null)
                return;

            var self = ctx.CurrentSession.SceneHost.FindByName(entity.Name);
            EventSink.Add($"{AfterLoadEventPrefix}{entity.Name}:self={(self is not null ? "true" : "false")}");

            foreach (var other in ctx.CurrentSession.SceneHost.GetEntities())
                if (other.Name != entity.Name)
                    EventSink.Add($"{AfterLoadEventPrefix}{entity.Name}:sibling={other.Name}");
        }
    }
}
