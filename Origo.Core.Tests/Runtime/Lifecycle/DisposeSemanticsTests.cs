using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Tests that verify the Dispose contract: Dispose does NOT auto-persist,
///     does NOT trigger BeforeSave hooks, properly deletes current/ directory,
///     and handles edge cases correctly.
/// </summary>
public class DisposeSemanticsTests
{
    private const string BeforeSaveStrategyIndex = "dispose_sem.before_save";
    private const string BeforeQuitStrategyIndex = "dispose_sem.before_quit";

    // ── SessionRun.Dispose: no persist, no BeforeSave ───────────────────

    [Fact]
    public void SessionRun_Dispose_DoesNotWriteFilesToCurrent()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.SceneHost.SpawnEntity(CreateMeta("Entity"));

        var progressRun = ctx.EnsureProgressRun();
        progressRun.PersistProgress();
        bg.Dispose();

        Assert.False(fs.Exists("root/current/level_bg_level/snd_scene.json"));
        Assert.False(fs.Exists("root/current/level_bg_level/session.json"));
        Assert.False(fs.Exists("root/current/level_bg_level/session_state_machines.json"));
    }

    [Fact]
    public void SessionRun_Dispose_DoesNotTriggerBeforeSave()
    {
        var events = new List<string>();
        var (ctx, _) = CreateForegroundContext(world =>
        {
            BeforeSaveSpyStrategy.Bind(events);
            world.RegisterStrategy(() => new BeforeSaveSpyStrategy());
        });

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.SceneHost.SpawnEntity(CreateMetaWithIndex("Entity", BeforeSaveStrategyIndex));

        events.Clear();
        bg.Dispose();

        Assert.DoesNotContain("BeforeSave:Entity", events);
    }

    [Fact]
    public void SessionRun_Dispose_TriggersBeforeQuit()
    {
        var events = new List<string>();
        var (ctx, _) = CreateForegroundContext(world =>
        {
            BeforeQuitSpyStrategy.Bind(events);
            world.RegisterStrategy(() => new BeforeQuitSpyStrategy());
        });

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.SceneHost.SpawnEntity(CreateMetaWithIndex("Entity", BeforeQuitStrategyIndex));

        events.Clear();
        bg.Dispose();

        Assert.Contains("BeforeQuit:Entity", events);
    }

    [Fact]
    public void SessionRun_ExplicitPersistLevelState_WritesToCurrent_BeforeDispose()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.SceneHost.SpawnEntity(CreateMeta("Entity"));

        AsSessionRun(bg).PersistLevelState();
        Assert.True(fs.Exists("root/current/level_bg_level/snd_scene.json"));
        Assert.True(fs.Exists("root/current/level_bg_level/session.json"));
        Assert.True(fs.Exists("root/current/level_bg_level/session_state_machines.json"));

        bg.Dispose();

        // Files survive Dispose because Dispose no longer deletes current/
        Assert.True(fs.Exists("root/current/level_bg_level/snd_scene.json"));
    }

    [Fact]
    public void SessionRun_ExplicitPersistLevelState_TriggersBeforeSave()
    {
        var events = new List<string>();
        var (ctx, _) = CreateForegroundContext(world =>
        {
            BeforeSaveSpyStrategy.Bind(events);
            world.RegisterStrategy(() => new BeforeSaveSpyStrategy());
        });

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.SceneHost.SpawnEntity(CreateMetaWithIndex("Entity", BeforeSaveStrategyIndex));

        events.Clear();
        AsSessionRun(bg).PersistLevelState();

        Assert.Contains("BeforeSave:Entity", events);
    }

    // ── ProgressRun.Dispose: no persist, deletes current/ ────────────────

    [Fact]
    public void ProgressRun_Dispose_DoesNotCallPersistProgress()
    {
        var (ctx, fs) = CreateForegroundContext();

        ctx.ProgressBlackboard!.Set("test_key", 42);

        var progressRun = ctx.EnsureProgressRun();
        progressRun.Dispose();

        // PersistProgress was never explicitly called; current/progress.json should not exist
        Assert.False(fs.Exists("root/current/progress.json"));
    }

    [Fact]
    public void ProgressRun_Dispose_DeletesCurrentDirectory()
    {
        var (ctx, fs) = CreateForegroundContext();

        var progressRun = ctx.EnsureProgressRun();
        // Persist to populate current/ first
        progressRun.PersistProgress();

        Assert.True(fs.Exists("root/current/progress.json"));

        progressRun.Dispose();

        Assert.False(fs.Exists("root/current/progress.json"));
        Assert.False(fs.Exists("root/current/progress_state_machines.json"));
    }

    [Fact]
    public void ProgressRun_Dispose_DeletesCurrentDirectory_EvenWhenEmpty()
    {
        var (ctx, fs) = CreateForegroundContext();

        var progressRun = ctx.EnsureProgressRun();
        progressRun.Dispose();

        // current/ should not exist (or be empty) — dispose is idempotent for directory cleanup
        Assert.False(fs.Exists("root/current/progress.json"));
    }

    // ── Double dispose idempotency ──────────────────────────────────────

    [Fact]
    public void SessionRun_Dispose_Twice_IsIdempotent()
    {
        var (ctx, _) = CreateForegroundContext();

        var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Dispose();

        var ex = Record.Exception(() => bg.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void ProgressRun_Dispose_Twice_IsIdempotent()
    {
        var (ctx, _) = CreateForegroundContext();

        var progressRun = ctx.EnsureProgressRun();
        progressRun.Dispose();

        var ex = Record.Exception(() => progressRun.Dispose());
        Assert.Null(ex);
    }

    // ── ObjectDisposed after dispose ────────────────────────────────────

    [Fact]
    public void SessionRun_AfterDispose_SerializeToPayload_ThrowsObjectDisposed()
    {
        var (ctx, _) = CreateForegroundContext();

        var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Dispose();

        Assert.Throws<ObjectDisposedException>(() => AsSessionRun(bg).SerializeToPayload());
    }

    [Fact]
    public void SessionRun_AfterDispose_LoadFromPayload_ThrowsObjectDisposed()
    {
        var (ctx, _) = CreateForegroundContext();

        var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
            AsSessionRun(bg).LoadFromPayload(new LevelPayload()));
    }

    [Fact]
    public void SessionRun_AfterDispose_PersistLevelState_ThrowsObjectDisposed()
    {
        var (ctx, _) = CreateForegroundContext();

        var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Dispose();

        Assert.Throws<ObjectDisposedException>(() => AsSessionRun(bg).PersistLevelState());
    }

    [Fact]
    public void SessionRun_AfterDispose_SessionBlackboard_ThrowsObjectDisposed()
    {
        var (ctx, _) = CreateForegroundContext();

        var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Dispose();

        Assert.Throws<ObjectDisposedException>(() => bg.SessionBlackboard);
    }

    [Fact]
    public void SessionRun_AfterDispose_SceneHost_ThrowsObjectDisposed()
    {
        var (ctx, _) = CreateForegroundContext();

        var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Dispose();

        Assert.Throws<ObjectDisposedException>(() => bg.SceneHost);
    }

    [Fact]
    public void SessionRun_AfterDispose_GetSessionStateMachines_ThrowsObjectDisposed()
    {
        var (ctx, _) = CreateForegroundContext();

        var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Dispose();

        Assert.Throws<ObjectDisposedException>(() => bg.GetSessionStateMachines());
    }

    // ── ProgressRun post-dispose state ──────────────────────────────────

    [Fact]
    public void ProgressRun_AfterDispose_ForegroundSession_IsNull()
    {
        var (ctx, _) = CreateForegroundContext();
        var progressRun = ctx.EnsureProgressRun();

        Assert.NotNull(progressRun.ForegroundSession);

        progressRun.Dispose();

        Assert.Null(progressRun.ForegroundSession);
    }

    [Fact]
    public void ProgressRun_AfterDispose_SessionManagerKeys_IsEmpty()
    {
        var (ctx, _) = CreateForegroundContext();
        ctx.SessionManager.CreateBackgroundSession("bg", "bg_level");

        var progressRun = ctx.EnsureProgressRun();
        Assert.NotEmpty(progressRun.SessionManager.Keys);

        progressRun.Dispose();

        Assert.Empty(progressRun.SessionManager.Keys);
    }

    [Fact]
    public void ProgressRun_AfterDispose_ProgressBlackboard_IsCleared()
    {
        var (ctx, _) = CreateForegroundContext();
        ctx.ProgressBlackboard!.Set("key", "value");

        var progressRun = ctx.EnsureProgressRun();
        progressRun.Dispose();

        var (found, _) = progressRun.ProgressBlackboard.TryGet<string>("key");
        Assert.False(found);
    }

    // ── Exception safety ───────────────────────────────────────────────

    [Fact]
    public void ProgressRun_Dispose_SafeEvenWhenNoCurrentDirectory()
    {
        var (ctx, _) = CreateForegroundContext();

        var progressRun = ctx.EnsureProgressRun();
        progressRun.Dispose();

        // Second dispose is safe (no current/ exists anymore)
        var ex = Record.Exception(() => progressRun.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void ProgressRun_Dispose_StateMachineContainerClear_DoesNotThrow()
    {
        var (ctx, _) = CreateForegroundContext();

        var progressRun = ctx.EnsureProgressRun();
        var sm = progressRun.GetProgressStateMachines();

        var ex = Record.Exception(() => progressRun.Dispose());
        Assert.Null(ex);
    }

    // ── Save-then-Quit round-trip (Continue) ────────────────────────────

    [Fact]
    public void ExplicitSave_ThenDispose_ThenContinue_LoadsSavedState()
    {
        var (ctx, fs) = CreateForegroundContext();

        var fg = ctx.SessionManager.ForegroundSession;
        Assert.NotNull(fg);

        fg.SceneHost.SpawnEntity(CreateMeta("SavedEntity"));
        fg.SessionBlackboard.Set("save_key", "save_value");

        ctx.RequestSaveGame("test_001");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists("root/save_test_001/progress.json"));
        Assert.True(fs.Exists("root/save_test_001/level_test_level/snd_scene.json"));

        var progressRun = ctx.EnsureProgressRun();
        progressRun.Dispose();
        ctx.SetProgressRun(null);

        // Simulate Continue: LoadOrContinueStrict equivalent
        var payload = ctx.StorageService.ReadSavePayloadFromSnapshot("test_001", "test_level");
        var newPr = TestFactory.CreateProgressRun(
            "test_001", ctx.Runtime.Logger, fs, "root", ctx.Runtime, ctx);
        ctx.SetProgressRun(newPr);

        newPr.LoadFromPayload(payload);

        var restoredFg = newPr.ForegroundSession;
        Assert.NotNull(restoredFg);

        var entities = restoredFg.SceneHost.GetEntities();
        Assert.Single(entities);
        Assert.Equal("SavedEntity", entities.First().Name);

        var (found, val) = restoredFg.SessionBlackboard.TryGet<string>("save_key");
        Assert.True(found);
        Assert.Equal("save_value", val);
    }

    [Fact]
    public void Save_ThenDispose_ThenContinue_ProgressBlackboardPreserved()
    {
        var (ctx, fs) = CreateForegroundContext();
        ctx.ProgressBlackboard!.Set("global_score", 9001);

        ctx.RequestSaveGame("score_save");
        ctx.FlushDeferredActionsForCurrentFrame();

        var progressRun = ctx.EnsureProgressRun();
        progressRun.Dispose();
        ctx.SetProgressRun(null);

        var payload = ctx.StorageService.ReadSavePayloadFromSnapshot("score_save", "test_level");
        var newPr = TestFactory.CreateProgressRun(
            "score_save", ctx.Runtime.Logger, fs, "root", ctx.Runtime, ctx);
        ctx.SetProgressRun(newPr);
        newPr.LoadFromPayload(payload);

        var (found, score) = newPr.ProgressBlackboard.TryGet<int>("global_score");
        Assert.True(found);
        Assert.Equal(9001, score);
    }

    // ── Full round-trip: save background, switch, dispose, reload ────────

    [Fact]
    public void SaveAfterSwitch_HasCorrectActiveLevel()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = ctx.SessionManager.CreateBackgroundSession("gen", "game", true);
        bg.SceneHost.SpawnEntity(CreateMeta("GameEntity"));

        ctx.RequestSaveGameAuto();
        ctx.FlushDeferredActionsForCurrentFrame();

        ctx.SessionManager.DestroySession("gen");
        ctx.RequestSwitchForegroundLevel("game");
        ctx.FlushDeferredActionsForCurrentFrame();

        var fg = ctx.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("game", fg.LevelId);

        var saveId = "after_switch";
        ctx.RequestSaveGame(saveId);
        ctx.FlushDeferredActionsForCurrentFrame();

        var payload = ctx.StorageService.ReadSavePayloadFromSnapshot(saveId, "game");
        Assert.Equal("game", payload.ActiveLevelId);
        Assert.True(payload.Levels.ContainsKey("game"));
    }

    [Fact]
    public void SaveSwitchDisposeReload_RestoresToSavedState()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = ctx.SessionManager.CreateBackgroundSession("gen", "game", true);
        bg.SceneHost.SpawnEntity(CreateMeta("GameEntity1"));
        bg.SceneHost.SpawnEntity(CreateMeta("GameEntity2"));
        bg.SessionBlackboard.Set("map_seed", 42);

        ctx.RequestSaveGameAuto();
        ctx.FlushDeferredActionsForCurrentFrame();

        ctx.SessionManager.DestroySession("gen");
        ctx.RequestSwitchForegroundLevel("game");
        ctx.FlushDeferredActionsForCurrentFrame();

        var fgBefore = ctx.SessionManager.ForegroundSession;
        Assert.NotNull(fgBefore);
        Assert.Equal("game", fgBefore.LevelId);

        var saveId = "reload_test";
        ctx.RequestSaveGame(saveId);
        ctx.FlushDeferredActionsForCurrentFrame();

        var progressRun = ctx.EnsureProgressRun();
        progressRun.Dispose();
        ctx.SetProgressRun(null);

        var payload = ctx.StorageService.ReadSavePayloadFromSnapshot(saveId, "game");
        var newPr = TestFactory.CreateProgressRun(
            saveId, ctx.Runtime.Logger, fs, "root", ctx.Runtime, ctx);
        ctx.SetProgressRun(newPr);
        newPr.LoadFromPayload(payload);

        var restoredFg = newPr.ForegroundSession;
        Assert.NotNull(restoredFg);
        Assert.Equal("game", restoredFg.LevelId);
        Assert.Equal(2, restoredFg.SceneHost.GetEntities().Count);

        var (foundSeed, seed) = restoredFg.SessionBlackboard.TryGet<int>("map_seed");
        Assert.True(foundSeed);
        Assert.Equal(42, seed);
    }

    [Fact]
    public void FullRoundTrip_SwitchWithoutSave_OldLevelDataOnDisk()
    {
        var (ctx, fs) = CreateForegroundContext();

        var oldFg = ctx.SessionManager.ForegroundSession;
        Assert.NotNull(oldFg);
        oldFg.SessionBlackboard.Set("old_data", "old_value");
        oldFg.SceneHost.SpawnEntity(CreateMeta("OldEntity"));

        fs.SeedFile("root/current/level_game/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_game/session.json", "{}");
        fs.SeedFile("root/current/level_game/session_state_machines.json",
            "{\"machines\":[]}");

        var progressRun = ctx.EnsureProgressRun();
        progressRun.SwitchForeground("game");

        // Old foreground's level data was explicitly persisted to current/
        Assert.True(fs.Exists("root/current/level_test_level/snd_scene.json"));
        Assert.True(fs.Exists("root/current/level_test_level/session.json"));
        Assert.True(fs.Exists("root/current/level_test_level/session_state_machines.json"));
    }

    // ── Helpers ─────────────────────────────────────────────────────────

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

    private static SessionRun AsSessionRun(ISessionRun session) => (SessionRun)session;

    private static SndMetaData CreateMeta(string name) =>
        new()
        {
            Name = name,
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        };

    private static SndMetaData CreateMetaWithIndex(string name, string index) =>
        new()
        {
            Name = name,
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData { EntityIndices = new List<string> { index } },
            DataMetaData = new DataMetaData()
        };

    // ── Test strategies ─────────────────────────────────────────────────

    [StrategyIndex(BeforeSaveStrategyIndex)]
    private sealed class BeforeSaveSpyStrategy : EntityStrategyBase
    {
        private static ICollection<string>? EventSink { get; set; }

        public static void Bind(ICollection<string> events) => EventSink = events;

        public override void BeforeSave(ISndEntity entity, ISndContext ctx) =>
            EventSink?.Add($"BeforeSave:{entity.Name}");
    }

    [StrategyIndex(BeforeQuitStrategyIndex)]
    private sealed class BeforeQuitSpyStrategy : EntityStrategyBase
    {
        private static ICollection<string>? EventSink { get; set; }

        public static void Bind(ICollection<string> events) => EventSink = events;

        public override void BeforeQuit(ISndEntity entity, ISndContext ctx) =>
            EventSink?.Add($"BeforeQuit:{entity.Name}");
    }
}
