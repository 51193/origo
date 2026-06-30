using System;
using Origo.Core.DataSource;
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
        var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg1", "bg1");

        Assert.True(ctx.Runtime.SessionManager.Contains("bg1"));
        Assert.Same(bg, ctx.Runtime.SessionManager.TryGet("bg1"));
    }

    [Fact]
    public void DestroySession_RemovesSession_TryGetReturnsNull()
    {
        var (ctx, _) = CreateContext();
        var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg1", "bg1");

        ctx.Runtime.SessionManager.DestroySession("bg1");
        Assert.False(ctx.Runtime.SessionManager.Contains("bg1"));
        Assert.Null(ctx.Runtime.SessionManager.TryGet("bg1"));
    }

    [Fact]
    public void CreateBackgroundSession_DuplicateKey_Throws()
    {
        var (ctx, _) = CreateContext();
        ctx.Runtime.SessionManager.CreateBackgroundSession("dup", "bg1");

        Assert.Throws<InvalidOperationException>(() => ctx.Runtime.SessionManager.CreateBackgroundSession("dup", "bg2"));
    }

    [Fact]
    public void DestroySession_NonExistentKey_DoesNotChangeMountedSessions()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);
        ctx.Runtime.SessionManager.CreateBackgroundSession("bg1", "bg1");

        ctx.Runtime.SessionManager.DestroySession("no_such_key");

        Assert.True(ctx.Runtime.SessionManager.Contains(ISessionManager.ForegroundKey));
        Assert.True(ctx.Runtime.SessionManager.Contains("bg1"));
        Assert.NotNull(ctx.Runtime.SessionManager.ForegroundSession);
    }

    [Fact]
    public void ForegroundKey_IsAvailable_WhenForegroundSessionExists()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);

        Assert.NotNull(ctx.Runtime.SessionManager.ForegroundSession);
    }

    [Fact]
    public void DestroySession_ForegroundKey_ClearsForegroundSession()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);

        ctx.Runtime.SessionManager.DestroySession(ISessionManager.ForegroundKey);
        Assert.Null(ctx.Runtime.SessionManager.ForegroundSession);
    }

    // ── Foreground accessor ──────────────────────────────────────────

    [Fact]
    public void ForegroundSession_ReflectsProgressRunForegroundSession()
    {
        var (ctx, _) = CreateContext();
        // Progress run exists but no foreground session yet.
        Assert.Null(ctx.Runtime.SessionManager.ForegroundSession);

        SetupForegroundSession(ctx);
        Assert.NotNull(ctx.Runtime.SessionManager.ForegroundSession);
        Assert.Same(ctx.Runtime.SessionManager.ForegroundSession, ctx.Runtime.SessionManager.TryGet(ISessionManager.ForegroundKey));
    }

    [Fact]
    public void TryGet_ForegroundKey_ReturnsForegroundSession()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);

        var session = ctx.Runtime.SessionManager.TryGet(ISessionManager.ForegroundKey);
        Assert.Same(ctx.Runtime.SessionManager.ForegroundSession, session);
    }

    [Fact]
    public void Contains_ForegroundKey_TrueWhenSessionActive()
    {
        var (ctx, _) = CreateContext();
        Assert.False(ctx.Runtime.SessionManager.Contains(ISessionManager.ForegroundKey));

        SetupForegroundSession(ctx);
        Assert.True(ctx.Runtime.SessionManager.Contains(ISessionManager.ForegroundKey));
    }

    // ── Keys ────────────────────────────────────────────────────────

    [Fact]
    public void Keys_IncludesForegroundAndBackground()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);
        ctx.Runtime.SessionManager.CreateBackgroundSession("bg1", "bg1");

        var keys = ctx.Runtime.SessionManager.Keys;
        Assert.Contains(ISessionManager.ForegroundKey, keys);
        Assert.Contains("bg1", keys);
    }

    // ── ProcessAllSessions ─────────────────────────────────────

    [Fact]
    public void ProcessAllSessions_OnlySynced_SessionsAreProcessed()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);
        ctx.Runtime.SessionManager.CreateBackgroundSession("synced", "synced", true);
        ctx.Runtime.SessionManager.CreateBackgroundSession("stored", "stored");

        var ex = Record.Exception(() =>
        {
            ctx.Runtime.SessionManager.ProcessAllSessions(0.016);
            ctx.Runtime.SessionManager.ProcessAllSessions(0.016, true);
        });

        Assert.Null(ex);
        Assert.Contains("synced", ((SessionManager)ctx.Runtime.SessionManager).ProcessingKeys);
        Assert.DoesNotContain("stored", ((SessionManager)ctx.Runtime.SessionManager).ProcessingKeys);
    }

    [Fact]
    public void ProcessingKeys_OnlyReturnsSyncedKeys()
    {
        var (ctx, _) = CreateContext();
        ctx.Runtime.SessionManager.CreateBackgroundSession("synced", "synced", true);
        ctx.Runtime.SessionManager.CreateBackgroundSession("stored", "stored");

        var processingKeys = ((SessionManager)ctx.Runtime.SessionManager).ProcessingKeys;
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

        ctx.Runtime.SessionManager.CreateBackgroundSession("bg1", "bg1");
        var ex =
            Assert.Throws<InvalidOperationException>(() => ctx.Runtime.SessionManager.CreateBackgroundSession("bg2", "bg1"));
        Assert.Contains("bg1", ex.Message);
        Assert.Contains("already manages this level", ex.Message);
    }

    [Fact]
    public void CreateBackgroundSession_DuplicateLevelIdWithAnotherBackground_Throws()
    {
        var (ctx, _) = CreateContext();

        ctx.Runtime.SessionManager.CreateBackgroundSession("bg1", "level_a");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ctx.Runtime.SessionManager.CreateBackgroundSession("bg2", "level_a"));
        Assert.Contains("bg1", ex.Message);
        Assert.Contains("already manages this level", ex.Message);
    }

    [Fact]
    public void SwitchForeground_AutoHandlesBackgroundSessionCollision()
    {
        var (ctx, fs) = CreateContext();

        ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "game", true);

        fs.SeedFile("root/current/level_game/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_game/session.json", "{}");
        fs.SeedFile("root/current/level_game/session_state_machines.json",
            "{\"machines\":[]}");

        var progressRun = ctx.EnsureProgressRun();
        progressRun.SwitchForeground("game");

        var destroyedBg = ctx.Runtime.SessionManager.TryGet("bg");
        Assert.Null(destroyedBg);

        var fg = ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("game", fg.LevelId);
    }

    [Fact]
    public void CreateForegroundSession_DifferentLevelId_Succeeds()
    {
        var (ctx, fs) = CreateContext();
        SetupForegroundSession(ctx);

        ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "game", true);

        fs.SeedFile("root/current/level_game/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_game/session.json", "{}");
        fs.SeedFile("root/current/level_game/session_state_machines.json",
            "{\"machines\":[]}");

        ctx.RequestSaveGameAuto();
        ctx.FlushDeferredActionsForCurrentFrame();

        ctx.Runtime.SessionManager.DestroySession("bg");
        ctx.RequestSwitchForegroundLevel("game");
        ctx.FlushDeferredActionsForCurrentFrame();

        var fg = ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("game", fg.LevelId);
    }

    [Fact]
    public void AppendBackgroundPayloads_DifferentLevelIds_IncludesBothInPayload()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);

        var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "level_x", true);
        ((SessionRun)bg).SceneHost.CreateEntity(new SndMetaData
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
            ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "default"));
        Assert.Contains("bg", ex.Message);
        Assert.Contains("default", ex.Message);
        Assert.Contains("__foreground__", ex.Message);
        Assert.Contains("already manages this level", ex.Message);
    }

    [Fact]
    public void CreateBackgroundSession_SameLevelIdAsDestroyedSession_Succeeds()
    {
        var (ctx, _) = CreateContext();

        var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg1", "reusable");
        ctx.Runtime.SessionManager.DestroySession("bg1");

        var bg2 = ctx.Runtime.SessionManager.CreateBackgroundSession("bg2", "reusable");
        Assert.NotNull(bg2);
        Assert.Equal("reusable", bg2.LevelId);
    }

    [Fact]
    public void CreateBackgroundSession_DuplicateLevelId_ClearErrorMessage()
    {
        var (ctx, _) = CreateContext();

        ctx.Runtime.SessionManager.CreateBackgroundSession("owner", "treasure");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ctx.Runtime.SessionManager.CreateBackgroundSession("thief", "treasure"));

        Assert.Contains("thief", ex.Message);
        Assert.Contains("treasure", ex.Message);
        Assert.Contains("owner", ex.Message);
        Assert.Contains("Destroy the existing session", ex.Message);
    }

    // ── Entity operations on ISessionRun ────────────────────────────────

    [Fact]
    public void SessionRun_Spawn_CreatesEntity()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);

        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        var entity = session.Spawn(new SndMetaData
        {
            Name = "spawned",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        });

        Assert.Equal("spawned", entity.Name);
        Assert.Same(entity, session.FindByName("spawned"));
    }

    [Fact]
    public void SessionRun_SpawnMany_CreatesMultipleEntities()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);

        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        session.SpawnMany(new[]
        {
            new SndMetaData
            {
                Name = "a1", NodeMetaData = new NodeMetaData(),
                StrategyMetaData = new StrategyMetaData(), DataMetaData = new DataMetaData()
            },
            new SndMetaData
            {
                Name = "a2", NodeMetaData = new NodeMetaData(),
                StrategyMetaData = new StrategyMetaData(), DataMetaData = new DataMetaData()
            }
        });

        Assert.Equal(2, session.GetEntities().Count);
        Assert.NotNull(session.FindByName("a1"));
        Assert.NotNull(session.FindByName("a2"));
    }

    [Fact]
    public void SessionRun_RequestKillEntity_MarksEntityPending()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);

        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        var entity = session.Spawn(new SndMetaData
        {
            Name = "to_kill",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        });

        Assert.False(entity.IsPendingKill);

        session.RequestKillEntity("to_kill");
        Assert.True(entity.IsPendingKill);
    }

    [Fact]
    public void KillPendingAllSessions_ProcessesForegroundPendingKill()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);

        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        session.Spawn(new SndMetaData
        {
            Name = "doomed",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        });

        session.RequestKillEntity("doomed");
        ctx.Runtime.SessionManager.KillPendingAllSessions();

        Assert.Null(session.FindByName("doomed"));
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static (SndContext ctx, TestFileSystem fs) CreateContext()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial",
            "res://entry/entry.json"));
        var progressRun = TestFactory.CreateProgressRun(
            "001", logger, metaAccess, pathResolver, "root", runtime, ctx, sharedDataSourceIo: dataSourceIo);
        ctx.SetProgressRun(progressRun);
        return (ctx, fs);
    }

    private static void SetupForegroundSession(SndContext ctx)
    {
        var progressRun = TestFactory.CreateProgressRun(
            "001", ctx.Runtime.Logger, ctx.MetaAccess, ctx.PathResolver, "root", ctx.Runtime, ctx, sharedDataSourceIo: ctx.DataSourceIo);
        ctx.SetProgressRun(progressRun);
        progressRun.LoadAndMountForeground("default");
    }
}
