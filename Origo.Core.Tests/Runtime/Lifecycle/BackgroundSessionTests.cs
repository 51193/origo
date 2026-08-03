using Origo.Core.Runtime.Lifecycle;
using System;
using System.Collections.Generic;
using System.Threading;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Scene;
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
///     Tests for background <see cref="ISessionRun" /> (created via
///     <see cref="ISessionManager.CreateBackgroundSession" />), <see cref="FullMemorySndSceneHost" />,
///     <see cref="NullNodeFactory" />, and <see cref="TestMemoryFileSystem" />.
///     Tests prefer the <see cref="ISndSceneHost" /> interface; concrete
///     <see cref="FullMemorySndSceneHost" /> is only used where its extra methods
///     (ProcessAll, DeadByName) are under test.
/// </summary>
[Collection("StrategyStateTests")]
public class BackgroundSessionTests
{
    private const string _trackingStrategyIndex = "test.tracking";
    private const string _processStrategyIndex = "test.process";
    private const string _sessionContextStrategyIndex = "test.session_context";

    public static TheoryData<string?> CreateBackgroundSession_InvalidLevelIds_Data { get; } =
        CreateBackgroundSessionInvalidLevelIds();

    // ── Creation & basic state ────────────────────────────────────────

    [Fact]
    public void CreateBackgroundSession_ReturnsInitializedSession()
    {
        var (ctx, _) = CreateForegroundContext();

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg_level", "bg_level");

        Assert.Equal("bg_level", bg.LevelId);
        Assert.NotNull(bg.SessionBlackboard);
        Assert.NotNull(bg.GetSessionStateMachines());
        Assert.NotNull(bg.GetSessionStateMachines());
        Assert.NotNull(((SessionRun)bg).SceneHost);
        Assert.IsType<ISndSceneHost>(((SessionRun)bg).SceneHost, exactMatch: false);
        Assert.Empty(bg.GetEntities());
    }

    [Theory]
    [MemberData(nameof(CreateBackgroundSession_InvalidLevelIds_Data))]
    public void CreateBackgroundSession_Throws_WhenLevelIdInvalid(string? levelId)
    {
        var (ctx, _) = CreateForegroundContext();
        Assert.ThrowsAny<ArgumentException>(() => ctx.Runtime.SessionManager.CreateBackgroundSession("test_key", levelId!));
    }

    // ── Shared ProgressBlackboard ─────────────────────────────────────

    [Fact]
    public void SharedProgressBlackboard_ForegroundWriteVisibleToBackground()
    {
        var (ctx, _) = CreateForegroundContext();

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg");

        ctx.Blackboard.ProgressBlackboard!.SetValue("shared_key", 42);

        // Background session shares the same ProgressBlackboard via SndContext.
        var (found, value) = ctx.Blackboard.ProgressBlackboard!.TryGet<int>("shared_key");
        Assert.True(found);
        Assert.Equal(42, value);
    }

    [Fact]
    public void SharedProgressBlackboard_BackgroundWriteVisibleToForeground()
    {
        var (ctx, _) = CreateForegroundContext();

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg");
        ctx.Blackboard.ProgressBlackboard!.SetValue("from_bg", "hello");

        var (found, value) = ctx.Blackboard.ProgressBlackboard!.TryGet<string>("from_bg");
        Assert.True(found);
        Assert.Equal("hello", value);
    }

    // ── Shared SndWorld (strategy pool) ───────────────────────────────

    [Fact]
    public void SharedSndWorld_StrategiesFireInBackground()
    {
        var events = new List<string>();
        var (ctx, _) = CreateForegroundContext(world =>
        {
            TrackingStrategy.Bind(events);
            world.RegisterStrategy(() => new TrackingStrategy());
        });

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg");
        var entity = bg.Spawn(CreateMetaWithStrategy("bg_entity"));
        if (entity is IEntityLifecycle lc)
            lc.FireAfterSpawnHooks();

        Assert.Contains("AfterSpawn:bg_entity", events);
    }

    [Fact]
    public void SessionContext_OwningSession_CorrectlyBoundToBackgroundSession()
    {
        var seenSessionLevelIds = new List<string>();
        var (ctx, _) = CreateForegroundContext(world =>
        {
            SessionContextSpyStrategy.Bind(seenSessionLevelIds);
            world.RegisterStrategy(() => new SessionContextSpyStrategy());
        });

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg_ctx", "bg_ctx", true);
        bg.Spawn(CreateMetaWithIndices("spy", _sessionContextStrategyIndex));
        ctx.Runtime.SessionManager.ProcessAllSessions(0.016, false);

        Assert.Contains("bg_ctx", seenSessionLevelIds);
    }

