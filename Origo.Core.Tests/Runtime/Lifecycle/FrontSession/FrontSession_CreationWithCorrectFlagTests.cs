using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save;
using Origo.Core.Snd;
using Xunit;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.DataSource;

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
        var fg = ctx.Runtime.SessionManager.ForegroundSession!;

        Assert.True(fg.IsFrontSession);
    }

    [Fact]
    public void GivenSessionManager_WhenCreateForegroundFromPayload_ThenIsFrontSessionIsTrue()
    {
        var (ctx, fs) = CreateContext();
        SetupForegroundSession(ctx);

        ctx.Runtime.SessionManager.ForegroundSession!.SessionBlackboard.SetValue("test", 42);
        ctx.Save.RequestSaveGame("fg_test");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists("root/save_fg_test/progress.json"));
        Assert.True(ctx.Runtime.SessionManager.ForegroundSession!.IsFrontSession);
    }

    [Fact]
    public void GivenSessionManager_WhenSwitchForeground_ThenNewForegroundStillIsFrontSession()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);

        // 1st foreground
        var fg1 = ctx.Runtime.SessionManager.ForegroundSession!;
        Assert.True(fg1.IsFrontSession);

        // Switch foreground (creates new one)
        var progressRun = ctx.EnsureProgressRun();
        progressRun.SwitchForeground("level_b");

        var fg2 = ctx.Runtime.SessionManager.ForegroundSession!;
        Assert.True(fg2.IsFrontSession);
    }

    private static (SndContext ctx, TestMemoryFileSystem fs) CreateContext()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial",
            "res://entry/entry.json"));
        return (ctx, fs);
    }

    private static void SetupForegroundSession(SndContext ctx)
    {
        var progressRun = TestFactory.CreateProgressRun(
            "001", ctx.Runtime.Logger, ctx.MetaAccess, ctx.PathResolver, "root", ctx.Runtime, ctx);
        ctx.SetProgressRun(progressRun);
        progressRun.LoadAndMountForeground("default");
    }
}
