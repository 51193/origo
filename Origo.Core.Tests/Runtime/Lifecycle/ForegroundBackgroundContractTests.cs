using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;
using Origo.Core.StateMachine;
using Xunit;

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
        ((SessionManager)ctx.SessionManager).LoadSessionFromPayload("bg", payload);
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
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);
        var fg = ctx.SessionManager.ForegroundSession!;

        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg");

        // 两者都能序列化且结构一致
        var fgPayload = ((SessionRun)fg).SerializeToPayload();
        var bgPayload = ((SessionRun)bg).SerializeToPayload();

        Assert.NotNull(fgPayload);
        Assert.NotNull(bgPayload);
        Assert.Equal(fg.LevelId, fgPayload.LevelId);
        Assert.Equal(bg.LevelId, bgPayload.LevelId);
        Assert.False(fgPayload.SndSceneNode.IsNull);
        Assert.False(bgPayload.SndSceneNode.IsNull);
        Assert.False(fgPayload.SessionStateMachinesNode.IsNull);
        Assert.False(bgPayload.SessionStateMachinesNode.IsNull);
    }

    [Fact]
    public void LoadFromPayload_WorksIdentically_ForForegroundAndBackground()
    {
        var (ctx, _) = CreateContext();
        var payload = new LevelPayload
        {
            LevelId = "test_level",
            SndSceneNode = TestFactory.NodeFromJson("[]"),
            SessionNode = TestFactory.NodeFromJson("""{"key1":{"type":"Int32","data":42}}"""),
            SessionStateMachinesNode = TestFactory.NodeFromJson("{\"machines\":[]}")
        };

        // 前台
        SetupForegroundSession(ctx);
        var fg = ctx.SessionManager.ForegroundSession!;
        ((SessionRun)fg).LoadFromPayload(payload);
        var (fgFound, fgVal) = fg.SessionBlackboard.TryGet<int>("key1");

        // 后台
        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg");
        ((SessionRun)bg).LoadFromPayload(payload);
        var (bgFound, bgVal) = bg.SessionBlackboard.TryGet<int>("key1");

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

        fg.SessionBlackboard.Set("x", "hello");
        bg.SessionBlackboard.Set("x", "world");

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

        fg.SessionBlackboard.Set("only_fg", 1);
        bg.SessionBlackboard.Set("only_bg", 2);

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
        Assert.Throws<ObjectDisposedException>(() => fg.SceneHost);
        Assert.Throws<ObjectDisposedException>(() => bg.SceneHost);
        Assert.Throws<ObjectDisposedException>(() => fg.GetSessionStateMachines());
        Assert.Throws<ObjectDisposedException>(() => bg.GetSessionStateMachines());
    }

    // ── 5. 状态机行为一致 ──────────────────────────────────────────────

    [Fact]
    public void StateMachines_WorkIdentically_ForForegroundAndBackground()
    {
        ContractPushStrategy.Events = new List<string>();

        try
        {
            var (ctx, _) = CreateContext(w =>
            {
                w.RegisterStrategy(() => new ContractPushStrategy());
                w.RegisterStrategy(() => new ContractPopStrategy());
            });

            SetupForegroundSession(ctx);
            var fg = ctx.SessionManager.ForegroundSession!;
            using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg");

            // 前台 push
            var fgMachine = fg.GetSessionStateMachines().CreateOrGet(
                "test_sm", "contract.push", "contract.pop");
            fgMachine.Push("state_a");

            // 后台 push
            var bgMachine = bg.GetSessionStateMachines().CreateOrGet(
                "test_sm", "contract.push", "contract.pop");
            bgMachine.Push("state_a");

            // 两者都应触发策略钩子且事件格式一致
            Assert.Equal(2, ContractPushStrategy.Events!.Count);
            Assert.Equal(ContractPushStrategy.Events![0], ContractPushStrategy.Events![1]);
        }
        finally
        {
            ContractPushStrategy.Events = null;
        }
    }

    // ── 6. PersistLevelState 行为一致 ──────────────────────────────────

    [Fact]
    public void PersistLevelState_WritesToStorage_ForBothForegroundAndBackground()
    {
        var (ctx, fs) = CreateContext();
        SetupForegroundSession(ctx);
        var fg = ctx.SessionManager.ForegroundSession!;
        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg");

        ((SessionRun)fg).PersistLevelState();
        ((SessionRun)bg).PersistLevelState();

        // 两者都应写入 current/ 下的关卡文件
        Assert.True(fs.Exists($"root/current/level_{fg.LevelId}/snd_scene.json"));
        Assert.True(fs.Exists("root/current/level_bg/snd_scene.json"));
    }

    // ── 7. 接口统一使用契约 ────────────────────────────────────────────

    [Fact]
    public void BusinessCode_CanTreatBothSessionsIdentically_ThroughInterface()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);
        var fg = ctx.SessionManager.ForegroundSession!;
        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg");

        // 业务代码只通过 ISessionRun 接口操作，不区分前后台
        var sessions = new List<ISessionRun> { fg, bg };
        foreach (var session in sessions)
        {
            session.SessionBlackboard.Set("unified_key", session.LevelId);
            var payload = ((SessionRun)session).SerializeToPayload();
            Assert.NotNull(payload);
            Assert.Equal(session.LevelId, payload.LevelId);

            var (found, val) = session.SessionBlackboard.TryGet<string>("unified_key");
            Assert.True(found);
            Assert.Equal(session.LevelId, val);
        }
    }

    [Fact]
    public void RoundTrip_SerializeAndLoad_IdenticalBetweenForegroundAndBackground()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);

        // 后台序列化
        using var bg1 = ctx.SessionManager.CreateBackgroundSession("bg1", "level_a");
        bg1.SessionBlackboard.Set("data", 99);
        var payload = ((SessionRun)bg1).SerializeToPayload();

        // 另一个后台反序列化
        using var bg2 = ctx.SessionManager.CreateBackgroundSession("bg2", "level_a");
        ((SessionManager)ctx.SessionManager).LoadSessionFromPayload("bg2", payload);
        var (found, val) = bg2.SessionBlackboard.TryGet<int>("data");
        Assert.True(found);
        Assert.Equal(99, val);

        // 前台也能反序列化相同 payload
        SetupForegroundSession(ctx);
        var fg = ctx.SessionManager.ForegroundSession!;
        ((SessionRun)fg).LoadFromPayload(payload);
        var (fgFound, fgVal) = fg.SessionBlackboard.TryGet<int>("data");
        Assert.True(fgFound);
        Assert.Equal(99, fgVal);
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static (SndContext ctx, TestFileSystem fs) CreateContext(
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
        return (ctx, fs);
    }

    private static void SetupForegroundSession(SndContext ctx)
    {
        var progressRun = TestFactory.CreateProgressRun(
            "001", ctx.Runtime.Logger, ctx.FileSystem, "root", ctx.Runtime, ctx);
        ctx.SetProgressRun(progressRun);
        progressRun.LoadAndMountForeground("default");
    }

    // ── Test strategies ───────────────────────────────────────────────

    [StrategyIndex("contract.push")]
    private sealed class ContractPushStrategy : StateMachineStrategyBase
    {
        internal static List<string>? Events { get; set; }

        public override void OnPushRuntime(StateMachineStrategyContext context, IStateMachineContext ctx) =>
            Events?.Add($"push:{context.BeforeTop ?? "null"}->{context.AfterTop ?? "null"}");
    }

    [StrategyIndex("contract.pop")]
    private sealed class ContractPopStrategy : StateMachineStrategyBase
    {
    }
}
