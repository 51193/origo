using System;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public class SaveIdValidationTests
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

        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();
        return ctx;
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("a/b")]
    [InlineData("bad space")]
    [InlineData("bad+char")]
    [InlineData("bad:char")]
    [InlineData("bad\\char")]
    public void RequestSaveGame_InvalidSaveId_Throws(string saveId)
    {
        var ctx = CreateContext(new TestMemoryFileSystem());
        Assert.Throws<ArgumentException>(() => ctx.Save.RequestSaveGame(saveId));
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("a/b")]
    [InlineData("bad space")]
    public void RequestLoadGame_InvalidSaveId_Throws(string saveId)
    {
        var ctx = CreateContext(new TestMemoryFileSystem());
        Assert.Throws<ArgumentException>(() => ctx.Save.RequestLoadGame(saveId));
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("a/b")]
    [InlineData("bad space")]
    public void SetContinueTarget_InvalidSaveId_Throws(string saveId)
    {
        var ctx = CreateContext(new TestMemoryFileSystem());
        Assert.Throws<ArgumentException>(() => ctx.Save.SetContinueTarget(saveId));
    }

    [Theory]
    [InlineData("slot_001")]
    [InlineData("a.b-c")]
    [InlineData("SaveSlot1")]
    public void RequestSaveGame_ValidSaveId_Succeeds(string saveId)
    {
        var ctx = CreateContext(new TestMemoryFileSystem());
        ctx.Save.RequestSaveGame(saveId);
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();
        Assert.Contains(saveId, ctx.Save.ListSaves());
    }
}
