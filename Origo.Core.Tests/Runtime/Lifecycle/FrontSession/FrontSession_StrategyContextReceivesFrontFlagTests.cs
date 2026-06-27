using Origo.Core.Snd;
using Xunit;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.DataSource;

namespace Origo.Core.Tests;

/// <summary>
///     验证前台 Session 的策略 Context 中 IsFrontSession 信息正确传递。
/// </summary>
public class FrontSession_StrategyContextReceivesFrontFlagTests
{
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
        Assert.Null(ctx.SessionManager.ForegroundSession);
    }

    private static (SndContext ctx, TestFileSystem fs) CreateContext()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
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
