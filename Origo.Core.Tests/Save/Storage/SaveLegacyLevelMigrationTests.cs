using System;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Full shell-path regression for legacy mixed-case level directories: a
///     real <c>RequestLoadGame</c> request must surface the actionable
///     migration error (not a raw path-validation exception) and must not leave
///     a partially restored <c>current/</c>; after the documented directory
///     rename and topology rewrite the same save must load normally.
/// </summary>
public class SaveLegacyLevelMigrationTests
{
    [Fact]
    public void RequestLoadGame_LegacyMixedCaseLevelDirectory_ThrowsMigrationError_ThenLoadsAfterRename()
    {
        var fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(new TestLogger(), new TestSndSceneHost());
        var ctx = new SndContext(new SndContextParameters(
            runtime, io, metaAccess, pathResolver, "root", "res://initial", "entry.json")
        {
            AutoDiscoverStrategies = false
        });
        fs.SeedFile("entry.json",
            "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]");
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.FlushFrame();

        // Produce a real snapshot through the normal save path, then convert it
        // on disk into the legacy shape this test targets: a mixed-case level
        // directory plus the matching mixed-case id in the persisted topology.
        ctx.Save.RequestSaveGame("legacy_slot");
        ctx.FlushFrame();
        fs.Rename("root/save_legacy_slot/level_main_menu", "root/save_legacy_slot/level_MainMenu");
        var progressPath = "root/save_legacy_slot/progress.json";
        var validProgress = fs.ReadAllText(progressPath);
        var legacyProgress = validProgress.Replace("=main_menu=", "=MainMenu=", StringComparison.Ordinal);
        Assert.NotEqual(validProgress, legacyProgress);
        fs.WriteAllText(progressPath, legacyProgress, overwrite: true);

        ctx.Save.RequestLoadGame("legacy_slot");
        var ex = Assert.Throws<InvalidOperationException>(() => ctx.FlushFrame());

        Assert.Contains("MainMenu", ex.Message, StringComparison.Ordinal);
        Assert.Contains("level_MainMenu", ex.Message, StringComparison.Ordinal);
        Assert.Contains("lowercase", ex.Message, StringComparison.Ordinal);

        // The real load workflow tears down the active progress run (and its
        // current/) before it reads the slot; the migration failure happens
        // before the restore write, so no payload or marker appears afterwards.
        Assert.False(fs.DirectoryExists("root/current"));
        Assert.False(fs.Exists("root/current/progress.json"));
        Assert.False(fs.Exists("root/current/.write_in_progress"));

        // Documented migration: rename the directory to the lowercase id and
        // rewrite the persisted topology level id to match.
        fs.Rename("root/save_legacy_slot/level_MainMenu", "root/save_legacy_slot/level_mainmenu");
        var migratedProgress = validProgress.Replace("=main_menu=", "=mainmenu=", StringComparison.Ordinal);
        Assert.NotEqual(validProgress, migratedProgress);
        fs.WriteAllText(progressPath, migratedProgress, overwrite: true);

        ctx.Save.RequestLoadGame("legacy_slot");
        ctx.FlushFrame();

        var session = Assert.IsType<ISessionRun>(
            ctx.Runtime.SessionManager.ForegroundSession, exactMatch: false);
        Assert.Equal("mainmenu", session.LevelId);
        Assert.True(fs.Exists("root/current/progress.json"));
        Assert.True(fs.Exists("root/current/level_mainmenu/snd_scene.json"));
        Assert.True(fs.Exists("root/current/level_mainmenu/session.json"));
        Assert.False(fs.Exists("root/current/.write_in_progress"));
    }
}
