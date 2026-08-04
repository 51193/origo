using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests.Snd;

public class PersistenceRequestTrackingTests
{
    private static SndContext CreateContext(TestMemoryFileSystem fs)
    {
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());

        var ctx = new SndContext(new SndContextParameters(
            runtime, io, metaAccess, pathResolver, "root", "res://initial", "entry.json")
        {
            AutoDiscoverStrategies = false
        });
        fs.SeedFile("entry.json",
            "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]");
        return ctx;
    }

    [Fact]
    public void RequestSaveGame_IsTrackedUntilFlushed()
    {
        var fs = new TestMemoryFileSystem();
        var ctx = CreateContext(fs);
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        ctx.Save.RequestSaveGame("tracked_slot");
        Assert.Equal(1, ctx.Deferred.GetPendingPersistenceRequestCount());

        ctx.Deferred.FlushDeferredActionsForCurrentFrame();
        Assert.Equal(0, ctx.Deferred.GetPendingPersistenceRequestCount());
    }

    [Fact]
    public void RequestContinueGame_IsTrackedUntilFlushed()
    {
        var fs = new TestMemoryFileSystem();
        var ctx = CreateContext(fs);
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();
        ctx.Save.RequestSaveGame("continue_slot");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        Assert.True(ctx.Lifecycle.RequestContinueGame());
        Assert.Equal(1, ctx.Deferred.GetPendingPersistenceRequestCount());

        ctx.Deferred.FlushDeferredActionsForCurrentFrame();
        Assert.Equal(0, ctx.Deferred.GetPendingPersistenceRequestCount());
    }

    [Fact]
    public void RequestLoadInitialSave_IsTrackedUntilFlushed()
    {
        // Loading the initial save requires an initial-storage snapshot that
        // this harness does not provide; only the tracking semantics of the
        // request are verified here.
        var fs = new TestMemoryFileSystem();
        var ctx = CreateContext(fs);

        ctx.Lifecycle.RequestLoadInitialSave();
        Assert.Equal(1, ctx.Deferred.GetPendingPersistenceRequestCount());
    }

    [Fact]
    public void RequestLoadMainMenuEntrySave_IsTrackedUntilFlushed()
    {
        var fs = new TestMemoryFileSystem();
        var ctx = CreateContext(fs);

        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        Assert.Equal(1, ctx.Deferred.GetPendingPersistenceRequestCount());

        ctx.Deferred.FlushDeferredActionsForCurrentFrame();
        Assert.Equal(0, ctx.Deferred.GetPendingPersistenceRequestCount());
    }
}
