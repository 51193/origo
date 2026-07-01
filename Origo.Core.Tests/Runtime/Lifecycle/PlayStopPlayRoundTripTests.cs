using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Snd;
using Xunit;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.DataSource;

namespace Origo.Core.Tests;

/// <summary>
///     Play-Stop-Play 完整往返测试：
///     验证 ProgressRun 序列化 → Dispose → 重建 → 反序列化 后，
///     前台 SessionRun 身份保留、Tick 状态保留、各 SessionBlackBoard 数据互不污染。
///     <para>
///         恢复入口仅需 saveId —— ProgressRun 在内部自行创建空白 ProgressBlackboard，
///         通过 <see cref="ProgressRun.LoadFromPayload" /> 读取持久化数据并恢复全部状态。
///         测试不注入预构建的 IBlackboard 实例。
///     </para>
/// </summary>
public class PlayStopPlayRoundTripTests
{
    // ── Full round-trip: serialize → dispose → recreate → deserialize ──

    [Fact]
    public void RoundTrip_ForegroundIdentity_Preserved()
    {
        // ── PLAY 1 ──────────────────────────────────────────────────────
        var (ctx1, _) = CreateContext();
        var pr1 = SetupProgressRun(ctx1);
        pr1.LoadAndMountForeground("level_a");

        var fg1 = ctx1.Runtime.SessionManager.ForegroundSession!;
        Assert.True(fg1.IsFrontSession);
        Assert.Equal("level_a", fg1.LevelId);

        // Serialize.
        var payload = pr1.BuildSavePayload("save-001");

        // ── STOP ────────────────────────────────────────────────────────
        pr1.Dispose();

        // ── PLAY 2 ──────────────────────────────────────────────────────
        var (ctx2, _) = CreateContext();
        var pr2 = SetupProgressRun(ctx2);
        pr2.LoadFromPayload(payload);

        var fg2 = ctx2.Runtime.SessionManager.ForegroundSession!;
        Assert.True(fg2.IsFrontSession, "Foreground identity must be restored after round-trip.");
        Assert.Equal("level_a", fg2.LevelId);

        pr2.Dispose();
    }

    [Fact]
    public void RoundTrip_BackgroundTickState_Preserved()
    {
        // ── PLAY 1 ──────────────────────────────────────────────────────
        var (ctx1, _) = CreateContext();
        var pr1 = SetupProgressRun(ctx1);
        pr1.LoadAndMountForeground("level_a");

        // Create background sessions: one with syncProcess=true, one with syncProcess=false.
        ctx1.Runtime.SessionManager.CreateBackgroundSession("bg_tick", "bg_level_tick", true);
        ctx1.Runtime.SessionManager.CreateBackgroundSession("bg_store", "bg_level_store");

        var sm = (SessionManager)ctx1.Runtime.SessionManager;
        Assert.Contains("bg_tick", sm.ProcessingKeys);
        Assert.DoesNotContain("bg_store", sm.ProcessingKeys);

        // Serialize.
        var payload = pr1.BuildSavePayload("save-002");

        // ── STOP ────────────────────────────────────────────────────────
        pr1.Dispose();

        // ── PLAY 2 ──────────────────────────────────────────────────────
        var (ctx2, _) = CreateContext();
        var pr2 = SetupProgressRun(ctx2);
        pr2.LoadFromPayload(payload);

        var sm2 = (SessionManager)ctx2.Runtime.SessionManager;
        Assert.Contains("bg_tick", sm2.ProcessingKeys);
        Assert.DoesNotContain("bg_store", sm2.ProcessingKeys);
        Assert.NotNull(ctx2.Runtime.SessionManager.TryGet("bg_tick"));
        Assert.NotNull(ctx2.Runtime.SessionManager.TryGet("bg_store"));

        pr2.Dispose();
    }

