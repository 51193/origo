using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     验证后台 Session 创建后 <see cref="ISessionRun.IsFrontSession" /> 为 false。
/// </summary>
public class BackgroundSession_CreationWithCorrectFlagTests
{
    [Fact]
    public void GivenSessionManager_WhenCreateBackgroundSession_ThenIsFrontSessionIsFalse()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);
        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg_level");

        Assert.False(bg.IsFrontSession);
    }

    [Fact]
    public void GivenSessionManager_WhenCreateBackgroundWithSync_ThenIsFrontSessionIsFalse()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);
        using var bg = ctx.SessionManager.CreateBackgroundSession("bg", "bg_level", true);

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
