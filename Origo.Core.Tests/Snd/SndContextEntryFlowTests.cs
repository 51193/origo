using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
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
        var fs = new TestFileSystem();
        fs.SeedFile(
            "res://entry/entry.json",
            """
            [
              {
                "name": "EntryNpc",
                "node": { "pairs": {} },
                "strategy": { "indices": [] },
                "data": { "pairs": {} }
              }
            ]
            """);

        var ctx = new SndContext(new SndContextParameters(runtime, fs, "root", "res://initial",
            "res://entry/entry.json"));
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.NotNull(ctx.ProgressBlackboard);
        Assert.NotNull(ctx.SessionManager.ForegroundSession);
        Assert.NotNull(runtime.Snd.FindByName("EntryNpc"));
    }

    [Fact]
    public void RequestLoadMainMenuEntrySave_ClearsPreviousForegroundEntities()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var ctx = new SndContext(new SndContextParameters(runtime, fs, "root", "res://initial",
            "res://entry/entry.json"));

        runtime.Snd.Spawn(new SndMetaData
        {
            Name = "legacy",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        });
        Assert.NotNull(runtime.Snd.FindByName("legacy"));

        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.Null(runtime.Snd.FindByName("legacy"));
    }
}