    [Fact]
    public void RoundTrip_SessionBlackboards_Isolated_NoCrossContamination()
    {
        // ── PLAY 1 ──────────────────────────────────────────────────────
        var (ctx1, _) = CreateContext();
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

        // Serialize.
        var payload = pr1.BuildSavePayload("save-003");

        // ── STOP ────────────────────────────────────────────────────────
        pr1.Dispose();

        // ── PLAY 2 ──────────────────────────────────────────────────────
        var (ctx2, _) = CreateContext();
        var pr2 = SetupProgressRun(ctx2);
        pr2.LoadFromPayload(payload);

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

        pr2.Dispose();
    }

    [Fact]
    public void RoundTrip_ProgressBlackboard_Shared_AcrossSessions()
    {
        // ── PLAY 1 ──────────────────────────────────────────────────────
        var (ctx1, _) = CreateContext();
        var pr1 = SetupProgressRun(ctx1);
        pr1.LoadAndMountForeground("level_a");
        pr1.ProgressBlackboard.SetValue("global_flag", "hello_world");

        ctx1.Runtime.SessionManager.CreateBackgroundSession("bg1", "bg_level");

        var payload = pr1.BuildSavePayload("save-004");

        // ── STOP ────────────────────────────────────────────────────────
        pr1.Dispose();

        // ── PLAY 2 ──────────────────────────────────────────────────────
        var (ctx2, _) = CreateContext();
        var pr2 = SetupProgressRun(ctx2);
        pr2.LoadFromPayload(payload);

        // ProgressBlackboard data is restored and shared.
        var (found, val) = pr2.ProgressBlackboard.TryGet<string>("global_flag");
        Assert.True(found);
        Assert.Equal("hello_world", val);

        pr2.Dispose();
    }

    [Fact]
    public void RoundTrip_AllSessionProperties_Restored_Correctly()
    {
        // ── PLAY 1: Create complex topology ─────────────────────────────
        var (ctx1, _) = CreateContext();
        var pr1 = SetupProgressRun(ctx1);
        pr1.LoadAndMountForeground("main_level");

        var fg1 = ctx1.Runtime.SessionManager.ForegroundSession!;
        fg1.SessionBlackboard.SetValue("score", 42);

        // Tickable background.
        var bgTick = ctx1.Runtime.SessionManager.CreateBackgroundSession("sim", "sim_level", true);
        bgTick.SessionBlackboard.SetValue("step", 7);

        // Non-tickable background.
        var bgStore = ctx1.Runtime.SessionManager.CreateBackgroundSession("cache", "cache_level");
        bgStore.SessionBlackboard.SetValue("cached", true);

        // Verify state before serialization.
        Assert.True(fg1.IsFrontSession);
        Assert.False(bgTick.IsFrontSession);
        Assert.False(bgStore.IsFrontSession);
        Assert.Contains("sim", ((SessionManager)ctx1.Runtime.SessionManager).ProcessingKeys);
        Assert.DoesNotContain("cache", ((SessionManager)ctx1.Runtime.SessionManager).ProcessingKeys);

        var payload = pr1.BuildSavePayload("save-005");

        // ── STOP ────────────────────────────────────────────────────────
        pr1.Dispose();

        // ── PLAY 2: Full restore ────────────────────────────────────────
        var (ctx2, _) = CreateContext();
        var pr2 = SetupProgressRun(ctx2);
        pr2.LoadFromPayload(payload);

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
        Assert.Contains("sim", ((SessionManager)ctx2.Runtime.SessionManager).ProcessingKeys);

        // Non-tickable background: restored with syncProcess=false.
        var cache2 = ctx2.Runtime.SessionManager.TryGet("cache")!;
        Assert.False(cache2.IsFrontSession);
        Assert.Equal("cache_level", cache2.LevelId);
        Assert.True(cache2.SessionBlackboard.TryGet<bool>("cached").value);
        Assert.DoesNotContain("cache", ((SessionManager)ctx2.Runtime.SessionManager).ProcessingKeys);

        // Cross-contamination check.
        Assert.False(fg2.SessionBlackboard.TryGet<int>("step").found);
        Assert.False(fg2.SessionBlackboard.TryGet<bool>("cached").found);
        Assert.False(sim2.SessionBlackboard.TryGet<int>("score").found);
        Assert.False(sim2.SessionBlackboard.TryGet<bool>("cached").found);
        Assert.False(cache2.SessionBlackboard.TryGet<int>("score").found);
        Assert.False(cache2.SessionBlackboard.TryGet<int>("step").found);

        pr2.Dispose();
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
        var (ctx1, _) = CreateContext();
        var pr1 = SetupProgressRun(ctx1);
        pr1.LoadAndMountForeground("level_x");
        pr1.ProgressBlackboard.SetValue("user_data", "important");

        ctx1.Runtime.SessionManager.CreateBackgroundSession("bg_sim", "sim_level", true);

        var payload = pr1.BuildSavePayload("save-rt");

        // ── STOP ────────────────────────────────────────────────────────
        pr1.Dispose();

        // ── PLAY 2: Restore from payload only ───────────────────────────
        var (ctx2, _) = CreateContext();
        var pr2 = SetupProgressRun(ctx2);

        // Before LoadFromPayload: completely empty.
        Assert.Null(pr2.SessionManager.ForegroundSession);
        Assert.Empty(pr2.SessionManager.Keys);

        pr2.LoadFromPayload(payload);

        // After LoadFromPayload: everything is restored from the payload.
        Assert.NotNull(pr2.SessionManager.ForegroundSession);
        Assert.True(pr2.SessionManager.ForegroundSession!.IsFrontSession);
        Assert.Equal("level_x", pr2.SessionManager.ForegroundSession!.LevelId);

        var (found, val) = pr2.ProgressBlackboard.TryGet<string>("user_data");
        Assert.True(found);
        Assert.Equal("important", val);

        Assert.NotNull(pr2.SessionManager.TryGet("bg_sim"));
        Assert.Contains("bg_sim", ((SessionManager)pr2.SessionManager).ProcessingKeys);

        pr2.Dispose();
    }