    // ── Own SessionBlackboard ─────────────────────────────────────────

    [Fact]
    public void OwnSessionBlackboard_IsolatedFromForeground()
    {
        var (ctx, _) = CreateForegroundContext();

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg");
        bg.SessionBlackboard.SetValue("bg_only", 99);

        var (found, _) = ctx.Runtime.SessionManager.ForegroundSession?.SessionBlackboard!.TryGet<int>("bg_only") ?? (false, 0);
        Assert.False(found);
    }

    // ── Own entities ──────────────────────────────────────────────────

    [Fact]
    public void OwnEntities_IsolatedFromForeground()
    {
        var (ctx, _) = CreateForegroundContext();

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg");
        bg.Spawn(CreateMeta("bg_entity"));

        Assert.Null(ctx.Runtime.SessionManager.ForegroundSession?.FindByName("bg_entity"));
        Assert.NotNull(bg.FindByName("bg_entity"));
    }

    [Fact]
    public void KillAll_TriggersBeforeDead()
    {
        var events = new List<string>();
        var (ctx, _) = CreateForegroundContext(world =>
        {
            TrackingStrategy.Bind(events);
            world.RegisterStrategy(() => new TrackingStrategy());
        });

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg");
        bg.Spawn(CreateMetaWithStrategy("npc"));

        var entity = bg.FindByName("npc");
        Assert.NotNull(entity);
        if (entity is IEntityLifecycle lc)
            lc.FireBeforeDeadHooks();

        Assert.Contains("BeforeDead:npc", events);
    }

    // ── Spawn / FindByName / GetEntities ──────────────────────────────

    [Fact]
    public void Spawn_AddsEntity()
    {
        var (ctx, _) = CreateForegroundContext();

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg");
        var entity = bg.Spawn(CreateMeta("npc"));

        Assert.Equal("npc", entity.Name);
        Assert.Single(bg.GetEntities());
        Assert.Same(entity, bg.FindByName("npc"));
    }

    [Fact]
    public void SpawnMany_AddsAll()
    {
        var (ctx, _) = CreateForegroundContext();

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg");
        bg.SpawnMany(CreateMeta("a"), CreateMeta("b"), CreateMeta("c"));

        Assert.Equal(3, bg.GetEntities().Count);
    }

    [Fact]
    public void FindByName_ReturnsNullWhenNotFound()
    {
        var (ctx, _) = CreateForegroundContext();

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg");

        Assert.Null(bg.FindByName("nonexistent"));
    }

    // ── DeadByName / ClearAll ─────────────────────────────────────────

    [Fact]
    public void DeadByName_RemovesEntity_FiresBeforeDead()
    {
        var events = new List<string>();
        var (ctx, _) = CreateForegroundContext(world =>
        {
            TrackingStrategy.Bind(events);
            world.RegisterStrategy(() => new TrackingStrategy());
        });

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg");
        bg.Spawn(CreateMetaWithStrategy("npc"));

        bg.RequestKillEntity("npc");
        ctx.Runtime.SessionManager.KillPendingAllSessions();

        Assert.Contains("BeforeDead:npc", events);
        Assert.Empty(bg.GetEntities());
    }

    [Fact]
    public void ClearAll_RemovesAll_FiresBeforeQuit()
    {
        var events = new List<string>();
        var (ctx, _) = CreateForegroundContext(world =>
        {
            TrackingStrategy.Bind(events);
            world.RegisterStrategy(() => new TrackingStrategy());
        });

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg");
        var host = ((SessionRun)bg).SceneHost;
        host.CreateEntity(CreateMetaWithStrategy("a"));
        host.CreateEntity(CreateMetaWithStrategy("b"));

        foreach (var e in host.GetEntities())
            if (e is IEntityLifecycle lc)
            {
                lc.FireBeforeQuitHooks();
                lc.ReleaseStrategiesOnly();
                lc.TeardownOnly();
            }

        host.RemoveAllEntities();

        Assert.Contains("BeforeQuit:a", events);
        Assert.Contains("BeforeQuit:b", events);
        Assert.Empty(host.GetEntities());
    }

    // ── Tick (Process) ────────────────────────────────────────────────

