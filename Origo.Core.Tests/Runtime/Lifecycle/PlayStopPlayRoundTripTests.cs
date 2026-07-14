using System.Collections.Generic;
using System.Threading;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Snd;
using Xunit;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.DataSource;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;

namespace Origo.Core.Tests;

/// <summary>
///     Play-Stop-Play 完整往返测试：
///     验证 存档 → Dispose → 新进程（共享文件系统）→ 读档 后，
///     前台 SessionRun 身份保留、Tick 状态保留、各 SessionBlackBoard 数据互不污染。
///     <para>
///         往返通过公共 <see cref="ISndContext" /> 存读档流程完成：ctx1 存档到共享
///         文件系统，ctx2（模拟重启）从同一文件系统读档。会话的 syncProcess 状态
///         通过 <see cref="ISessionManager.ProcessAllSessions" /> 是否处理该会话的
///         探针实体来间接验证，而非直接读取内部属性。
///     </para>
/// </summary>
[Collection("StrategyStateTests")]
public class PlayStopPlayRoundTripTests
{
    // ── Full round-trip: save → dispose → recreate → reload ──

    [Fact]
    public void RoundTrip_ForegroundIdentity_Preserved()
    {
        // ── PLAY 1 ──────────────────────────────────────────────────────
        var (ctx1, fs) = CreateContext();
        var pr1 = SetupProgressRun(ctx1);
        pr1.LoadAndMountForeground("level_a");

        var fg1 = ctx1.Runtime.SessionManager.ForegroundSession!;
        Assert.True(fg1.IsFrontSession);
        Assert.Equal("level_a", fg1.LevelId);

        ctx1.Save.RequestSaveGame("save-001");
        ctx1.Deferred.FlushDeferredActionsForCurrentFrame();

        // ── STOP ────────────────────────────────────────────────────────
        pr1.Dispose();

        // ── PLAY 2 (shared file system simulates a restart) ─────────────
        var (ctx2, _) = CreateContext(fs);
        ctx2.Save.RequestLoadGame("save-001");
        ctx2.Deferred.FlushDeferredActionsForCurrentFrame();

        var fg2 = ctx2.Runtime.SessionManager.ForegroundSession!;
        Assert.True(fg2.IsFrontSession, "Foreground identity must be restored after round-trip.");
        Assert.Equal("level_a", fg2.LevelId);
    }

    [Fact]
    public void RoundTrip_BackgroundTickState_Preserved()
    {
        // ── PLAY 1 ──────────────────────────────────────────────────────
        var (ctx1, fs) = CreateContext();
        var pr1 = SetupProgressRun(ctx1);
        pr1.LoadAndMountForeground("level_a");

        // Create background sessions: one with syncProcess=true, one with syncProcess=false.
        var bgTick = ctx1.Runtime.SessionManager.CreateBackgroundSession("bg_tick", "bg_level_tick", true);
        var bgStore = ctx1.Runtime.SessionManager.CreateBackgroundSession("bg_store", "bg_level_store");
        bgTick.Spawn(TickProbeMeta());
        bgStore.Spawn(TickProbeMeta());

        // Only the synced background session is processed.
        TickProbeStrategy.Reset();
        ctx1.Runtime.SessionManager.ProcessAllSessions(0.016, includeForeground: false);
        Assert.Contains("bg_level_tick", TickProbeStrategy.Ticked);
        Assert.DoesNotContain("bg_level_store", TickProbeStrategy.Ticked);

        ctx1.Save.RequestSaveGame("save-002");
        ctx1.Deferred.FlushDeferredActionsForCurrentFrame();

        // ── STOP ────────────────────────────────────────────────────────
        pr1.Dispose();

        // ── PLAY 2 ──────────────────────────────────────────────────────
        var (ctx2, _) = CreateContext(fs);
        ctx2.Save.RequestLoadGame("save-002");
        ctx2.Deferred.FlushDeferredActionsForCurrentFrame();

        Assert.NotNull(ctx2.Runtime.SessionManager.TryGet("bg_tick"));
        Assert.NotNull(ctx2.Runtime.SessionManager.TryGet("bg_store"));

        // syncProcess is preserved across the round-trip: only bg_tick is processed.
        TickProbeStrategy.Reset();
        ctx2.Runtime.SessionManager.ProcessAllSessions(0.016, includeForeground: false);
        Assert.Contains("bg_level_tick", TickProbeStrategy.Ticked);
        Assert.DoesNotContain("bg_level_store", TickProbeStrategy.Ticked);
    }