    [Fact]
    public void LoadFromPayload_CanBeCalledMultipleTimes()
    {
        // Verify that calling LoadFromPayload on an already-loaded ProgressRun
        // cleanly replaces all state (no residual data from previous load).
        var (ctx, _) = CreateContext();
        var pr = SetupProgressRun(ctx);
        pr.LoadAndMountForeground("first_level");
        pr.ProgressBlackboard.SetValue("first_data", "A");

        var payload1 = pr.BuildSavePayload("save-1");

        // Create second state.
        pr.SwitchForeground("second_level");
        pr.ProgressBlackboard.SetValue("second_data", "B");
        ctx.Runtime.SessionManager.CreateBackgroundSession("bg2", "bg2_level", true);

        var payload2 = pr.BuildSavePayload("save-2");

        pr.Dispose();

        // Restore from payload1.
        var (ctx2, _) = CreateContext();
        var pr2 = SetupProgressRun(ctx2);
        pr2.LoadFromPayload(payload1);

        Assert.Equal("first_level", pr2.SessionManager.ForegroundSession!.LevelId);
        Assert.True(pr2.ProgressBlackboard.TryGet<string>("first_data").found);

        // Override with payload2.
        pr2.LoadFromPayload(payload2);

        Assert.Equal("second_level", pr2.SessionManager.ForegroundSession!.LevelId);
        Assert.True(pr2.ProgressBlackboard.TryGet<string>("second_data").found);
        Assert.NotNull(pr2.SessionManager.TryGet("bg2"));
        Assert.Contains("bg2", ((SessionManager)pr2.SessionManager).ProcessingKeys);

        pr2.Dispose();
    }

    // ── Helpers ─────────────────────────────────────────────────────────

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
        return (ctx, fs);
    }

    private static ProgressRun SetupProgressRun(SndContext ctx)
    {
        var progressRun = TestFactory.CreateProgressRun(
            "001", ctx.Runtime.Logger, ctx.MetaAccess, ctx.PathResolver, "root", ctx.Runtime, ctx, sharedDataSourceIo: ctx.DataSourceIo);
        ctx.SetProgressRun(progressRun);
        return progressRun;
    }
}
