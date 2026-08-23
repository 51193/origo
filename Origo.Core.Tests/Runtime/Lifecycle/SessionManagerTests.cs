using System;
using System.Collections.Generic;
using System.Threading;
using Origo.Core.Abstractions.Entity;
using Origo.Core.DataSource;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
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
        _ = ctx.Runtime.SessionManager.CreateBackgroundSession("bg1", "bg1");

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
    public void CreateBackgroundSession_ReservedForegroundKey_Throws()
    {
        var (ctx, _) = CreateContext();

        // The foreground key is a reserved slot managed exclusively by the
        // framework's foreground mount paths; a background session must not
        // be able to occupy (or destroy) it.
        Assert.Throws<InvalidOperationException>(() =>
            ctx.Runtime.SessionManager.CreateBackgroundSession(ISessionManager.ForegroundKey, "bg1"));
        Assert.False(ctx.Runtime.SessionManager.Contains(ISessionManager.ForegroundKey));
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
    public void ProcessAllSessions_OnlyProcessesSyncedSessions()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);

        ctx.Runtime.SndWorld.RegisterStrategy(() => new ProcessSpyStrategy());
        ProcessSpyStrategy.ProcessCalls.Clear();

        var synced = ctx.Runtime.SessionManager.CreateBackgroundSession("synced", "synced", true);
        var stored = ctx.Runtime.SessionManager.CreateBackgroundSession("stored", "stored");

        var meta = new SndMetaData
        {
            Name = "entity_synced",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData
            {
                LifecycleIndices = [_processSpyIdx]
            },
            DataMetaData = new DataMetaData()
        };
        synced.Spawn(meta);

        var meta2 = new SndMetaData
        {
            Name = "entity_stored",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData
            {
                LifecycleIndices = [_processSpyIdx]
            },
            DataMetaData = new DataMetaData()
        };
        stored.Spawn(meta2);

        ProcessSpyStrategy.ProcessCalls.Clear();
        ctx.Runtime.SessionManager.ProcessAllSessions(0.016);

        Assert.Contains("entity_synced", ProcessSpyStrategy.ProcessCalls);
        Assert.DoesNotContain("entity_stored", ProcessSpyStrategy.ProcessCalls);
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

        ctx.Save.RequestSwitchForegroundLevel("game");
        ctx.FlushFrame();

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

        ctx.Save.RequestSaveGameAuto();
        ctx.FlushFrame();

        ctx.Runtime.SessionManager.DestroySession("bg");
        ctx.Save.RequestSwitchForegroundLevel("game");
        ctx.FlushFrame();

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
        bg.Spawn(new SndMetaData
        {
            Name = "CollisionEntity",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        });

        ctx.Save.RequestSaveGame("test_save");
        ctx.FlushFrame();

        var payload = ctx.StorageService.ReadSavePayloadFromSnapshot("test_save", "default");
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

        _ = ctx.Runtime.SessionManager.CreateBackgroundSession("bg1", "reusable");
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
        session.SpawnMany(
        [
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
        ]);

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

    // ── Edge cases: empty/null/whitespace keys ──────────────────────────

    [Fact]
    public void TryGet_EmptyKey_ReturnsNull()
    {
        var (ctx, _) = CreateContext();
        Assert.Null(ctx.Runtime.SessionManager.TryGet(""));
    }

    [Fact]
    public void TryGet_WhitespaceKey_ReturnsNull()
    {
        var (ctx, _) = CreateContext();
        Assert.Null(ctx.Runtime.SessionManager.TryGet("   "));
    }

    [Fact]
    public void Contains_EmptyKey_ReturnsFalse()
    {
        var (ctx, _) = CreateContext();
        Assert.False(ctx.Runtime.SessionManager.Contains(""));
    }

    [Fact]
    public void DestroySession_EmptyKey_DoesNotThrow()
    {
        var (ctx, _) = CreateContext();
        var ex = Record.Exception(() => ctx.Runtime.SessionManager.DestroySession(""));
        Assert.Null(ex);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static (SndContext ctx, TestMemoryFileSystem fs) CreateContext()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("res://entry/entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
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

    private const string _processSpyIdx = "test.smgr.process_spy";

    [StrategyIndex(_processSpyIdx)]
    private sealed class ProcessSpyStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<List<string>> _processCalls = new();
        public static List<string> ProcessCalls => _processCalls.Value ??= [];

        public override void Process(ISndEntity entity, double delta, ISndContext ctx) =>
            ProcessCalls.Add(entity.Name);
    }
}