    [Fact]
    public void ProcessAll_FiresProcessOnEntities()
    {
        var processCount = 0;
        var (ctx, _) = CreateForegroundContext(world =>
        {
            ProcessCounterStrategy.Bind(() => processCount++);
            world.RegisterStrategy(() => new ProcessCounterStrategy());
        });

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg", true);
        bg.Spawn(CreateMetaWithIndices("npc", _processStrategyIndex));

        ctx.Runtime.SessionManager.ProcessAllSessions(0.016, false);

        Assert.Equal(1, processCount);
    }

    // ── SerializeMetaList ─────────────────────────────────────────────

    [Fact]
    public void SerializeMetaList_ReturnsAllEntities()
    {
        var (ctx, _) = CreateForegroundContext();

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg");
        bg.Spawn(CreateMeta("a"));
        bg.Spawn(CreateMeta("b"));

        var list = ((SessionRun)bg).SceneHost.BuildMetaList();

        Assert.Equal(2, list.Count);
    }

    // ── PersistLevelState ─────────────────────────────────────────────

    [Fact]
    public void PersistLevelState_WritesPayloadToFileSystem()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("dungeon", "dungeon");
        bg.Spawn(CreateMeta("boss"));
        bg.SessionBlackboard.SetValue("difficulty", "hard");

