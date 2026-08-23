using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Snd;
using Xunit;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.DataSource;

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
        var fg1 = (SessionRun)ctx.Runtime.SessionManager.ForegroundSession!;
        Assert.True(fg1.IsFrontSession);

        // Replace foreground with new level
        ctx.Save.RequestSwitchForegroundLevel("new_level");
        ctx.FlushFrame();
        var fg2 = (SessionRun)ctx.Runtime.SessionManager.ForegroundSession!;

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
        foreach (var key in ctx.Runtime.SessionManager.Keys)
            if (key == ISessionManager.ForegroundKey)
                foregroundCount++;

        Assert.Equal(1, foregroundCount);
    }

    [Fact]
    public void GivenSessionManager_WhenForegroundAndBackgroundExist_ThenOnlyForegroundHasFlag()
    {
        var (ctx, _) = CreateContext();
        SetupForegroundSession(ctx);
        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg_1", "bg_level");

        var fg = (SessionRun)ctx.Runtime.SessionManager.ForegroundSession!;
        Assert.True(fg.IsFrontSession);
        Assert.False(bg.IsFrontSession);
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
