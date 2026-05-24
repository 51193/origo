using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     验证前台 Session 的唯一性约束：SessionManager 内部至多一个前台会话。
/// </summary>
public class FrontSession_UniqueConstraintValidationTests
{
    [Fact]
    public void GivenSessionManager_WhenCreateForegroundTwice_ThenOldForegroundReplaced()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);
        var fg1 = ctx.SessionManager.ForegroundSession!;
        Assert.True(fg1.IsFrontSession);

        // Replace foreground with new level
        ctx.EnsureProgressRun().SwitchForeground("new_level");
        var fg2 = ctx.SessionManager.ForegroundSession!;

        Assert.True(fg2.IsFrontSession);
        Assert.NotSame(fg1, fg2);
        Assert.Equal("new_level", fg2.LevelId);
    }

    [Fact]
    public void GivenSessionManager_WhenForegroundExists_ThenOnlyOneForegroundKey()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);

        var foregroundCount = 0;
        foreach (var key in ctx.SessionManager.Keys)
            if (key == ISessionManager.ForegroundKey)
                foregroundCount++;

        Assert.Equal(1, foregroundCount);
    }

    [Fact]
    public void GivenSessionManager_WhenForegroundAndBackgroundExist_ThenOnlyForegroundHasFlag()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);
        using var bg = ctx.SessionManager.CreateBackgroundSession("bg_1", "bg_level");

        var fg = ctx.SessionManager.ForegroundSession!;
        Assert.True(fg.IsFrontSession);
        Assert.False(bg.IsFrontSession);
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
