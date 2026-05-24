using System;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save;
using Origo.Core.Snd;
using Xunit;

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