    [Fact]
    public void RoundTrip_SessionBlackboards_Isolated_NoCrossContamination()
    {
        // ── PLAY 1 ──────────────────────────────────────────────────────
        var (ctx1, fs) = CreateContext();
        var pr1 = SetupProgressRun(ctx1);
        pr1.LoadAndMountForeground("level_a");

        var fg1 = ctx1.Runtime.SessionManager.ForegroundSession!;
        fg1.SessionBlackboard.SetValue("marker", "fg_data_42");
        fg1.SessionBlackboard.SetValue("fg_only", 100);

        var bg1 = ctx1.Runtime.SessionManager.CreateBackgroundSession("bg1", "bg_level");
        bg1.SessionBlackboard.SetValue("marker", "bg_data_99");
        bg1.SessionBlackboard.SetValue("bg_only", 200);

        // Verify isolation before serialize.
        Assert.False(fg1.SessionBlackboard.TryGet<int>("bg_only").found);
        Assert.False(bg1.SessionBlackboard.TryGet<int>("fg_only").found);

        ctx1.Save.RequestSaveGame("save-003");
        ctx1.Deferred.FlushDeferredActionsForCurrentFrame();

        // ── STOP ────────────────────────────────────────────────────────
        pr1.Dispose();

        // ── PLAY 2 ──────────────────────────────────────────────────────
        var (ctx2, _) = CreateContext(fs);
        ctx2.Save.RequestLoadGame("save-003");
        ctx2.Deferred.FlushDeferredActionsForCurrentFrame();

        var fg2 = ctx2.Runtime.SessionManager.ForegroundSession!;
        var bg2 = ctx2.Runtime.SessionManager.TryGet("bg1")!;

        // Foreground data restored.
        var (fgFound, fgMarker) = fg2.SessionBlackboard.TryGet<string>("marker");
        Assert.True(fgFound);
        Assert.Equal("fg_data_42", fgMarker);
        var (fgOnlyFound, fgOnlyVal) = fg2.SessionBlackboard.TryGet<int>("fg_only");
        Assert.True(fgOnlyFound);
        Assert.Equal(100, fgOnlyVal);

        // Background data restored.
        var (bgFound, bgMarker) = bg2.SessionBlackboard.TryGet<string>("marker");
        Assert.True(bgFound);
        Assert.Equal("bg_data_99", bgMarker);
        var (bgOnlyFound, bgOnlyVal) = bg2.SessionBlackboard.TryGet<int>("bg_only");
        Assert.True(bgOnlyFound);
        Assert.Equal(200, bgOnlyVal);

        // Blackboards remain isolated.
        Assert.False(fg2.SessionBlackboard.TryGet<int>("bg_only").found,
            "Foreground blackboard must not contain background-only data.");
        Assert.False(bg2.SessionBlackboard.TryGet<int>("fg_only").found,
            "Background blackboard must not contain foreground-only data.");
    }

    [Fact]
    public void RoundTrip_ProgressBlackboard_Shared_AcrossSessions()
    {
        // ── PLAY 1 ──────────────────────────────────────────────────────
        var (ctx1, fs) = CreateContext();
        var pr1 = SetupProgressRun(ctx1);
        pr1.LoadAndMountForeground("level_a");
        pr1.ProgressBlackboard.SetValue("global_flag", "hello_world");

        ctx1.Runtime.SessionManager.CreateBackgroundSession("bg1", "bg_level");

        ctx1.Save.RequestSaveGame("save-004");
        ctx1.Deferred.FlushDeferredActionsForCurrentFrame();

        // ── STOP ────────────────────────────────────────────────────────
        pr1.Dispose();

        // ── PLAY 2 ──────────────────────────────────────────────────────
        var (ctx2, _) = CreateContext(fs);
        ctx2.Save.RequestLoadGame("save-004");
        ctx2.Deferred.FlushDeferredActionsForCurrentFrame();

        // ProgressBlackboard data is restored and shared.
        var (found, val) = ctx2.Blackboard.ProgressBlackboard!.TryGet<string>("global_flag");
        Assert.True(found);
        Assert.Equal("hello_world", val);
    }

