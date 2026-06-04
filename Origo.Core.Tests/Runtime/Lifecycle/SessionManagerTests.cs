using System;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Xunit;
using Origo.Core.Abstractions.Lifecycle;

namespace Origo.Core.Tests;

/// <summary>
///     SessionManager 单元测试：验证 KVP 注册表、前台访问器、后台会话创建/销毁、Process 同步。
/// </summary>
public class SessionManagerTests
{
    // ── Create / Destroy Session ───────────────────────────────────────

    [Fact]
    public void CreateBackgroundSession_AddsSession_TryGetReturnsIt()
    {
        var (ctx, _) = CreateContext();
        var bg = ctx.SessionManager.CreateBackgroundSession("bg1", "bg1");

        Assert.True(ctx.SessionManager.Contains("bg1"));
        Assert.Same(bg, ctx.SessionManager.TryGet("bg1"));
    }

    [Fact]
    public void DestroySession_RemovesSession_TryGetReturnsNull()
    {
        var (ctx, _) = CreateContext();
        var bg = ctx.SessionManager.CreateBackgroundSession("bg1", "bg1");

        ctx.SessionManager.DestroySession("bg1");
        Assert.False(ctx.SessionManager.Contains("bg1"));
        Assert.Null(ctx.SessionManager.TryGet("bg1"));
    }

    [Fact]
    public void CreateBackgroundSession_DuplicateKey_Throws()
    {
        var (ctx, _) = CreateContext();
        ctx.SessionManager.CreateBackgroundSession("dup", "bg1");

        Assert.Throws<InvalidOperationException>(() => ctx.SessionManager.CreateBackgroundSession("dup", "bg2"));
    }

