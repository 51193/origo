using System;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Full shell-path regression for legacy level directories: a real
///     <c>RequestLoadGame</c> request must surface the actionable migration
///     error (not a raw path-validation exception and not a generic missing
///     progress file error), and must not leave a partially restored
///     <c>current/</c>. The same contract applies when the only remaining copy
///     is an interrupted snapshot backup (<c>save_{id}.bak/</c>); after the
///     documented directory rename and topology rewrite the backup must load
///     normally.
/// </summary>
public class SaveLegacyLevelMigrationTests
{
    [Fact]
    public void RequestLoadGame_LegacyMixedCaseLevelDirectory_ThrowsMigrationError_ThenLoadsAfterRename()
    {
        var (fs, ctx) = CreateContextWithSavedSlot();
        MakeLevelLegacy(fs, "MainMenu");

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
        RewriteTopology(fs, "root/save_legacy_slot/progress.json", "=MainMenu=", "=mainmenu=");

        ctx.Save.RequestLoadGame("legacy_slot");
        ctx.FlushFrame();

        AssertLoaded(ctx, fs, "mainmenu");
    }

    [Fact]
    public void RequestLoadGame_LegacyLevelInOnlyBackup_ThrowsMigrationError_ThenLoadsAfterRename()
    {
        var (fs, ctx) = CreateContextWithSavedSlot();
        MakeLevelLegacy(fs, "MainMenu");
        MoveSlotToBackup(fs, "root/save_legacy_slot.bak");

        ctx.Save.RequestLoadGame("legacy_slot");
        var ex = Assert.Throws<InvalidOperationException>(() => ctx.FlushFrame());

        // The recovery path must report the same actionable migration failure
        // as the strict reader, naming the directory and the actual backup.
        Assert.Contains("level_MainMenu", ex.Message, StringComparison.Ordinal);
        Assert.Contains("lowercase", ex.Message, StringComparison.Ordinal);
        Assert.Contains("save_legacy_slot.bak", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Missing required progress.json", ex.Message, StringComparison.Ordinal);

        // Reporting the failure must not mutate the only remaining copy: no
        // promotion, no deletion, and no partial current/ restore.
        Assert.True(fs.DirectoryExists("root/save_legacy_slot.bak"));
        Assert.False(fs.DirectoryExists("root/save_legacy_slot"));
        Assert.False(fs.DirectoryExists("root/current"));

        // A retry while the backup is still legacy must report the same
        // actionable failure; a failed load must not poison later attempts.
        ctx.Save.RequestLoadGame("legacy_slot");
        var retry = Assert.Throws<InvalidOperationException>(() => ctx.FlushFrame());
        Assert.Contains("level_MainMenu", retry.Message, StringComparison.Ordinal);
        Assert.True(fs.DirectoryExists("root/save_legacy_slot.bak"));

        // Documented migration on the only remaining backup: rename the
        // directory inside the backup and update the persisted topology. The
        // next read must promote the now-complete backup and load it.
        fs.Rename("root/save_legacy_slot.bak/level_MainMenu", "root/save_legacy_slot.bak/level_mainmenu");
        RewriteTopology(fs, "root/save_legacy_slot.bak/progress.json", "=MainMenu=", "=mainmenu=");

        ctx.Save.RequestLoadGame("legacy_slot");
        ctx.FlushFrame();

        AssertLoaded(ctx, fs, "mainmenu");
        Assert.True(fs.DirectoryExists("root/save_legacy_slot"));
        Assert.False(fs.DirectoryExists("root/save_legacy_slot.bak"));
    }

    [Fact]
    public void RequestLoadGame_LegacyTrailingPeriodLevelInOnlyBackup_ThrowsMigrationError()
    {
        var (fs, ctx) = CreateContextWithSavedSlot();
        MakeLevelLegacy(fs, "mainmenu.");
        MoveSlotToBackup(fs, "root/save_legacy_slot.bak");

        ctx.Save.RequestLoadGame("legacy_slot");
        var ex = Assert.Throws<InvalidOperationException>(() => ctx.FlushFrame());

        Assert.Contains("level_mainmenu.", ex.Message, StringComparison.Ordinal);
        Assert.Contains("lowercase", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Missing required progress.json", ex.Message, StringComparison.Ordinal);
        Assert.True(fs.DirectoryExists("root/save_legacy_slot.bak"));
        Assert.False(fs.DirectoryExists("root/save_legacy_slot"));
    }

    [Fact]
    public void RequestLoadGame_LegacyLevelInCaseVariantBackup_ThrowsMigrationError()
    {
        var (fs, ctx) = CreateContextWithSavedSlot();
        MakeLevelLegacy(fs, "MainMenu");
        MoveSlotToBackup(fs, "root/save_legacy_slot.BAK");

        ctx.Save.RequestLoadGame("legacy_slot");
        var ex = Assert.Throws<InvalidOperationException>(() => ctx.FlushFrame());

        // The message must name the path that was actually discovered, not a
        // reconstructed lowercase backup name.
        Assert.Contains("level_MainMenu", ex.Message, StringComparison.Ordinal);
        Assert.Contains("save_legacy_slot.BAK", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Missing required progress.json", ex.Message, StringComparison.Ordinal);
        Assert.True(fs.DirectoryExists("root/save_legacy_slot.BAK"));
        Assert.False(fs.DirectoryExists("root/save_legacy_slot"));
    }

    [Fact]
    public void RequestLoadGame_IncompleteLegacyLevelInOnlyBackup_StillReportsMissingProgress()
    {
        var (fs, ctx) = CreateContextWithSavedSlot();
        MakeLevelLegacy(fs, "MainMenu");
        // The rename would not make this backup loadable: the level payload is
        // incomplete. It must keep the generic incomplete-backup behavior
        // instead of claiming the legacy name is the only defect.
        fs.Delete("root/save_legacy_slot/level_MainMenu/session.json");
        MoveSlotToBackup(fs, "root/save_legacy_slot.bak");

        ctx.Save.RequestLoadGame("legacy_slot");
        var ex = Assert.Throws<InvalidOperationException>(() => ctx.FlushFrame());

        Assert.Contains("Missing required progress.json", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("level_MainMenu", ex.Message, StringComparison.Ordinal);
        Assert.True(fs.DirectoryExists("root/save_legacy_slot.bak"));
        Assert.False(fs.DirectoryExists("root/save_legacy_slot"));
    }

    [Fact]
    public void ListSaves_LegacyLevelInOnlyBackup_DoesNotThrowOrPromote()
    {
        var (fs, ctx) = CreateContextWithSavedSlot();
        MakeLevelLegacy(fs, "MainMenu");
        MoveSlotToBackup(fs, "root/save_legacy_slot.bak");

        // Listing is a discovery surface, not a load: it must not throw for a
        // backup it cannot promote and must not create a fake slot.
        var saves = ctx.Save.ListSaves();

        Assert.Empty(saves);
        Assert.True(fs.DirectoryExists("root/save_legacy_slot.bak"));
        Assert.False(fs.DirectoryExists("root/save_legacy_slot"));
    }

    private static (TestMemoryFileSystem Fs, SndContext Ctx) CreateContextWithSavedSlot()
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

        // Produce a real snapshot through the normal save path, then let each
        // test convert it into the legacy shape it targets.
        ctx.Save.RequestSaveGame("legacy_slot");
        ctx.FlushFrame();
        return (fs, ctx);
    }

    private static void MakeLevelLegacy(TestMemoryFileSystem fs, string legacyLevelId)
    {
        fs.Rename("root/save_legacy_slot/level_main_menu", $"root/save_legacy_slot/level_{legacyLevelId}");
        RewriteTopology(fs, "root/save_legacy_slot/progress.json", "=main_menu=", $"={legacyLevelId}=");
    }

    private static void MoveSlotToBackup(TestMemoryFileSystem fs, string backupPath) =>
        fs.Rename("root/save_legacy_slot", backupPath);

    private static void RewriteTopology(
        TestMemoryFileSystem fs,
        string progressPath,
        string oldValue,
        string newValue)
    {
        var before = fs.ReadAllText(progressPath);
        var after = before.Replace(oldValue, newValue, StringComparison.Ordinal);
        Assert.NotEqual(before, after);
        fs.WriteAllText(progressPath, after, overwrite: true);
    }

    private static void AssertLoaded(SndContext ctx, TestMemoryFileSystem fs, string expectedLevelId)
    {
        var session = Assert.IsType<ISessionRun>(
            ctx.Runtime.SessionManager.ForegroundSession, exactMatch: false);
        Assert.Equal(expectedLevelId, session.LevelId);
        Assert.True(fs.Exists("root/current/progress.json"));
        Assert.True(fs.Exists($"root/current/level_{expectedLevelId}/snd_scene.json"));
        Assert.True(fs.Exists($"root/current/level_{expectedLevelId}/session.json"));
        Assert.False(fs.Exists("root/current/.write_in_progress"));
    }
}