    [Fact]
    public void RoundTrip_AllSessionProperties_Restored_Correctly()
    {
        // ── PLAY 1: Create complex topology ─────────────────────────────
        var (ctx1, fs) = CreateContext();
        var pr1 = SetupProgressRun(ctx1);
        pr1.LoadAndMountForeground("main_level");

        var fg1 = ctx1.Runtime.SessionManager.ForegroundSession!;
        fg1.SessionBlackboard.SetValue("score", 42);

        // Tickable background.
        var bgTick = ctx1.Runtime.SessionManager.CreateBackgroundSession("sim", "sim_level", true);
        bgTick.SessionBlackboard.SetValue("step", 7);
        bgTick.Spawn(TickProbeMeta());

        // Non-tickable background.
        var bgStore = ctx1.Runtime.SessionManager.CreateBackgroundSession("cache", "cache_level");
        bgStore.SessionBlackboard.SetValue("cached", true);
        bgStore.Spawn(TickProbeMeta());

        // Verify state before serialization.
        Assert.True(fg1.IsFrontSession);
        Assert.False(bgTick.IsFrontSession);
        Assert.False(bgStore.IsFrontSession);
        TickProbeStrategy.Reset();
        ctx1.Runtime.SessionManager.ProcessAllSessions(0.016, includeForeground: false);
        Assert.Contains("sim_level", TickProbeStrategy.Ticked);
        Assert.DoesNotContain("cache_level", TickProbeStrategy.Ticked);

        ctx1.Save.RequestSaveGame("save-005");
        ctx1.Deferred.FlushDeferredActionsForCurrentFrame();

        // ── STOP ────────────────────────────────────────────────────────
        pr1.Dispose();

        // ── PLAY 2: Full restore ────────────────────────────────────────
        var (ctx2, _) = CreateContext(fs);
        ctx2.Save.RequestLoadGame("save-005");
        ctx2.Deferred.FlushDeferredActionsForCurrentFrame();

        // Foreground identity & data.
        var fg2 = ctx2.Runtime.SessionManager.ForegroundSession!;
        Assert.True(fg2.IsFrontSession, "Foreground identity must be restored.");
        Assert.Equal("main_level", fg2.LevelId);
        Assert.Equal(42, fg2.SessionBlackboard.TryGet<int>("score").value);

        // Tickable background: restored with syncProcess=true.
        var sim2 = ctx2.Runtime.SessionManager.TryGet("sim")!;
        Assert.False(sim2.IsFrontSession);
        Assert.Equal("sim_level", sim2.LevelId);
        Assert.Equal(7, sim2.SessionBlackboard.TryGet<int>("step").value);

        // Non-tickable background: restored with syncProcess=false.
        var cache2 = ctx2.Runtime.SessionManager.TryGet("cache")!;
        Assert.False(cache2.IsFrontSession);
        Assert.Equal("cache_level", cache2.LevelId);
        Assert.True(cache2.SessionBlackboard.TryGet<bool>("cached").value);

        // syncProcess preserved: only sim is processed after restore.
        TickProbeStrategy.Reset();
        ctx2.Runtime.SessionManager.ProcessAllSessions(0.016, includeForeground: false);
        Assert.Contains("sim_level", TickProbeStrategy.Ticked);
        Assert.DoesNotContain("cache_level", TickProbeStrategy.Ticked);

        // Cross-contamination check.
        Assert.False(fg2.SessionBlackboard.TryGet<int>("step").found);
        Assert.False(fg2.SessionBlackboard.TryGet<bool>("cached").found);
        Assert.False(sim2.SessionBlackboard.TryGet<int>("score").found);
        Assert.False(sim2.SessionBlackboard.TryGet<bool>("cached").found);
        Assert.False(cache2.SessionBlackboard.TryGet<int>("score").found);
        Assert.False(cache2.SessionBlackboard.TryGet<int>("step").found);
    }

    // ── Verify ProgressRun starts clean — no auto-restore from blackboard ──

    [Fact]
    public void NewProgressRun_AlwaysStartsWithEmptyBlackboard()
    {
        // ProgressRun always creates its own blank blackboard internally.
        // No external blackboard injection is supported.
        var progressRun = TestFactory.CreateProgressRun(
            "001", new TestLogger(),
            DataSourceFactory.CreateFileMetaAccess(new TestFileSystem()),
            DataSourceFactory.CreatePathResolver(new TestFileSystem()),
            "root",
            TestFactory.CreateRuntime(),
            new SndContext(new SndContextParameters(TestFactory.CreateRuntime(),
                DataSourceFactory.CreateDefaultIoGateway(new TestFileSystem()),
                DataSourceFactory.CreateFileMetaAccess(new TestFileSystem()),
                DataSourceFactory.CreatePathResolver(new TestFileSystem()),
                "root",
                "initial", "entry.json")));

        Assert.Null(progressRun.SessionManager.ForegroundSession);
        Assert.Empty(progressRun.SessionManager.Keys);
        Assert.Empty(progressRun.ProgressBlackboard.GetKeys());

        progressRun.Dispose();
    }

