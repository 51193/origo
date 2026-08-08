using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Snd;
using Xunit;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.DataSource;

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

        using var bg1 = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg_1", "level_a");
        using var bg2 = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg_2", "level_b");
        using var bg3 = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg_3", "level_c");

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
        using var bg1 = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg_1", "level_a");
        using var bg2 = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg_2", "level_b");

        var fg = (SessionRun)ctx.Runtime.SessionManager.ForegroundSession!;
        Assert.True(fg.IsFrontSession);
    }

    private static (SndContext ctx, TestMemoryFileSystem fs) CreateContext()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("res://entry/entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
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
