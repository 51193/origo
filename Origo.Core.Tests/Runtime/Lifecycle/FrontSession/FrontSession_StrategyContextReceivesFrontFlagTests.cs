using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     验证前台 Session 的策略 Context 中 IsFrontSession 信息正确传递。
/// </summary>
public class FrontSession_StrategyContextReceivesFrontFlagTests
{
    [Fact]
    public void GivenForegroundSession_WhenBuildSessionSndContext_ThenContextIsFrontSessionIsTrue()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);
        var fg = ctx.SessionManager.ForegroundSession!;

        // SessionSndContext wraps the session; verify IsFrontSession propagates
        var sessionCtx = new SessionSndContext(ctx, fg);
        Assert.True(sessionCtx.IsFrontSession);
        Assert.Same(fg, sessionCtx.CurrentSession);
    }

    [Fact]
    public void GivenGlobalSndContext_WhenForegroundMounted_ThenContextIsFrontSessionIsTrue()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);

        // Global context's CurrentSession points to foreground
        Assert.True(ctx.IsFrontSession);
    }

    [Fact]
    public void GivenGlobalSndContext_WhenNoForeground_ThenContextIsFrontSessionIsFalse()
    {
        var (ctx, _) = CreateContext();

        // No progress run, no session
        Assert.False(ctx.IsFrontSession);
        Assert.Null(ctx.CurrentSession);
    }

    private static (SndContext ctx, TestFileSystem fs) CreateContext()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
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
}