    [Fact]
    public void LoadFromPayload_FullyRestoresFromPayloadOnly()
    {
        // ── PLAY 1: Create state ────────────────────────────────────────
        var (ctx1, fs) = CreateContext();
        var pr1 = SetupProgressRun(ctx1);
        pr1.LoadAndMountForeground("level_x");
        pr1.ProgressBlackboard.SetValue("user_data", "important");

        var bgSim = ctx1.Runtime.SessionManager.CreateBackgroundSession("bg_sim", "sim_level", true);
        bgSim.Spawn(TickProbeMeta());

        ctx1.Save.RequestSaveGame("save-rt");
        ctx1.Deferred.FlushDeferredActionsForCurrentFrame();

        // ── STOP ────────────────────────────────────────────────────────
        pr1.Dispose();

        // ── PLAY 2: Restore from a fresh context ────────────────────────
        var (ctx2, _) = CreateContext(fs);

        // Before load: completely empty.
        Assert.Null(ctx2.Runtime.SessionManager.ForegroundSession);
        Assert.Empty(ctx2.Runtime.SessionManager.Keys);

        ctx2.Save.RequestLoadGame("save-rt");
        ctx2.Deferred.FlushDeferredActionsForCurrentFrame();

        // After load: everything is restored from storage.
        Assert.NotNull(ctx2.Runtime.SessionManager.ForegroundSession);
        Assert.True(ctx2.Runtime.SessionManager.ForegroundSession!.IsFrontSession);
        Assert.Equal("level_x", ctx2.Runtime.SessionManager.ForegroundSession!.LevelId);

        var (found, val) = ctx2.Blackboard.ProgressBlackboard!.TryGet<string>("user_data");
        Assert.True(found);
        Assert.Equal("important", val);

        Assert.NotNull(ctx2.Runtime.SessionManager.TryGet("bg_sim"));
        TickProbeStrategy.Reset();
        ctx2.Runtime.SessionManager.ProcessAllSessions(0.016, includeForeground: false);
        Assert.Contains("sim_level", TickProbeStrategy.Ticked);
    }

    [Fact]
    public void LoadFromPayload_CanBeCalledMultipleTimes()
    {
        // Verify that loading twice cleanly replaces all state (no residual
        // data from the previous load).
        var (ctx, fs) = CreateContext();
        var pr = SetupProgressRun(ctx);
        pr.LoadAndMountForeground("first_level");
        pr.ProgressBlackboard.SetValue("first_data", "A");

        ctx.Save.RequestSaveGame("save-1");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        // Create second state.
        ctx.Save.RequestSwitchForegroundLevel("second_level");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();
        pr.ProgressBlackboard.SetValue("second_data", "B");
        var bg2 = ctx.Runtime.SessionManager.CreateBackgroundSession("bg2", "bg2_level", true);
        bg2.Spawn(TickProbeMeta());

        ctx.Save.RequestSaveGame("save-2");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        pr.Dispose();

        // Restore from save-1.
        var (ctx2, _) = CreateContext(fs);
        ctx2.Save.RequestLoadGame("save-1");
        ctx2.Deferred.FlushDeferredActionsForCurrentFrame();

        Assert.Equal("first_level", ctx2.Runtime.SessionManager.ForegroundSession!.LevelId);
        Assert.True(ctx2.Blackboard.ProgressBlackboard!.TryGet<string>("first_data").found);

        // Override with save-2.
        ctx2.Save.RequestLoadGame("save-2");
        ctx2.Deferred.FlushDeferredActionsForCurrentFrame();

        Assert.Equal("second_level", ctx2.Runtime.SessionManager.ForegroundSession!.LevelId);
        Assert.True(ctx2.Blackboard.ProgressBlackboard!.TryGet<string>("second_data").found);
        Assert.NotNull(ctx2.Runtime.SessionManager.TryGet("bg2"));
        TickProbeStrategy.Reset();
        ctx2.Runtime.SessionManager.ProcessAllSessions(0.016, includeForeground: false);
        Assert.Contains("bg2_level", TickProbeStrategy.Ticked);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static (SndContext ctx, TestFileSystem fs) CreateContext(TestFileSystem? sharedFs = null)
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = sharedFs ?? new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        runtime.SndWorld.RegisterStrategy(() => new TickProbeStrategy());
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial",
            "res://entry/entry.json"));
        return (ctx, fs);
    }

    private static ProgressRun SetupProgressRun(SndContext ctx)
    {
        var progressRun = TestFactory.CreateProgressRun(
            "001", ctx.Runtime.Logger, ctx.MetaAccess, ctx.PathResolver, "root", ctx.Runtime, ctx, sharedDataSourceIo: ctx.DataSourceIo);
        ctx.SetProgressRun(progressRun);
        return progressRun;
    }

    private static SndMetaData TickProbeMeta() => new()
    {
        Name = "tick_probe",
        NodeMetaData = new NodeMetaData(),
        StrategyMetaData = new StrategyMetaData { LifecycleIndices = [_tickProbeIndex] },
        DataMetaData = new DataMetaData()
    };

    private const string _tickProbeIndex = "play_stop_play.tick_probe";

    [StrategyIndex(_tickProbeIndex)]
    private sealed class TickProbeStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<HashSet<string>?> _ticked = new();
        public static HashSet<string> Ticked => _ticked.Value ??= [];
        public static void Reset() => _ticked.Value = [];

        public override void Process(ISndEntity entity, double delta, ISndContext ctx) =>
            Ticked.Add(entity.OwningSession.LevelId);
    }
}
