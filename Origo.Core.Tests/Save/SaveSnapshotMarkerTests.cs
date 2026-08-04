using Origo.Core.DataSource;
using Origo.Core.Snd;
using System;
using Xunit;

namespace Origo.Core.Tests;

public class SaveSnapshotMarkerTests
{
    [Fact]
    public void Snapshot_DoesNotContainWriteInProgressMarker()
    {
        var fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);

        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);

        var ctx = new SndContext(new SndContextParameters(
            runtime, io, metaAccess, pathResolver, "root", "res://initial", "entry.json")
        {
            AutoDiscoverStrategies = false
        });
        fs.SeedFile("entry.json",
            "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]");

        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        ctx.Save.RequestSaveGame("slot_001");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        var snapshotDir = "root/save_slot_001";
        Assert.True(fs.DirectoryExists(snapshotDir), "snapshot directory should exist");

        var files = fs.EnumerateFiles(snapshotDir, "*", true);
        Assert.DoesNotContain(files, f => f.EndsWith(".write_in_progress", StringComparison.Ordinal));
    }
}
