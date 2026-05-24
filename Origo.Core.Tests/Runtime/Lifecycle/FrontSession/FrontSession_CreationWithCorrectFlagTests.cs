using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     验证前台 Session 创建后 <see cref="ISessionRun.IsFrontSession" /> 为 true。
/// </summary>
public class FrontSession_CreationWithCorrectFlagTests
{
    [Fact]
    public void GivenSessionManager_WhenCreateForegroundSession_ThenIsFrontSessionIsTrue()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);
        var fg = ctx.SessionManager.ForegroundSession!;

        Assert.True(fg.IsFrontSession);
    }

    [Fact]
    public void GivenSessionManager_WhenCreateForegroundFromPayload_ThenIsFrontSessionIsTrue()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);
        // Load a payload into foreground
        var payload = new LevelPayload
        {
            LevelId = "default",
            SndSceneNode = TestFactory.NodeFromJson("[]"),
            SessionNode = TestFactory.NodeFromJson("{}"),
            SessionStateMachinesNode = TestFactory.NodeFromJson("{\"machines\":[]}")
        };
        ((SessionRun)ctx.SessionManager.ForegroundSession!).LoadFromPayload(payload);

        Assert.True(ctx.SessionManager.ForegroundSession!.IsFrontSession);
    }

    [Fact]
    public void GivenSessionManager_WhenSwitchForeground_ThenNewForegroundStillIsFrontSession()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);

        // 1st foreground
        var fg1 = ctx.SessionManager.ForegroundSession!;
        Assert.True(fg1.IsFrontSession);

        // Switch foreground (creates new one)
        var progressRun = ctx.EnsureProgressRun();
        progressRun.SwitchForeground("level_b");

        var fg2 = ctx.SessionManager.ForegroundSession!;
        Assert.True(fg2.IsFrontSession);
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
