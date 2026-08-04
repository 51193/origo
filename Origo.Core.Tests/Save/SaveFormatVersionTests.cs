using System;
using System.Collections.Generic;
using System.Globalization;
using Origo.Core.Save;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests.Save;

public class SaveFormatVersionTests
{
    private static (TestMemoryFileSystem fs, SndContext ctx) CreateSavedGame(string saveId)
    {
        var fs = new TestMemoryFileSystem();
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
        ctx.Save.RegisterSaveMetaContributor(_ =>
            new Dictionary<string, string> { ["display_name"] = "version test" });
        fs.SeedFile("entry.json",
            "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]");

        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        ctx.Save.RequestSaveGame(saveId);
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();
        return (fs, ctx);
    }

    [Fact]
    public void Save_WritesFormatVersionToMetaMap()
    {
        var (fs, _) = CreateSavedGame("ver_slot");

        var metaPath = "root/save_ver_slot/meta.map";
        Assert.True(fs.Exists(metaPath), "meta.map should exist");
        var metaText = fs.ReadAllText(metaPath);
        Assert.Contains("origo.format_version", metaText);
        Assert.Contains(SaveGamePayload.CurrentFormatVersion.ToString(CultureInfo.InvariantCulture), metaText);
    }

    [Fact]
    public void Load_RejectsSaveWithNewerFormatVersion()
    {
        var (fs, ctx) = CreateSavedGame("ver_slot");

        var metaPath = "root/save_ver_slot/meta.map";
        var metaText = fs.ReadAllText(metaPath);
        fs.WriteAllText(metaPath, metaText.Replace(
            "origo.format_version: 1", "origo.format_version: 99"), overwrite: true);

        ctx.Save.RequestLoadGame("ver_slot");
        Assert.Throws<InvalidOperationException>(
            () => ctx.Deferred.FlushDeferredActionsForCurrentFrame());
    }

    [Fact]
    public void Load_AcceptsMissingFormatVersionKey()
    {
        var (fs, ctx) = CreateSavedGame("ver_slot");

        // A save written before format-version tracking has no version key;
        // it must still load (treated as version 1).
        var metaPath = "root/save_ver_slot/meta.map";
        var metaText = fs.ReadAllText(metaPath);
        fs.WriteAllText(metaPath, metaText.Replace("origo.format_version: 1", ""), overwrite: true);

        ctx.Save.RequestLoadGame("ver_slot");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();
        Assert.NotNull(ctx.Runtime.SessionManager.ForegroundSession);
    }

    [Fact]
    public void ListSaves_HidesFrameworkReservedMetaKeys()
    {
        var (_, ctx) = CreateSavedGame("ver_slot");

        var saves = ctx.Save.ListSaves();
        Assert.Contains("ver_slot", saves);

        foreach (var entry in ctx.StorageService.EnumerateSavesWithMetaData())
        {
            Assert.DoesNotContain(entry.MetaData.Keys, k => k.StartsWith("origo.", StringComparison.Ordinal));
        }
    }
}
