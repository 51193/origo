using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     验证后台 Session 可以同时创建多个实例。
/// </summary>
public class BackgroundSession_MultipleInstancesAllowedTests
{
    [Fact]
    public void GivenSessionManager_WhenCreateMultipleBackgroundSessions_ThenAllCreatedSuccessfully()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);

        using var bg1 = ctx.SessionManager.CreateBackgroundSession("bg_1", "level_a");
        using var bg2 = ctx.SessionManager.CreateBackgroundSession("bg_2", "level_b");
        using var bg3 = ctx.SessionManager.CreateBackgroundSession("bg_3", "level_c");

        Assert.False(bg1.IsFrontSession);
        Assert.False(bg2.IsFrontSession);
        Assert.False(bg3.IsFrontSession);
        Assert.Equal("level_a", bg1.LevelId);
        Assert.Equal("level_b", bg2.LevelId);
        Assert.Equal("level_c", bg3.LevelId);
    }

    [Fact]
    public void GivenSessionManager_WhenMultipleBackgroundSessionsExist_ThenForegroundStillIsFront()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);
        using var bg1 = ctx.SessionManager.CreateBackgroundSession("bg_1", "level_a");
        using var bg2 = ctx.SessionManager.CreateBackgroundSession("bg_2", "level_b");

        var fg = ctx.SessionManager.ForegroundSession!;
        Assert.True(fg.IsFrontSession);
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