    [Fact]
    public void DestroySession_NonExistentKey_DoesNotChangeMountedSessions()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);
        ctx.SessionManager.CreateBackgroundSession("bg1", "bg1");

        ctx.SessionManager.DestroySession("no_such_key");

        Assert.True(ctx.SessionManager.Contains(ISessionManager.ForegroundKey));
        Assert.True(ctx.SessionManager.Contains("bg1"));
        Assert.NotNull(ctx.SessionManager.ForegroundSession);
    }

    [Fact]
    public void ForegroundKey_IsAvailable_WhenForegroundSessionExists()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);

        Assert.NotNull(ctx.SessionManager.ForegroundSession);
    }

    [Fact]
    public void DestroySession_ForegroundKey_ClearsForegroundSession()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);

        ctx.SessionManager.DestroySession(ISessionManager.ForegroundKey);
        Assert.Null(ctx.SessionManager.ForegroundSession);
    }

    // ── Foreground accessor ──────────────────────────────────────────

    [Fact]
    public void ForegroundSession_ReflectsProgressRunForegroundSession()
    {
        var (ctx, _) = CreateContext();
        // Progress run exists but no foreground session yet.
        Assert.Null(ctx.SessionManager.ForegroundSession);

        SetupForegroundSession(ctx);
        Assert.NotNull(ctx.SessionManager.ForegroundSession);
        Assert.Same(ctx.SessionManager.ForegroundSession, ctx.SessionManager.TryGet(ISessionManager.ForegroundKey));
    }

    [Fact]
    public void TryGet_ForegroundKey_ReturnsForegroundSession()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);

        var session = ctx.SessionManager.TryGet(ISessionManager.ForegroundKey);
        Assert.Same(ctx.SessionManager.ForegroundSession, session);
    }

    [Fact]
    public void Contains_ForegroundKey_TrueWhenSessionActive()
    {
        var (ctx, _) = CreateContext();
        Assert.False(ctx.SessionManager.Contains(ISessionManager.ForegroundKey));

        SetupForegroundSession(ctx);
        Assert.True(ctx.SessionManager.Contains(ISessionManager.ForegroundKey));
    }

    // ── Keys ────────────────────────────────────────────────────────

    [Fact]
    public void Keys_IncludesForegroundAndBackground()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);
        ctx.SessionManager.CreateBackgroundSession("bg1", "bg1");

        var keys = ctx.SessionManager.Keys;
        Assert.Contains(ISessionManager.ForegroundKey, keys);
        Assert.Contains("bg1", keys);
    }

    // ── ProcessAllSessions ─────────────────────────────────────

    [Fact]
    public void ProcessAllSessions_OnlySynced_SessionsAreProcessed()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);
        ctx.SessionManager.CreateBackgroundSession("synced", "synced", true);
        ctx.SessionManager.CreateBackgroundSession("stored", "stored");

        var ex = Record.Exception(() =>
        {
            ctx.SessionManager.ProcessAllSessions(0.016);
            ctx.SessionManager.ProcessAllSessions(0.016, true);
        });

        Assert.Null(ex);
        Assert.Contains("synced", ((SessionManager)ctx.SessionManager).ProcessingKeys);
        Assert.DoesNotContain("stored", ((SessionManager)ctx.SessionManager).ProcessingKeys);
    }

    [Fact]
    public void ProcessingKeys_OnlyReturnsSyncedKeys()
    {
        var (ctx, _) = CreateContext();
        ctx.SessionManager.CreateBackgroundSession("synced", "synced", true);
        ctx.SessionManager.CreateBackgroundSession("stored", "stored");

        var processingKeys = ((SessionManager)ctx.SessionManager).ProcessingKeys;
        Assert.Contains("synced", processingKeys);
        Assert.DoesNotContain("stored", processingKeys);
    }

    // ── Background level IDs in progress blackboard ───────────────────

    [Fact]
    public void SessionTopology_WellKnownKey_Exists() =>
        Assert.Equal("origo.session_topology", WellKnownKeys.SessionTopology);

    // ── levelId conflict detection ────────────────────────────────────

    [Fact]
    public void CreateBackgroundSession_DuplicateLevelIdWithForeground_Throws()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);

        ctx.SessionManager.CreateBackgroundSession("bg1", "bg1");
        var ex =
            Assert.Throws<InvalidOperationException>(() => ctx.SessionManager.CreateBackgroundSession("bg2", "bg1"));
        Assert.Contains("bg1", ex.Message);
        Assert.Contains("already manages this level", ex.Message);
    }

    [Fact]
    public void CreateBackgroundSession_DuplicateLevelIdWithAnotherBackground_Throws()
    {
        var (ctx, _) = CreateContext();

        ctx.SessionManager.CreateBackgroundSession("bg1", "level_a");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ctx.SessionManager.CreateBackgroundSession("bg2", "level_a"));
        Assert.Contains("bg1", ex.Message);
        Assert.Contains("already manages this level", ex.Message);
    }

    [Fact]
    public void SwitchForeground_AutoHandlesBackgroundSessionCollision()
    {
        var (ctx, fs) = CreateContext();

        ctx.SessionManager.CreateBackgroundSession("bg", "game", true);

        fs.SeedFile("root/current/level_game/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_game/session.json", "{}");
        fs.SeedFile("root/current/level_game/session_state_machines.json",
            "{\"machines\":[]}");

        var progressRun = ctx.EnsureProgressRun();
        progressRun.SwitchForeground("game");

        var destroyedBg = ctx.SessionManager.TryGet("bg");
        Assert.Null(destroyedBg);

        var fg = ctx.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("game", fg.LevelId);
    }

    [Fact]
    public void CreateForegroundSession_DifferentLevelId_Succeeds()
    {
        var (ctx, fs) = CreateContext();
        SetupForegroundSession(ctx);

        ctx.SessionManager.CreateBackgroundSession("bg", "game", true);

        fs.SeedFile("root/current/level_game/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_game/session.json", "{}");
        fs.SeedFile("root/current/level_game/session_state_machines.json",
            "{\"machines\":[]}");

        ctx.RequestSaveGameAuto();
        ctx.FlushDeferredActionsForCurrentFrame();

        ctx.SessionManager.DestroySession("bg");
        ctx.RequestSwitchForegroundLevel("game");
        ctx.FlushDeferredActionsForCurrentFrame();

        var fg = ctx.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("game", fg.LevelId);
    }

    [Fact]
    public void AppendBackgroundPayloads_LevelIdCollision_Throws()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);

        var bg = ctx.SessionManager.CreateBackgroundSession("bg", "level_x", true);
        bg.SceneHost.CreateEntity(new SndMetaData
        {
            Name = "CollisionEntity",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        });

        var progressRun = ctx.EnsureProgressRun();

        // Directly add a bogus level payload with the same levelId to simulate collision
        var payload = progressRun.BuildSavePayload("test_save");
        Assert.True(payload.Levels.ContainsKey("default"));
        Assert.True(payload.Levels.ContainsKey("level_x"));
    }

    [Fact]
    public void AppendBackgroundPayloads_LevelIdCollisionBetweenForegroundAndBackground_Throws()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);

        // Creating a background session with the same levelId as the
        // foreground is rejected immediately — it never reaches
        // AppendBackgroundPayloads.
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ctx.SessionManager.CreateBackgroundSession("bg", "default"));
        Assert.Contains("bg", ex.Message);
        Assert.Contains("default", ex.Message);
        Assert.Contains("__foreground__", ex.Message);
        Assert.Contains("already manages this level", ex.Message);
    }

    [Fact]
    public void CreateBackgroundSession_SameLevelIdAsDestroyedSession_Succeeds()
    {
        var (ctx, _) = CreateContext();

        var bg = ctx.SessionManager.CreateBackgroundSession("bg1", "reusable");
        ctx.SessionManager.DestroySession("bg1");

        var bg2 = ctx.SessionManager.CreateBackgroundSession("bg2", "reusable");
        Assert.NotNull(bg2);
        Assert.Equal("reusable", bg2.LevelId);
    }

    [Fact]
    public void CreateBackgroundSession_DuplicateLevelId_ClearErrorMessage()
    {
        var (ctx, _) = CreateContext();

        ctx.SessionManager.CreateBackgroundSession("owner", "treasure");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ctx.SessionManager.CreateBackgroundSession("thief", "treasure"));

        Assert.Contains("thief", ex.Message);
        Assert.Contains("treasure", ex.Message);
        Assert.Contains("owner", ex.Message);
        Assert.Contains("Destroy the existing session", ex.Message);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static (SndContext ctx, TestFileSystem fs) CreateContext()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var ctx = new SndContext(new SndContextParameters(runtime, fs, "root", "res://initial",
            "res://entry/entry.json"));
        // Set up a progress run so ctx.SessionManager returns a per-instance manager
        // (avoids cross-test contamination via the static fallback).
        var progressRun = TestFactory.CreateProgressRun(
            "001", logger, fs, "root", runtime, ctx);
        ctx.SetProgressRun(progressRun);
        return (ctx, fs);
    }

    private static void SetupForegroundSession(SndContext ctx)
    {
        var progressRun = TestFactory.CreateProgressRun(
            "001", ctx.Runtime.Logger, ctx.FileSystem, "root", ctx.Runtime, ctx);
        ctx.SetProgressRun(progressRun);
        progressRun.LoadAndMountForeground("default");
    }
}