        ctx.Save.RequestSaveGame("persist_dungeon");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists("root/save_persist_dungeon/level_dungeon/snd_scene.json"));
        Assert.True(fs.Exists("root/save_persist_dungeon/level_dungeon/session.json"));
        Assert.True(fs.Exists("root/save_persist_dungeon/level_dungeon/session_state_machines.json"));
    }

    // ── Dispose ───────────────────────────────────────────────────────

    [Fact]
    public void Dispose_ClearsEntities()
    {
        var events = new List<string>();
        var (ctx, _) = CreateForegroundContext(world =>
        {
            TrackingStrategy.Bind(events);
            world.RegisterStrategy(() => new TrackingStrategy());
        });

        var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg");
        bg.Spawn(CreateMetaWithStrategy("npc"));
        bg.Dispose();

        Assert.Contains("BeforeQuit:npc", events);
        Assert.Throws<ObjectDisposedException>(() => bg.FindByName("any"));
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var (ctx, _) = CreateForegroundContext();
        var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg");
        bg.Dispose();
        var ex = Record.Exception(bg.Dispose);
        Assert.Null(ex);
    }

    [Fact]
    public void DisposedSession_ThrowsOnAllPublicMethods()
    {
        var (ctx, _) = CreateForegroundContext();
        var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg");
        bg.Dispose();

        Assert.Throws<ObjectDisposedException>(() => bg.SessionBlackboard);
        Assert.Throws<ObjectDisposedException>(() => bg.GetSessionStateMachines());
        Assert.Throws<ObjectDisposedException>(() => bg.FindByName("any"));
        Assert.Throws<ObjectDisposedException>(() => bg.GetEntities());
    }

    // ── Full background workflow ──────────────────────────────────────

    [Fact]
    public void FullWorkflow_CreatePopulateTickSave()
    {
        var events = new List<string>();
        var (ctx, fs) = CreateForegroundContext(world =>
        {
            TrackingStrategy.Bind(events);
            ProcessCounterStrategy.Bind(() => events.Add("Process"));
            world.RegisterStrategy(() => new TrackingStrategy());
            world.RegisterStrategy(() => new ProcessCounterStrategy());
        });

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("generated_level", "generated_level", true);

        // Populate with entities.
        bg.Spawn(CreateMetaWithIndices("guard_01", _trackingStrategyIndex, _processStrategyIndex));
        bg.Spawn(CreateMetaWithIndices("guard_02", _trackingStrategyIndex));
        Assert.Contains("AfterSpawn:guard_01", events);
        Assert.Contains("AfterSpawn:guard_02", events);
        Assert.Equal(2, bg.GetEntities().Count);

        // ProcessAll: Process fires.
        events.Clear();
        ctx.Runtime.SessionManager.ProcessAllSessions(0.016, false);
        Assert.Contains("Process", events);

        // Set session data.
        bg.SessionBlackboard.SetValue("patrol_route", "north");

        // Write to ProgressBlackboard (shared with foreground).
        ctx.Blackboard.ProgressBlackboard!.SetValue("generated_level_ready", true);
        var (ready, _) = ctx.Blackboard.ProgressBlackboard!.TryGet<bool>("generated_level_ready");
        Assert.True(ready);

        // Save via ISndContext.
        events.Clear();
        ctx.Save.RequestSaveGame("full_workflow_save");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();
        Assert.Contains("BeforeSave:guard_01", events);
        Assert.Contains("BeforeSave:guard_02", events);

        // Verify files exist on shared file system.
        Assert.True(fs.Exists("root/save_full_workflow_save/level_generated_level/snd_scene.json"));
        Assert.True(fs.Exists("root/save_full_workflow_save/level_generated_level/session.json"));
        Assert.True(fs.Exists("root/save_full_workflow_save/level_generated_level/session_state_machines.json"));
    }

    // ── SerializeToPayload / LoadFromPayload ─────────────────────────

    [Fact]
    public void SerializeToPayload_ReturnsLevelPayload_WithCorrectLevelIdAndData()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg_level", "bg_level");
        bg.Spawn(CreateMeta("soldier_01"));
        bg.SessionBlackboard.SetValue("hp", 100);

        ctx.Save.RequestSaveGame("ser_test");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists("root/save_ser_test/level_bg_level/snd_scene.json"));
        Assert.True(fs.Exists("root/save_ser_test/level_bg_level/session.json"));
        Assert.True(fs.Exists("root/save_ser_test/level_bg_level/session_state_machines.json"));

        var sndJson = fs.ReadAllText("root/save_ser_test/level_bg_level/snd_scene.json");
        var sessionJson = fs.ReadAllText("root/save_ser_test/level_bg_level/session.json");
        Assert.False(string.IsNullOrWhiteSpace(sndJson));
        Assert.False(string.IsNullOrWhiteSpace(sessionJson));
        Assert.Contains("soldier_01", sndJson);
        Assert.Contains("hp", sessionJson);
    }

    [Fact]
    public void LoadFromPayload_RestoresSessionState()
    {
        var events = new List<string>();
        var (ctx, fs) = CreateForegroundContext(world =>
        {
            TrackingStrategy.Bind(events);
            world.RegisterStrategy(() => new TrackingStrategy());
        });

        using var source = ctx.Runtime.SessionManager.CreateBackgroundSession("src_level", "src_level");
        source.Spawn(CreateMetaWithStrategy("guard_01"));
        source.SessionBlackboard.SetValue("alert", 5);

        ctx.Save.RequestSaveGame("src_save");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists("root/save_src_save/level_src_level/session.json"));
        Assert.True(fs.Exists("root/save_src_save/level_src_level/snd_scene.json"));
    }

    [Fact]
    public void SerializeToPayload_ThenLoadFromPayload_RoundTrips()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg");
        bg.Spawn(CreateMeta("unit_a"));
        bg.Spawn(CreateMeta("unit_b"));
        bg.SessionBlackboard.SetValue("score", 42);

        ctx.Save.RequestSaveGame("roundtrip");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists("root/save_roundtrip/level_bg/snd_scene.json"));
        Assert.True(fs.Exists("root/save_roundtrip/level_bg/session.json"));
        Assert.NotNull(bg.FindByName("unit_a"));
        Assert.NotNull(bg.FindByName("unit_b"));
        var (found, value) = bg.SessionBlackboard.TryGet<int>("score");
        Assert.True(found);
        Assert.Equal(42, value);
    }

    [Fact]
    public void LoadFromPayload_Throws_WhenDisposed()
    {
        var (ctx, fs) = CreateForegroundContext();

        var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg");
        bg.Dispose();

        ctx.Save.RequestSaveGame("after_dispose");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        Assert.False(fs.Exists("root/save_after_dispose/level_bg/snd_scene.json"));
    }

    [Fact]
    public void SerializeToPayload_Throws_WhenDisposed()
    {
        var (ctx, fs) = CreateForegroundContext();

        var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg");
        bg.Spawn(CreateMeta("entity"));
        bg.Dispose();

        ctx.Save.RequestSaveGame("after_dispose2");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        Assert.False(fs.Exists("root/save_after_dispose2/level_bg/snd_scene.json"));
    }

    // ── Background session load from payload ───────────────────────────

    [Fact]
    public void CreateBackgroundSession_ThenLoadSessionFromPayload_RestoresState()
    {
        var events = new List<string>();
        var (ctx, fs) = CreateForegroundContext(world =>
        {
            TrackingStrategy.Bind(events);
            world.RegisterStrategy(() => new TrackingStrategy());
        });

        using var source = ctx.Runtime.SessionManager.CreateBackgroundSession("src", "src");
        source.Spawn(CreateMetaWithStrategy("npc_a"));
        source.SessionBlackboard.SetValue("difficulty", "hard");

        ctx.Save.RequestSaveGame("load_sess");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists("root/save_load_sess/level_src/session.json"));
        Assert.NotNull(source.FindByName("npc_a"));
        var (found, value) = source.SessionBlackboard.TryGet<string>("difficulty");
        Assert.True(found);
        Assert.Equal("hard", value);
    }

    [Fact]
    public void LoadSessionFromPayload_Throws_WhenPayloadNull()
    {
        var (ctx, _) = CreateForegroundContext();
        Assert.Throws<ArgumentException>(() => ctx.Runtime.SessionManager.CreateBackgroundSession("bg", ""));
    }

    // ── FullMemorySndSceneHost ────────────────────────────────────────

    [Fact]
    public void FullMemorySndSceneHost_ProcessAll_FiresProcess()
    {
        var processCount = 0;
        var (ctx, _) = CreateForegroundContext(world =>
        {
            ProcessCounterStrategy.Bind(() => processCount++);
            world.RegisterStrategy(() => new ProcessCounterStrategy());
        });

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg", true);
        bg.Spawn(CreateMetaWithIndices("npc", _processStrategyIndex));

        ctx.Runtime.SessionManager.ProcessAllSessions(0.016, false);

        Assert.Equal(1, processCount);
    }

    [Fact]
    public void FullMemorySndSceneHost_LoadFromMetaList_ClearsAndLoads()
    {
        var events = new List<string>();
        var (ctx, _) = CreateForegroundContext(world =>
        {
            TrackingStrategy.Bind(events);
            world.RegisterStrategy(() => new TrackingStrategy());
        });

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg");
        var host = ((SessionRun)bg).SceneHost;
        host.CreateEntity(CreateMetaWithStrategy("old_entity"));
        events.Clear();

        host.RemoveAllEntities();
        host.RecoverFromMetaList([CreateMetaWithStrategy("new_entity")]);
        foreach (var e in host.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireAfterLoadHooks();

        Assert.Contains("AfterLoad:new_entity", events);
        Assert.NotNull(host.FindByName("new_entity"));
        Assert.Null(host.FindByName("old_entity"));
    }

    // ── Background session persistence round-trip ─────────────────────

    [Fact]
    public void BuildSavePayload_IncludesBackgroundSessionsInPayload()
    {
        var (ctx, _) = CreateForegroundContext();
        var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg1", "bg_level", true);
        bg.SessionBlackboard.SetValue("bg_key", 42);

        ctx.Save.RequestSaveGame("save001");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();
        var payload = ctx.StorageService.ReadSavePayloadFromSnapshot("save001", "test_level");

        // Should contain both foreground and background levels.
        Assert.True(payload.Levels.ContainsKey("test_level"));
        Assert.True(payload.Levels.ContainsKey("bg_level"));

        // Session topology should be persisted in progress blackboard.
        var (found, bgIds) = ctx.Blackboard.ProgressBlackboard!.TryGet<string>(WellKnownKeys.SessionTopology);
        Assert.True(found);
        Assert.Contains("bg1=bg_level", bgIds);
    }

    [Fact]
    public void SaveAndLoad_RoundTrips_BackgroundSessions()
    {
        var (ctx, fs) = CreateForegroundContext();

        // Create and mount a background session with data.
        var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("sim1", "bg_sim", true);
        bg.SessionBlackboard.SetValue("sim_round", 10);
        bg.Spawn(CreateMeta("BgEntity"));

        // Save and reload through the public save/load flow.
        ctx.Save.RequestSaveGame("save_bg_test");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        // Clean up current runs.
        ctx.Runtime.SessionManager.DestroySession("sim1");
        ctx.EnsureProgressRun().Dispose();
        ctx.SetProgressRun(null);

        // Reload from saved snapshot.
        ctx.Save.RequestLoadGame("save_bg_test");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        // Verify the background session was restored.
        Assert.NotNull(ctx.Runtime.SessionManager.TryGet("sim1"));
        var restoredBg = ctx.Runtime.SessionManager.TryGet("sim1")!;
        Assert.Equal("bg_sim", restoredBg.LevelId);
        var (found, round) = restoredBg.SessionBlackboard.TryGet<int>("sim_round");
        Assert.True(found);
        Assert.Equal(10, round);
        Assert.Single(restoredBg.GetEntities());
    }

    [Fact]
    public void BuildSavePayload_WithNoBackgroundSessions_ClearsBackgroundLevelIds()
    {
        var (ctx, _) = CreateForegroundContext();

        // Set a stale but valid topology value (includes foreground + stale background).
        ctx.Blackboard.ProgressBlackboard!.SetValue(
            WellKnownKeys.SessionTopology,
            $"{ISessionManager.ForegroundKey}=test_level=false,stale=old=false");

        ctx.Save.RequestSaveGame("save_empty");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        // SessionTopology should always include foreground session.
        var (found, bgIds) = ctx.Blackboard.ProgressBlackboard!.TryGet<string>(WellKnownKeys.SessionTopology);
        Assert.True(found);
        Assert.Contains($"{ISessionManager.ForegroundKey}=test_level=false", bgIds);

        // Only foreground level in payload.
        var payload = ctx.StorageService.ReadSavePayloadFromSnapshot("save_empty", "test_level");
        Assert.Single(payload.Levels);
    }

    [Fact]
    public void BuildSavePayload_IncludesSyncProcessInBackgroundLevelIds()
    {
        var (ctx, _) = CreateForegroundContext();
        ctx.Runtime.SessionManager.CreateBackgroundSession("bg_sync", "bg_sync_level", true);
        ctx.Runtime.SessionManager.CreateBackgroundSession("bg_nosync", "bg_nosync_level");

        ctx.Save.RequestSaveGame("save_sync_test");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        var (found, bgIds) = ctx.Blackboard.ProgressBlackboard!.TryGet<string>(WellKnownKeys.SessionTopology);
        Assert.True(found);
        Assert.Contains("bg_sync=bg_sync_level=true", bgIds);
        Assert.Contains("bg_nosync=bg_nosync_level=false", bgIds);
    }

    [Fact]
    public void SaveAndLoad_RoundTrips_SyncProcessFlag()
    {
        var (ctx, _) = CreateForegroundContext();
        ctx.Runtime.SessionManager.CreateBackgroundSession("bg_sync", "bg_sync_level", true);
        ctx.Runtime.SessionManager.CreateBackgroundSession("bg_nosync", "bg_nosync_level");

        ctx.Save.RequestSaveGame("save_sync_rt");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        // Clean up and reload.
        ctx.Runtime.SessionManager.DestroySession("bg_sync");
        ctx.Runtime.SessionManager.DestroySession("bg_nosync");
        ctx.EnsureProgressRun().Dispose();
        ctx.SetProgressRun(null);

        ctx.Save.RequestLoadGame("save_sync_rt");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        // Verify syncProcess was restored for the true session.
        Assert.NotNull(ctx.Runtime.SessionManager.TryGet("bg_sync"));
        Assert.NotNull(ctx.Runtime.SessionManager.TryGet("bg_nosync"));

        // Verify via another save: the persisted format records the correct flags.
        ctx.Save.RequestSaveGame("save_sync_rt2");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();
        var (found2, bgIds2) = ctx.Blackboard.ProgressBlackboard!.TryGet<string>(WellKnownKeys.SessionTopology);
        Assert.True(found2);
        Assert.Contains("bg_sync=bg_sync_level=true", bgIds2);
        Assert.Contains("bg_nosync=bg_nosync_level=false", bgIds2);
    }

    [Fact]
    public void SaveAndLoad_FromDisk_RestoresBackgroundSessions()
    {
        var (ctx, fs) = CreateForegroundContext();

        // Create a background session with data.
        var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("sim1", "bg_sim", true);
        bg.SessionBlackboard.SetValue("sim_round", 10);
        bg.Spawn(CreateMeta("BgEntity"));

        // Save to disk (both current/ and snapshot).
        ctx.Save.RequestSaveGame("save_disk_test");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        // Read back from snapshot (simulating a full reload from disk).
        var readPayload = ctx.StorageService.ReadSavePayloadFromSnapshot(
            "save_disk_test", "test_level");

        // The payload should include the background level.
        Assert.True(readPayload.Levels.ContainsKey("bg_sim"),
            "Snapshot read should include background session levels.");

        // Clean up and reload from disk.
        ctx.Runtime.SessionManager.DestroySession("sim1");
        ctx.EnsureProgressRun().Dispose();
        ctx.SetProgressRun(null);

        ctx.Save.RequestLoadGame("save_disk_test");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        // Verify the background session was restored.
        var restoredBg = ctx.Runtime.SessionManager.TryGet("sim1");
        Assert.NotNull(restoredBg);
        Assert.Equal("bg_sim", restoredBg!.LevelId);
        var (found, round) = restoredBg.SessionBlackboard.TryGet<int>("sim_round");
        Assert.True(found);
        Assert.Equal(10, round);
        Assert.Single(restoredBg.GetEntities());
    }

    [Fact]
    public void ReadFromCurrent_IncludesAllLevelDirectories()
    {
        var (ctx, fs) = CreateForegroundContext();

        // Create a background session.
        var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg1", "bg_level");
        bg.SessionBlackboard.SetValue("val", 99);

        // Save (writes to current/ and snapshot).
        ctx.Save.RequestSaveGame("save_cur_test");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        // Read back from current/ — should include both levels.
        var readPayload = ctx.StorageService.ReadSavePayloadFromCurrent(
            "save_cur_test", "test_level");
        Assert.True(readPayload.Levels.ContainsKey("test_level"));
        Assert.True(readPayload.Levels.ContainsKey("bg_level"),
            "ReadFromCurrent should enumerate and include background level directories.");

        bg.Dispose();
    }

    // ── Helper methods ────────────────────────────────────────────────

    private static (SndContext ctx, TestMemoryFileSystem fs) CreateForegroundContext(
        Action<SndWorld>? configureWorld = null)
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

    private static SndMetaData CreateMeta(string name)
    {
        return new SndMetaData
        {
            Name = name,
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        };
    }

    private static SndMetaData CreateMetaWithStrategy(string name)
    {
        return new SndMetaData
        {
            Name = name,
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData { LifecycleIndices = [_trackingStrategyIndex] },
            DataMetaData = new DataMetaData()
        };
    }

    private static SndMetaData CreateMetaWithIndices(string name, params string[] indices)
    {
        return new SndMetaData
        {
            Name = name,
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData { LifecycleIndices = [.. indices] },
            DataMetaData = new DataMetaData()
        };
    }

    private static TheoryData<string?> CreateBackgroundSessionInvalidLevelIds()
    {
        var d = new TheoryData<string?>
        {
            default(string?),
            "",
            "   "
        };
        return d;
    }

    // ── Test strategy implementations ─────────────────────────────────

    [StrategyIndex(_trackingStrategyIndex)]
    private sealed class TrackingStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _events = new();

        public static void Bind(List<string> events) => _events.Value = events;

        public override void AfterSpawn(ISndEntity entity, ISndContext ctx) =>
            _events.Value?.Add($"AfterSpawn:{entity.Name}");

        public override void AfterLoad(ISndEntity entity, ISndContext ctx) =>
            _events.Value?.Add($"AfterLoad:{entity.Name}");

        public override void AfterAdd(ISndEntity entity, ISndContext ctx) =>
            _events.Value?.Add($"AfterAdd:{entity.Name}");

        public override void BeforeRemove(ISndEntity entity, ISndContext ctx) =>
            _events.Value?.Add($"BeforeRemove:{entity.Name}");

        public override void BeforeSave(ISndEntity entity, ISndContext ctx) =>
            _events.Value?.Add($"BeforeSave:{entity.Name}");

        public override void BeforeQuit(ISndEntity entity, ISndContext ctx) =>
            _events.Value?.Add($"BeforeQuit:{entity.Name}");

        public override void BeforeDead(ISndEntity entity, ISndContext ctx) =>
            _events.Value?.Add($"BeforeDead:{entity.Name}");
    }

    [StrategyIndex(_processStrategyIndex)]
    private sealed class ProcessCounterStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<Action?> _onProcess = new();

        public static void Bind(Action onProcess) => _onProcess.Value = onProcess;

        public override void Process(ISndEntity entity, double delta, ISndContext ctx) =>
            _onProcess.Value?.Invoke();
    }

    [StrategyIndex(_sessionContextStrategyIndex)]
    private sealed class SessionContextSpyStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _seen = new();

        public static void Bind(List<string> seen) => _seen.Value = seen;

        public override void Process(ISndEntity entity, double delta, ISndContext ctx) => _seen.Value?.Add(entity.OwningSession.LevelId);
    }
}
