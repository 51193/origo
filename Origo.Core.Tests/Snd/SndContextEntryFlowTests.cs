using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.DataSource;
using Xunit;

namespace Origo.Core.Tests;

public class SndContextEntryFlowTests
{
    [Fact]
    public void RequestLoadMainMenuEntrySave_MountsForegroundAndSpawnsEntryEntities()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        fs.SeedFile(
            "res://entry/entry.json",
            """
            {
              "levels": {
                "main_menu": { "snd_scene": "res://entry/main_menu_scene.json", "type": "main_menu" }
              },
              "main_menu_level": "main_menu"
            }
            """);
        fs.SeedFile(
            "res://entry/main_menu_scene.json",
            """
            [
              {
                "name": "EntryNpc",
                "node": { "pairs": {} },
                "strategy": { "lifecycle_indices": [] },
                "data": { "pairs": {} }
              }
            ]
            """);

        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial",
            "res://entry/entry.json"));
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        Assert.NotNull(ctx.Blackboard.ProgressBlackboard);
        Assert.NotNull(ctx.Runtime.SessionManager.ForegroundSession);
        Assert.NotNull(host.FindByName("EntryNpc"));
    }

    [Fact]
    public void RequestLoadMainMenuEntrySave_ClearsPreviousForegroundEntities()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("res://entry/entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial",
            "res://entry/entry.json"));

        host.CreateEntity(new SndMetaData
        {
            Name = "legacy",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        });
        Assert.NotNull(host.FindByName("legacy"));

        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        Assert.Null(host.FindByName("legacy"));
    }
}
