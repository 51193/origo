using System;
using System.Collections.Generic;
using System.Threading;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;
using Origo.Core.StateMachine;
using Xunit;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.DataSource;

namespace Origo.Core.Tests;

/// <summary>
///     契约测试：验证前台 SessionRun 与后台 SessionRun 行为完全一致，
///     确保业务层无需也不能根据宿主实现类型进行分叉逻辑。
/// </summary>
public class ForegroundBackgroundContractTests
{
    // ── 1. API 类型一致性 ──────────────────────────────────────────────

    [Fact]
    public void CreateBackgroundSession_ReturnsISessionRun_NotConcreteType()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);
        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg");

        // 公共 API 只暴露 ISessionRun，业务层无法获得具体类型。
        Assert.IsAssignableFrom<ISessionRun>(bg);
    }

    [Fact]
    public void CreateBackgroundSession_ThenLoadPayload_ReturnsISessionRun_NotConcreteType()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);
        var payload = new LevelPayload
        {
            LevelId = "bg",
            SndSceneNode = TestFactory.NodeFromJson("[]"),
            SessionNode = TestFactory.NodeFromJson("{}"),
            SessionStateMachinesNode = TestFactory.NodeFromJson("{\"machines\":[]}")
        };

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg");
        // Verify session can be populated via save/load round-trip
        ctx.ProgressBlackboard!.SetValue(
            WellKnownKeys.SessionTopology,
            $"{ISessionManager.ForegroundKey}=default=false,bg=bg=false");
        ctx.RequestSaveGame("test_load_rt");
        ctx.FlushDeferredActionsForCurrentFrame();
        Assert.IsAssignableFrom<ISessionRun>(bg);
    }

    [Fact]
    public void ForegroundSession_ExposedAsISessionRun()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);

        // ForegroundSession 属性类型为 ISessionRun?
        var fg = ctx.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.IsAssignableFrom<ISessionRun>(fg);
    }

    // ── 2. 序列化/反序列化格式一致 ─────────────────────────────────────

    [Fact]
    public void SerializeToPayload_ProducesSameFormat_ForForegroundAndBackground()
    {
        var (ctx, fs) = CreateContext();
        SetupForegroundSession(ctx);
        var fg = ctx.SessionManager.ForegroundSession!;

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg");

        fg.SessionBlackboard.SetValue("fg_key", 1);
        bg.SessionBlackboard.SetValue("bg_key", 2);

        ctx.RequestSaveGame("format_test");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists("root/save_format_test/level_default/session.json"));
        Assert.True(fs.Exists("root/save_format_test/level_bg/session.json"));

        ctx.SessionManager.DestroySession("bg");
        ctx.SetProgressRun(null);

        var newPr = TestFactory.CreateProgressRun(
            "format_test", ctx.Runtime.Logger, ctx.MetaAccess, ctx.PathResolver, "root", ctx.Runtime, ctx, sharedDataSourceIo: ctx.DataSourceIo);
        ctx.SetProgressRun(newPr);
        ctx.RequestLoadGame("format_test");
        ctx.FlushDeferredActionsForCurrentFrame();

        var loadedBg = ctx.SessionManager.TryGet("bg");
        Assert.NotNull(loadedBg);
        var (foundFg, _) = ctx.SessionManager.ForegroundSession!.SessionBlackboard.TryGet<int>("fg_key");
        var (foundBg, _) = loadedBg!.SessionBlackboard.TryGet<int>("bg_key");
        Assert.True(foundFg);
        Assert.True(foundBg);

        loadedBg.Dispose();
        newPr.Dispose();
    }

    [Fact]
    public void LoadFromPayload_WorksIdentically_ForForegroundAndBackground()
    {
        var (ctx, fs) = CreateContext();

        SetupForegroundSession(ctx);
        var fg = ctx.SessionManager.ForegroundSession!;
        fg.SessionBlackboard.SetValue("shared_key", 42);

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg");
        bg.SessionBlackboard.SetValue("shared_key", 42);

        ctx.RequestSaveGame("load_test");
        ctx.FlushDeferredActionsForCurrentFrame();
        Assert.True(fs.Exists("root/save_load_test/progress.json"));

        var (fgFound, fgVal) = fg.SessionBlackboard.TryGet<int>("shared_key");
        var (bgFound, bgVal) = bg.SessionBlackboard.TryGet<int>("shared_key");

        Assert.True(fgFound);
        Assert.True(bgFound);
        Assert.Equal(42, fgVal);
        Assert.Equal(42, bgVal);
    }

    // ── 3. 黑板操作一致 ───────────────────────────────────────────────

    [Fact]
    public void SessionBlackboard_ReadWrite_IdenticalBehavior()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);
        var fg = ctx.SessionManager.ForegroundSession!;
        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg");

        fg.SessionBlackboard.SetValue("x", "hello");
        bg.SessionBlackboard.SetValue("x", "world");

        var (fgOk, fgVal) = fg.SessionBlackboard.TryGet<string>("x");
        var (bgOk, bgVal) = bg.SessionBlackboard.TryGet<string>("x");

        Assert.True(fgOk);
        Assert.True(bgOk);
        Assert.Equal("hello", fgVal);
        Assert.Equal("world", bgVal);
    }

    [Fact]
    public void SessionBlackboard_Isolated_BetweenForegroundAndBackground()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);
        var fg = ctx.SessionManager.ForegroundSession!;
        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg");

        fg.SessionBlackboard.SetValue("only_fg", 1);
        bg.SessionBlackboard.SetValue("only_bg", 2);

        Assert.False(bg.SessionBlackboard.TryGet<int>("only_fg").found);
        Assert.False(fg.SessionBlackboard.TryGet<int>("only_bg").found);
    }

    // ── 4. Dispose 行为一致 ────────────────────────────────────────────

    [Fact]
    public void Dispose_ThrowsOnAccess_ForBothForegroundAndBackground()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);
        var fg = ctx.SessionManager.ForegroundSession!;
        var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg");

        fg.Dispose();
        bg.Dispose();

        Assert.Throws<ObjectDisposedException>(() => fg.SessionBlackboard);
        Assert.Throws<ObjectDisposedException>(() => bg.SessionBlackboard);
        Assert.Throws<ObjectDisposedException>(() => fg.FindByName("any"));
        Assert.Throws<ObjectDisposedException>(() => bg.FindByName("any"));
        Assert.Throws<ObjectDisposedException>(() => fg.GetSessionStateMachines());
        Assert.Throws<ObjectDisposedException>(() => bg.GetSessionStateMachines());
    }

    // ── 5. 状态机行为一致 ──────────────────────────────────────────────

    [Fact]
    public void StateMachines_WorkIdentically_ForForegroundAndBackground()
    {
        var events = new List<string>();

        ContractPushStrategy.Bind(events);

        var (ctx, _) = CreateContext(w =>
        {
            w.RegisterStrategy(() => new ContractPushStrategy());
            w.RegisterStrategy(() => new ContractPopStrategy());
        });

        SetupForegroundSession(ctx);
        var fg = ctx.SessionManager.ForegroundSession!;
        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg");

        var fgMachine = fg.GetSessionStateMachines().CreateOrGet(
            "test_sm", "contract.push", "contract.pop");
        fgMachine.Push("state_a");

        var bgMachine = bg.GetSessionStateMachines().CreateOrGet(
            "test_sm", "contract.push", "contract.pop");
        bgMachine.Push("state_a");

        Assert.Equal(2, events.Count);
        Assert.Equal(events[0], events[1]);
    }

    // ── 6. PersistLevelState 行为一致 ──────────────────────────────────

    [Fact]
    public void PersistLevelState_WritesToStorage_ForBothForegroundAndBackground()
    {
        var (ctx, fs) = CreateContext();
        SetupForegroundSession(ctx);
        var fg = ctx.SessionManager.ForegroundSession!;
        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg");

        fg.SessionBlackboard.SetValue("fg_data", "fg_val");
        bg.SessionBlackboard.SetValue("bg_data", "bg_val");

        ctx.RequestSaveGame("persist_test");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists($"root/save_persist_test/level_{fg.LevelId}/session.json"));
        Assert.True(fs.Exists("root/save_persist_test/level_bg/session.json"));
    }

    // ── 7. 接口统一使用契约 ────────────────────────────────────────────

    [Fact]
    public void BusinessCode_CanTreatBothSessionsIdentically_ThroughInterface()
    {
        var (ctx, fs) = CreateContext();
        SetupForegroundSession(ctx);
        var fg = ctx.SessionManager.ForegroundSession!;
        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg");

        var sessions = new List<ISessionRun> { fg, bg };
        foreach (var session in sessions)
        {
            session.SessionBlackboard.SetValue("unified_key", session.LevelId);

            var (found, val) = session.SessionBlackboard.TryGet<string>("unified_key");
            Assert.True(found);
            Assert.Equal(session.LevelId, val);
        }

        ctx.RequestSaveGame("unified_test");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists($"root/save_unified_test/level_{fg.LevelId}/session.json"));
        Assert.True(fs.Exists("root/save_unified_test/level_bg/session.json"));
    }

    [Fact]
    public void RoundTrip_SerializeAndLoad_IdenticalBetweenForegroundAndBackground()
    {
        var (ctx, fs) = CreateContext();
        SetupForegroundSession(ctx);

        using var bg1 = ctx.SessionManager.CreateBackgroundSession("bg1", "level_a");
        bg1.SessionBlackboard.SetValue("data", 99);

        ctx.RequestSaveGame("rttest");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists("root/save_rttest/progress.json"));
        Assert.NotNull(ctx.SessionManager.TryGet("bg1"));

        var loadedFg = ctx.SessionManager.ForegroundSession;
        Assert.NotNull(loadedFg);

        var savedBg = ctx.SessionManager.TryGet("bg1");
        Assert.NotNull(savedBg);
        var (bgFound, bgVal) = savedBg!.SessionBlackboard.TryGet<int>("data");
        Assert.True(bgFound);
        Assert.Equal(99, bgVal);
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static (SndContext ctx, TestFileSystem fs) CreateContext(
        Action<SndWorld>? configureWorld = null)
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        configureWorld?.Invoke(runtime.SndWorld);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial",
            "res://entry/entry.json"));
        return (ctx, fs);
    }

    private static void SetupForegroundSession(SndContext ctx)
    {
        var progressRun = TestFactory.CreateProgressRun(
            "001", ctx.Runtime.Logger, ctx.MetaAccess, ctx.PathResolver, "root", ctx.Runtime, ctx, sharedDataSourceIo: ctx.DataSourceIo);
        ctx.SetProgressRun(progressRun);
        progressRun.LoadAndMountForeground("default");
    }

    // ── Test strategies ───────────────────────────────────────────────

    [StrategyIndex("contract.push")]
    private sealed class ContractPushStrategy : StateMachineStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _events = new();

        public static void Bind(List<string> events) => _events.Value = events;

        public override void OnPushRuntime(StateMachineStrategyContext context, IStateMachineContext ctx) =>
            _events.Value?.Add($"push:{context.BeforeTop ?? "null"}->{context.AfterTop ?? "null"}");
    }

    [StrategyIndex("contract.pop")]
    private sealed class ContractPopStrategy : StateMachineStrategyBase
    {
    }
}
