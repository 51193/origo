using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     验证后台 Session 的策略 Context 中 IsFrontSession 信息为 false。
/// </summary>
public class BackgroundSession_StrategyContextReceivesBackgroundFlagTests
{
    [Fact]
    public void GivenBackgroundSession_WhenBuildSessionSndContext_ThenContextIsFrontSessionIsFalse()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);
        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg_level");

        var sessionCtx = new SessionSndContext(ctx, bg);
        Assert.False(sessionCtx.IsFrontSession);
        Assert.Same(bg, sessionCtx.CurrentSession);
    }

    [Fact]
    public void GivenBackgroundSession_WhenContextChecked_ThenCurrentSessionIsCorrect()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);
        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg_level");

        var sessionCtx = new SessionSndContext(ctx, bg);
        Assert.Equal("bg_level", sessionCtx.CurrentSession!.LevelId);
        Assert.False(sessionCtx.CurrentSession.IsFrontSession);
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
