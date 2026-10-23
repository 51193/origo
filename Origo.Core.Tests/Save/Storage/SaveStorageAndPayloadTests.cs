using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.DataSource;
using Origo.Core.Save;
using Origo.Core.Save.Storage;
using Xunit;

namespace Origo.Core.Tests;

public class SaveStorageAndPayloadTests
{
    [Fact]
    public void SaveStorageFacade_WriteAndReadCurrent_RoundTrip()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "user://origo_saves");
        var progressSm = """{"machines":[]}""";
        var sessionSm = """{"machines":[]}""";
        var payload = new SaveGamePayload
        {
            SaveId = "001",
            ActiveLevelId = "default",
            ProgressNode = TestFactory.NodeFromJson("""{"k":{"type":"Int32","data":1}}"""),
            ProgressStateMachinesNode = TestFactory.NodeFromJson(progressSm),
            Levels = new Dictionary<string, LevelPayload>
            {
                ["default"] = new()
                {
                    LevelId = "default",
                    SndSceneNode = TestFactory.NodeFromJson("[]"),
                    SessionNode = TestFactory.NodeFromJson("""{"s":{"type":"String","data":"ok"}}"""),
                    SessionStateMachinesNode = TestFactory.NodeFromJson(sessionSm)
                }
            }
        };

        WritePayloadWithPayloadSha(handle, payload);
        var loaded = SaveStorageFacade.ReadSavePayloadFromCurrent(handle, "001", "default");

        Assert.Equal("001", loaded.SaveId);
        Assert.Equal("default", loaded.ActiveLevelId);
        Assert.Contains("\"k\"", TestFactory.JsonFromNode(loaded.ProgressNode));
        Assert.Equal("[]", TestFactory.JsonFromNode(loaded.Levels["default"].SndSceneNode));
        // Disk round-trip may re-encode with different whitespace; normalize via parse + encode.
        Assert.Equal(CanonicalJsonLiteral(progressSm),
            CanonicalJsonLiteral(TestFactory.JsonFromNode(loaded.ProgressStateMachinesNode)));
        Assert.Equal(CanonicalJsonLiteral(sessionSm),
            CanonicalJsonLiteral(TestFactory.JsonFromNode(loaded.Levels["default"].SessionStateMachinesNode)));
    }

    private static void WritePayloadWithPayloadSha(SaveFileHandle handle, SaveGamePayload payload)
    {
        SavePayloadWriter.WriteToCurrent(handle, payload);
        SaveAtomicWriter.WritePayloadSha(handle, handle.PathPolicy.GetCurrentDirectory(),
            SavePayloadWriter.ComputePayloadHash(payload));
    }

    private static string CanonicalJsonLiteral(string json)
    {
        using var n = TestFactory.NodeFromJson(json);
        return TestFactory.JsonFromNode(n);
    }

    [Fact]
    public void SaveStorageFacade_WriteSavePayloadToCurrentThenSnapshot_WhenMarkerLeft_DoesNotIdempotentSkip()
    {
        // A failed snapshot leaves the write-in-progress marker on disk and
        // the combined hash in the snapshot slot. A retry with identical
        // content must not take the idempotent skip (which would leave
        // current/ refusing reads); it must rewrite and clear the marker.
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        var logger = new TestLogger();
        var progressSm = """{"machines":[]}""";
        var payload = new SaveGamePayload
        {
            SaveId = "001",
            ActiveLevelId = "default",
            ProgressNode = TestFactory.NodeFromJson("""{"marker":"content"}"""),
            ProgressStateMachinesNode = TestFactory.NodeFromJson(progressSm),
            Levels = new Dictionary<string, LevelPayload>
            {
                ["default"] = new()
                {
                    LevelId = "default",
                    SndSceneNode = TestFactory.NodeFromJson("[]"),
                    SessionNode = TestFactory.NodeFromJson("{}"),
                    SessionStateMachinesNode = TestFactory.NodeFromJson(progressSm)
                }
            }
        };

        // State after an interrupted snapshot: current/ carries the marker and
        // the snapshot slot already holds the matching combined hash.
        var combinedHash = SaveAtomicWriter.ComputeCombinedHash(handle, payload);
        var saveShaRel = handle.PathPolicy.GetPayloadShaFile(handle.PathPolicy.GetSaveDirectory("001"));
        fs.SeedFile(fs.CombinePath("root", saveShaRel), combinedHash);
        var markerRel = SavePathLayout.GetWriteInProgressMarker(SavePathLayout.GetCurrentDirectory());
        fs.SeedFile(fs.CombinePath("root", markerRel), string.Empty);

        SaveStorageFacade.WriteSavePayloadToCurrentThenSnapshot(handle, payload, "001", logger);

        Assert.False(fs.Exists(fs.CombinePath("root", markerRel)),
            "The retry must clear the leftover write-in-progress marker.");
        Assert.True(fs.DirectoryExists("root/save_001"), "The retry must complete the snapshot.");
    }

    [Fact]
    public void SaveStorageFacade_WriteLevelPayloadOnly_NullStateMachines_NoFilesWritten()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");

        var level = new LevelPayload
        {
            LevelId = "lvl",
            SndSceneNode = TestFactory.NodeFromJson("[]"),
            SessionNode = TestFactory.NodeFromJson("{}"),
            SessionStateMachinesNode = TestFactory.NodeFromJson("null")
        };

        Assert.Throws<InvalidOperationException>(() =>
            SaveStorageFacade.WriteLevelPayloadOnly(handle, SavePathLayout.GetCurrentDirectory(), level));

        Assert.False(fs.Exists("root/current/level_lvl/snd_scene.json"));
        Assert.False(fs.Exists("root/current/level_lvl/session.json"));
    }

    [Fact]
    public void SaveStorageFacade_EnumerateSaveIds_ReservedSuffixDirectories_AreNotSlots()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        fs.CreateDirectory("root/save_foo.tmp");
        fs.CreateDirectory("root/save_bar");
        fs.CreateDirectory("root/save_bar.tmp");
        fs.CreateDirectory("root/save_bar.bak");

        var ids = SaveStorageFacade.EnumerateSaveIds(handle);

        // Atomic-write remnants are never user slots; the real bar slot remains.
        Assert.Equal(["bar"], ids);
    }

    [Fact]
    public void SaveStorageFacade_EnumerateSaveIds_InvalidDirectory_IsSkipped()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        fs.CreateDirectory("root/save_bad space");
        fs.CreateDirectory("root/save_good");

        var ids = SaveStorageFacade.EnumerateSaveIds(handle);

        Assert.Equal(["good"], ids);
        Assert.DoesNotContain(
            SaveStorageFacade.EnumerateSavesWithMetaData(handle),
            entry => entry.SaveId == "bad space");
    }

    [Fact]
    public void SnapshotCurrentToSave_IncompleteStaging_KeepsExistingSlotAndRemovesTemp()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var storage = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");
        fs.SeedFile("root/save_001/progress.json", "{\"old\":true}");
        fs.SeedFile("root/current/data.txt", "not a save");

        Assert.Throws<InvalidOperationException>(() => storage.SnapshotCurrentToSave("001"));

        Assert.Equal("{\"old\":true}", fs.ReadAllText("root/save_001/progress.json"));
        Assert.False(fs.DirectoryExists("root/save_001.tmp"));
    }

    [Fact]
    public void SavePathLayout_ValidateSaveId_RejectsReservedSuffixes()
    {
        Assert.Throws<ArgumentException>(() => SavePathLayout.ValidateSaveId("foo.tmp", "saveId"));
        Assert.Throws<ArgumentException>(() => SavePathLayout.ValidateSaveId("foo.bak", "saveId"));
        Assert.Throws<ArgumentException>(() => SavePathLayout.ValidateSaveId("foo.TMP", "saveId"));
        SavePathLayout.ValidateSaveId("foo.tmpx", "saveId");
    }

    [Fact]
    public void SavePathLayout_ValidateSaveId_RejectsCaseAndDotAliases()
    {
        Assert.Throws<ArgumentException>(() => SavePathLayout.ValidateSaveId("SaveSlot1", "saveId"));
        Assert.Throws<ArgumentException>(() => SavePathLayout.ValidateSaveId(".", "saveId"));
        Assert.Throws<ArgumentException>(() => SavePathLayout.ValidateSaveId("..", "saveId"));
        Assert.Throws<ArgumentException>(() => SavePathLayout.ValidateSaveId("...", "saveId"));
        Assert.Throws<ArgumentException>(() => SavePathLayout.ValidateSaveId("slot.", "saveId"));
        SavePathLayout.ValidateSaveId("slot.one", "saveId");
    }

    [Fact]
    public void SavePathLayout_ValidateLevelId_RejectsCaseAndDotAliases()
    {
        Assert.Throws<ArgumentException>(() => SavePathLayout.ValidateLevelId("Town", "levelId"));
        Assert.Throws<ArgumentException>(() => SavePathLayout.ValidateLevelId("..", "levelId"));
        Assert.Throws<ArgumentException>(() => SavePathLayout.ValidateLevelId("town.", "levelId"));
        SavePathLayout.ValidateLevelId("town", "levelId");
    }

    [Fact]
    public void SnapshotCurrentToSave_WithoutProgressFiles_ThrowsAndDoesNotInstallSlot()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        fs.SeedFile("root/current/data.txt", "not a save");

        Assert.Throws<InvalidOperationException>(() => SaveStorageFacade.SnapshotCurrentToSave(handle, "001"));
        Assert.False(fs.DirectoryExists("root/save_001"));
        Assert.False(fs.DirectoryExists("root/save_001.tmp"));
    }

    [Fact]
    public void SaveStorageFacade_HasCaseVariantSlot_DetectsLegacyUppercaseSlot()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        fs.CreateDirectory("root/save_Legacy");

        Assert.True(SaveStorageFacade.HasCaseVariantSlot(handle, "legacy"));
        Assert.False(SaveStorageFacade.HasCaseVariantSlot(handle, "other"));

        fs.CreateDirectory("root/save_Legacy.bak");
        fs.CreateDirectory("root/save_other.tmp");
        Assert.True(SaveStorageFacade.HasCaseVariantSlot(handle, "legacy"));
        Assert.False(SaveStorageFacade.HasCaseVariantSlot(handle, "other"));
    }

    [Fact]
    public void DefaultSaveStorageService_WriteToCaseVariantLegacySlot_Throws()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var storage = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");
        fs.CreateDirectory("root/save_Legacy");

        var ex = Assert.Throws<ArgumentException>(() =>
            storage.WriteSavePayloadToCurrentThenSnapshot(
                TestFactory.CreateMinimalPayload("legacy", "default"), "legacy", new TestLogger()));

        Assert.Contains("case", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SavePayloadWriter_CaseVariantLegacyLevel_Throws()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        fs.CreateDirectory("root/current/level_Town");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            SavePayloadWriter.EnsureNoCaseVariantLevelDirectory(handle, "current", "town"));

        Assert.Contains("case", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SaveStorageFacade_RecoverInterruptedSnapshots_RestoresCompleteBackup()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        SeedCompleteSnapshot(fs, "root/save_recovered.bak");

        var ids = SaveStorageFacade.EnumerateSaveIds(handle);

        Assert.Equal(["recovered"], ids);
        Assert.True(fs.DirectoryExists("root/save_recovered"));
        Assert.False(fs.DirectoryExists("root/save_recovered.bak"));
    }

    [Fact]
    public void SaveStorageFacade_ReadProgressFromSnapshot_RecoversCompleteBackup()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        SeedCompleteSnapshot(fs, "root/save_recovered.bak");

        using var progress = SaveStorageFacade.ReadProgressNodeFromSnapshot(handle, "recovered");

        Assert.NotNull(progress);
        Assert.True(fs.DirectoryExists("root/save_recovered"));
        Assert.False(fs.DirectoryExists("root/save_recovered.bak"));
    }

    [Fact]
    public void DefaultSaveStorageService_ReadProgressFromSnapshot_RecoversCompleteBackup()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        SeedCompleteSnapshot(fs, "root/save_recovered.bak");
        var service = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");

        using var progress = service.ReadProgressNodeFromSnapshot("recovered");

        Assert.NotNull(progress);
        Assert.True(fs.DirectoryExists("root/save_recovered"));
        Assert.False(fs.DirectoryExists("root/save_recovered.bak"));
    }

    [Fact]
    public void DefaultSaveStorageService_TryReadLevelPayloadFromSnapshot_RecoversCompleteBackup()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        SeedCompleteSnapshot(fs, "root/save_recovered.bak");
        var service = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");

        var level = service.TryReadLevelPayloadFromSnapshot("recovered", "default");

        Assert.NotNull(level);
        Assert.Equal("default", level!.LevelId);
        SavePayloadDisposal.Dispose(level);
        Assert.True(fs.DirectoryExists("root/save_recovered"));
        Assert.False(fs.DirectoryExists("root/save_recovered.bak"));
    }

    [Fact]
    public void SaveStorageFacade_EnumerateSaveIds_UsesPolicyRecovery()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(
            metaAccess, dataSourceIo, pathResolver, "root", new PrefixedRecoveryPathPolicy());
        SeedCompleteCustomSnapshot(fs, "root/custom_save_recovered.bak");

        var ids = SaveStorageFacade.EnumerateSaveIds(handle);

        Assert.Equal(["recovered"], ids);
        Assert.True(fs.DirectoryExists("root/custom_save_recovered"));
        Assert.False(fs.DirectoryExists("root/custom_save_recovered.bak"));
    }

    [Fact]
    public void SaveStorageFacade_EnumerateSaveIds_RecoversCaseVariantBackupSuffix()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        SeedCompleteSnapshot(fs, "root/save_recovered.BAK");

        var ids = SaveStorageFacade.EnumerateSaveIds(handle);

        Assert.Equal(["recovered"], ids);
        Assert.True(fs.DirectoryExists("root/save_recovered"));
        Assert.False(fs.DirectoryExists("root/save_recovered.BAK"));
    }

    [Fact]
    public void DefaultSaveStorageService_ReadProgressFromSnapshot_RecoversCaseVariantBackupSuffix()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        SeedCompleteSnapshot(fs, "root/save_recovered.BAK");
        var service = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");

        using var progress = service.ReadProgressNodeFromSnapshot("recovered");

        Assert.NotNull(progress);
        Assert.True(fs.DirectoryExists("root/save_recovered"));
        Assert.False(fs.DirectoryExists("root/save_recovered.BAK"));
    }

    [Fact]
    public void DefaultSaveStorageService_TryReadLevelPayloadFromSnapshot_MissingRoot_ReturnsNull()
    {
        var fs = new ThrowOnMissingRootEnumerationFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var service = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "missing_root");

        var level = service.TryReadLevelPayloadFromSnapshot("missing", "default");

        Assert.Null(level);
    }

    [Fact]
    public void DefaultSaveStorageService_ReadProgressFromSnapshot_MissingRoot_ReturnsNull()
    {
        var fs = new ThrowOnMissingRootEnumerationFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var service = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "missing_root");

        var progress = service.ReadProgressNodeFromSnapshot("missing");

        Assert.Null(progress);
    }

    [Fact]
    public void DefaultSaveStorageService_ReadSavePayloadFromSnapshot_MissingRoot_ThrowsMissingProgress()
    {
        var fs = new ThrowOnMissingRootEnumerationFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var service = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "missing_root");

        var exception = Assert.Throws<InvalidOperationException>(
            () => service.ReadSavePayloadFromSnapshot("missing", "default"));

        Assert.Contains("Missing required progress.json", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultSaveStorageService_ReadSavePayloadFromSnapshot_RecoversCompleteBackup()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        SeedCompleteSnapshot(fs, "root/save_recovered.bak");
        var service = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");

        var payload = service.ReadSavePayloadFromSnapshot("recovered", "default");

        Assert.Equal("recovered", payload.SaveId);
        Assert.Contains("default", payload.Levels.Keys);
        SavePayloadDisposal.Dispose(payload);
        Assert.True(fs.DirectoryExists("root/save_recovered"));
        Assert.False(fs.DirectoryExists("root/save_recovered.bak"));
    }

    [Fact]
    public void DefaultSaveStorageService_WriteSavePayloadToCurrent_PrunesCustomLevelDirectories()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var service = new DefaultSaveStorageService(
            metaAccess, dataSourceIo, pathResolver, "root", new PrefixedRecoveryPathPolicy());
        fs.SeedFile("root/current/custom_level_old/snd_scene.json", "[]");
        fs.SeedFile("root/current/custom_level_old/session.json", "{}");
        fs.SeedFile("root/current/custom_level_old/session_state_machines.json", """{"machines":[]}""");

        var payload = new SaveGamePayload
        {
            SaveId = "new_slot",
            ActiveLevelId = "new",
            ProgressNode = TestFactory.NodeFromJson("{}"),
            ProgressStateMachinesNode = TestFactory.NodeFromJson("""{"machines":[]}"""),
            Levels = new Dictionary<string, LevelPayload>
            {
                ["new"] = new()
                {
                    LevelId = "new",
                    SndSceneNode = TestFactory.NodeFromJson("[]"),
                    SessionNode = TestFactory.NodeFromJson("{}"),
                    SessionStateMachinesNode = TestFactory.NodeFromJson("""{"machines":[]}""")
                }
            }
        };

        service.WriteSavePayloadToCurrent(payload);

        Assert.False(fs.DirectoryExists("root/current/custom_level_old"));
        Assert.True(fs.DirectoryExists("root/current/custom_level_new"));
        SavePayloadDisposal.Dispose(payload);
    }

    [Fact]
    public void DefaultSaveStorageService_ReadSavePayloadFromSnapshot_RecoversCustomLayout()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        SeedCompleteCustomSnapshot(fs, "root/custom_save_recovered.bak");
        var service = new DefaultSaveStorageService(
            metaAccess, dataSourceIo, pathResolver, "root", new PrefixedRecoveryPathPolicy());

        var payload = service.ReadSavePayloadFromSnapshot("recovered", "default");

        Assert.Equal("recovered", payload.SaveId);
        Assert.Contains("bg", payload.Levels.Keys);
        SavePayloadDisposal.Dispose(payload);
        Assert.True(fs.DirectoryExists("root/custom_save_recovered"));
        Assert.False(fs.DirectoryExists("root/custom_save_recovered.bak"));
    }

    [Fact]
    public void SaveStorageFacade_RecoverInterruptedSnapshots_LeavesIncompleteBackup()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        fs.SeedFile("root/save_incomplete.bak/progress.json", "{}");

        var ids = SaveStorageFacade.EnumerateSaveIds(handle);

        Assert.Empty(ids);
        Assert.True(fs.DirectoryExists("root/save_incomplete.bak"));
    }

    [Fact]
    public void SaveStorageFacade_SnapshotCurrentToSave_PreservesRootMarkerButCopiesArchiveMarker()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        fs.SeedFile("root/current/progress.json", "{}");
        fs.SeedFile("root/current/progress_state_machines.json", """{"machines":[]}""");
        fs.SeedFile("root/current/.write_in_progress", "");
        fs.SeedFile("root/current/extra/.write_in_progress", "user data");

        SaveStorageFacade.SnapshotCurrentToSave(handle, "001");

        Assert.False(fs.Exists("root/save_001/.write_in_progress"));
        Assert.True(fs.Exists("root/save_001/extra/.write_in_progress"));
        Assert.Equal("user data", fs.ReadAllText("root/save_001/extra/.write_in_progress"));
    }

    [Fact]
    public void WriteSavePayloadToCurrentThenSnapshot_WhenRenameFails_LogsErrorAndPreservesCurrent()
    {
        var fs = new FailOnRenameFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        var logger = new TestLogger();
        fs.SeedFile("root/current/progress.json", "{}");
        fs.SeedFile("root/current/progress_state_machines.json", """{"machines":[]}""");
        fs.SeedFile("root/current/level_default/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_default/session.json", "{}");
        fs.SeedFile("root/current/level_default/session_state_machines.json", """{"machines":[]}""");
        fs.FailTempToFinalRename = true;

        Assert.Throws<IOException>(() =>
            SaveStorageFacade.WriteSavePayloadToCurrentThenSnapshot(
                handle, CreateFullPayloadForRenameTest(), "001", logger));

        Assert.NotEmpty(logger.Errors);
        Assert.True(fs.Exists("root/current/progress.json"));
        Assert.False(fs.DirectoryExists("root/save_001"));
    }

    private static SaveGamePayload CreateFullPayloadForRenameTest() => new()
    {
        SaveId = "001",
        ActiveLevelId = "default",
        ProgressNode = TestFactory.NodeFromJson("{}"),
        ProgressStateMachinesNode = TestFactory.NodeFromJson("""{"machines":[]}"""),
        Levels = new Dictionary<string, LevelPayload>
        {
            ["default"] = new()
            {
                LevelId = "default",
                SndSceneNode = TestFactory.NodeFromJson("[]"),
                SessionNode = TestFactory.NodeFromJson("{}"),
                SessionStateMachinesNode = TestFactory.NodeFromJson("""{"machines":[]}""")
            }
        }
    };

    private static void SeedCompleteSnapshot(TestMemoryFileSystem fs, string root)
    {
        fs.SeedFile($"{root}/progress.json", "{}");
        fs.SeedFile($"{root}/progress_state_machines.json", """{"machines":[]}""");
        fs.SeedFile($"{root}/level_default/snd_scene.json", "[]");
        fs.SeedFile($"{root}/level_default/session.json", "{}");
        fs.SeedFile($"{root}/level_default/session_state_machines.json", """{"machines":[]}""");
    }

    private static void SeedCompleteCustomSnapshot(TestMemoryFileSystem fs, string root)
    {
        fs.SeedFile($"{root}/progress.json", "{}");
        fs.SeedFile($"{root}/progress_state_machines.json", """{"machines":[]}""");
        fs.SeedFile($"{root}/custom_level_default/snd_scene.json", "[]");
        fs.SeedFile($"{root}/custom_level_default/session.json", "{}");
        fs.SeedFile($"{root}/custom_level_default/session_state_machines.json", """{"machines":[]}""");
        fs.SeedFile($"{root}/custom_level_bg/snd_scene.json", "[]");
        fs.SeedFile($"{root}/custom_level_bg/session.json", "{}");
        fs.SeedFile($"{root}/custom_level_bg/session_state_machines.json", """{"machines":[]}""");
    }

    [Fact]
    public void SaveStorageFacade_EnumerateSaveIds_NullHandle_Throws() =>
        Assert.Throws<ArgumentNullException>(() => SaveStorageFacade.EnumerateSaveIds(null!));

    [Fact]
    public void SaveStorageFacade_DeleteSave_RemovesSlotAndRemnants()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        fs.SeedFile("root/save_slot/progress.json", "{}");
        fs.SeedFile("root/save_slot.tmp/progress.json", "{}");
        fs.SeedFile("root/save_slot.bak/progress.json", "{}");
        Assert.True(fs.DirectoryExists("root/save_slot"));
        Assert.True(fs.DirectoryExists("root/save_slot.tmp"));
        Assert.True(fs.DirectoryExists("root/save_slot.bak"));

        SaveStorageFacade.DeleteSave(handle, "slot");

        Assert.False(fs.DirectoryExists("root/save_slot"));
        Assert.False(fs.DirectoryExists("root/save_slot.tmp"));
        Assert.False(fs.DirectoryExists("root/save_slot.bak"));
    }

    [Fact]
    public void SaveStorageFacade_DeleteSave_RemovesCaseVariantBackup()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        fs.SeedFile("root/save_slot/progress.json", "{}");
        fs.SeedFile("root/save_slot.bak/progress.json", "{}");
        fs.SeedFile("root/save_slot.BAK/progress.json", "{}");

        SaveStorageFacade.DeleteSave(handle, "slot");

        Assert.False(fs.DirectoryExists("root/save_slot"));
        Assert.False(fs.DirectoryExists("root/save_slot.bak"));
        Assert.False(fs.DirectoryExists("root/save_slot.BAK"));
    }

    [Fact]
    public void SaveStorageFacade_EnumerateSaveIds_MalformedLevelInBackup_DoesNotThrow()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        SeedCompleteSnapshot(fs, "root/save_incomplete.bak");
        fs.SeedFile("root/save_incomplete.bak/level_bad name/snd_scene.json", "[]");
        fs.SeedFile("root/save_incomplete.bak/level_bad name/session.json", "{}");
        fs.SeedFile("root/save_incomplete.bak/level_bad name/session_state_machines.json", """{"machines":[]}""");

        var ids = SaveStorageFacade.EnumerateSaveIds(handle);

        Assert.Empty(ids);
        Assert.True(fs.DirectoryExists("root/save_incomplete.bak"));
    }

    [Fact]
    public void SaveStorageFacade_DeleteSave_MissingSlot_Throws()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");

        var ex = Assert.Throws<InvalidOperationException>(
            () => SaveStorageFacade.DeleteSave(handle, "missing"));

        Assert.Contains("missing", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveStorageFacade_DeleteSave_DoesNotTouchCurrentDirectory()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        fs.SeedFile("root/current/progress.json", "{}");
        fs.SeedFile("root/save_slot/progress.json", "{}");
        Assert.True(fs.DirectoryExists("root/save_slot"));

        SaveStorageFacade.DeleteSave(handle, "slot");

        Assert.False(fs.DirectoryExists("root/save_slot"));
        Assert.True(fs.Exists("root/current/progress.json"));
    }

    [Fact]
    public void SaveStorageFacade_SnapshotCurrentToSave_MissingCurrentDirectory_Throws()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");

        var ex = Assert.Throws<InvalidOperationException>(
            () => SaveStorageFacade.SnapshotCurrentToSave(handle, "001"));

        Assert.Contains("current/", ex.Message, StringComparison.Ordinal);
        // The fail-fast guard must fire before any snapshot write.
        Assert.False(fs.DirectoryExists("root/save_001"));
    }

    [Fact]
    public void SaveStorageFacade_SnapshotCurrentToSave_WhitespaceNewSaveId_Throws()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        Assert.Throws<ArgumentException>(() => SaveStorageFacade.SnapshotCurrentToSave(handle, "  "));
    }

    [Fact]
    public void SaveStorageFacade_ReadSavePayloadFromSnapshot_WhitespaceSaveId_Throws()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");

        var ex = Assert.Throws<ArgumentException>(
            () => SaveStorageFacade.ReadSavePayloadFromSnapshot(handle, "  ", "default"));

        Assert.Equal("saveId", ex.ParamName);
    }

    [Fact]
    public void SaveStorageFacade_ReadProgressNodeFromSnapshot_Missing_ReturnsNull()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        Assert.Null(SaveStorageFacade.ReadProgressNodeFromSnapshot(handle, "missing"));
    }

    [Fact]
    public void SaveStorageFacade_ReadProgressNodeFromSnapshot_WhenPresent_ReturnsContent()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        fs.SeedFile("root/save_042/progress.json", """{"k":1}""");
        using var node = SaveStorageFacade.ReadProgressNodeFromSnapshot(handle, "042");
        var json = TestFactory.JsonFromNode(node!);
        Assert.Contains("\"k\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveStorageFacade_EnumerateSavesWithMetaData_SlotWithoutMetaMap_StillListed()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        fs.CreateDirectory("root/save_007");
        var entries = SaveStorageFacade.EnumerateSavesWithMetaData(handle);
        Assert.Single(entries);
        Assert.Equal("007", entries[0].SaveId);
        Assert.Empty(entries[0].MetaData);
    }

    [Fact]
    public void SaveStorageFacade_ReadCurrent_MissingProgressStateMachines_Throws()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var root = "root";
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, root);
        var currentRel = SavePathLayout.GetCurrentDirectory();
        var progressAbs = fs.CombinePath(root, SavePathLayout.GetProgressFile(currentRel));
        var sndSceneAbs = fs.CombinePath(root,
            SavePathLayout.GetLevelSndSceneFile(SavePathLayout.GetLevelDirectory(currentRel, "default")));
        var sessionAbs = fs.CombinePath(root,
            SavePathLayout.GetLevelSessionFile(SavePathLayout.GetLevelDirectory(currentRel, "default")));
        var sessionSmAbs = fs.CombinePath(root,
            SavePathLayout.GetLevelSessionStateMachinesFile(SavePathLayout.GetLevelDirectory(currentRel, "default")));

        fs.SeedFile(progressAbs, "{}");
        fs.SeedFile(sndSceneAbs, "[]");
        fs.SeedFile(sessionAbs, "{}");
        fs.SeedFile(sessionSmAbs, """{"machines":[]}""");

        Assert.Throws<InvalidOperationException>(() =>
            SaveStorageFacade.ReadSavePayloadFromCurrent(handle, "001", "default"));
    }

    [Fact]
    public void SaveStorageFacade_ReadCurrent_MissingSessionStateMachines_Throws()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var root = "root";
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, root);
        var currentRel = SavePathLayout.GetCurrentDirectory();
        var progressAbs = fs.CombinePath(root, SavePathLayout.GetProgressFile(currentRel));
        var progressSmAbs = fs.CombinePath(root, SavePathLayout.GetProgressStateMachinesFile(currentRel));
        var sndSceneAbs = fs.CombinePath(root,
            SavePathLayout.GetLevelSndSceneFile(SavePathLayout.GetLevelDirectory(currentRel, "default")));
        var sessionAbs = fs.CombinePath(root,
            SavePathLayout.GetLevelSessionFile(SavePathLayout.GetLevelDirectory(currentRel, "default")));

        fs.SeedFile(progressAbs, "{}");
        fs.SeedFile(progressSmAbs, """{"machines":[]}""");
        fs.SeedFile(sndSceneAbs, "[]");
        fs.SeedFile(sessionAbs, "{}");

        Assert.Throws<InvalidOperationException>(() =>
            SaveStorageFacade.ReadSavePayloadFromCurrent(handle, "001", "default"));
    }

    [Fact]
    public void SaveStorageFacade_SnapshotCurrentToSave_AndEnumerateSaveIds_Works()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        var payload = new SaveGamePayload
        {
            SaveId = "001",
            ActiveLevelId = "default",
            ProgressNode = TestFactory.NodeFromJson("{}"),
            ProgressStateMachinesNode = TestFactory.NodeFromJson("""{"machines":[]}"""),
            Levels = new Dictionary<string, LevelPayload>
            {
                ["default"] = new()
                {
                    LevelId = "default",
                    SndSceneNode = TestFactory.NodeFromJson("[]"),
                    SessionNode = TestFactory.NodeFromJson("{}"),
                    SessionStateMachinesNode = TestFactory.NodeFromJson("""{"machines":[]}""")
                }
            }
        };
        WritePayloadWithPayloadSha(handle, payload);

        SaveStorageFacade.SnapshotCurrentToSave(handle, "001");
        var ids = SaveStorageFacade.EnumerateSaveIds(handle);

        Assert.Contains("001", ids);
    }

    [Fact]
    public void SaveStorageFacade_SnapshotCurrentToSave_UsesTempDirectoryThenRename()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        fs.SeedFile("root/current/progress.json", "{}");
        fs.SeedFile("root/current/progress_state_machines.json", """{"machines":[]}""");
        fs.SeedFile("root/current/level_default/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_default/session.json", """{"k":"v"}""");

        SaveStorageFacade.SnapshotCurrentToSave(handle, "001");

        // No leftover .tmp directory.
        Assert.False(fs.DirectoryExists("root/save_001.tmp"));

        // Final save directory has all four files.
        Assert.True(fs.Exists("root/save_001/progress.json"));
        Assert.True(fs.Exists("root/save_001/progress_state_machines.json"));
        Assert.True(fs.Exists("root/save_001/level_default/snd_scene.json"));
        Assert.True(fs.Exists("root/save_001/level_default/session.json"));

        // Content must match the originals.
        Assert.Equal("{}", fs.ReadAllText("root/save_001/progress.json"));
        Assert.Equal("""{"k":"v"}""", fs.ReadAllText("root/save_001/level_default/session.json"));
    }

    [Fact]
    public void SnapshotCurrentToSave_OverwritingExistingSave_ReplacesContentAndLeavesNoBackup()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");

        // First snapshot establishes save_001 with the old content.
        fs.SeedFile("root/current/progress.json", """{"v":"old"}""");
        fs.SeedFile("root/current/progress_state_machines.json", """{"machines":[]}""");
        SaveStorageFacade.SnapshotCurrentToSave(handle, "001");
        Assert.Equal("""{"v":"old"}""", fs.ReadAllText("root/save_001/progress.json"));

        // Second snapshot overwrites it, exercising the backup-then-rename path.
        fs.SeedFile("root/current/progress.json", """{"v":"new"}""");
        SaveStorageFacade.SnapshotCurrentToSave(handle, "001");

        Assert.Equal("""{"v":"new"}""", fs.ReadAllText("root/save_001/progress.json"));
        // The old data is not deleted before the new data is in place, and the
        // backup/temp directories must not be left behind.
        Assert.False(fs.DirectoryExists("root/save_001.bak"));
        Assert.False(fs.DirectoryExists("root/save_001.tmp"));
    }

    [Fact]
    public void WriteToCurrent_WhenActiveLevelMissing_ThrowsWithoutWritingCurrent()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        var payload = new SaveGamePayload
        {
            SaveId = "001",
            ActiveLevelId = "missing",
            ProgressNode = TestFactory.NodeFromJson("""{"k":{"type":"Int32","data":1}}"""),
            ProgressStateMachinesNode = TestFactory.NodeFromJson("""{"machines":[]}"""),
            Levels = new Dictionary<string, LevelPayload>
            {
                ["default"] = new()
                {
                    LevelId = "default",
                    SndSceneNode = TestFactory.NodeFromJson("[]"),
                    SessionNode = TestFactory.NodeFromJson("{}"),
                    SessionStateMachinesNode = TestFactory.NodeFromJson("""{"machines":[]}""")
                }
            }
        };

        Assert.Throws<InvalidOperationException>(() => SavePayloadWriter.WriteToCurrent(handle, payload));

        // Payload completeness is validated before any write, so current/ must
        // not contain a partially-written progress file.
        Assert.False(fs.Exists("root/current/progress.json"));
    }

    [Fact]
    public void SaveStorageFacade_ReadCurrent_ActiveLevelPartial_MissingSession_Throws()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var root = "root";
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, root);
        var currentRel = SavePathLayout.GetCurrentDirectory();
        fs.SeedFile(fs.CombinePath(root, SavePathLayout.GetProgressFile(currentRel)), "{}");
        fs.SeedFile(fs.CombinePath(root, SavePathLayout.GetProgressStateMachinesFile(currentRel)),
            """{"machines":[]}""");
        var levelDir = SavePathLayout.GetLevelDirectory(currentRel, "default");
        fs.SeedFile(fs.CombinePath(root, SavePathLayout.GetLevelSndSceneFile(levelDir)), "[]");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            SaveStorageFacade.ReadSavePayloadFromCurrent(handle, "001", "default"));
        Assert.Contains("session.json", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveStorageFacade_ReadCurrent_BackgroundLevelPartial_MissingStateMachines_Throws()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var root = "root";
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, root);
        var currentRel = SavePathLayout.GetCurrentDirectory();
        fs.SeedFile(fs.CombinePath(root, SavePathLayout.GetProgressFile(currentRel)), "{}");
        fs.SeedFile(fs.CombinePath(root, SavePathLayout.GetProgressStateMachinesFile(currentRel)),
            """{"machines":[]}""");
        var defDir = SavePathLayout.GetLevelDirectory(currentRel, "default");
        fs.SeedFile(fs.CombinePath(root, SavePathLayout.GetLevelSndSceneFile(defDir)), "[]");
        fs.SeedFile(fs.CombinePath(root, SavePathLayout.GetLevelSessionFile(defDir)), "{}");
        fs.SeedFile(fs.CombinePath(root, SavePathLayout.GetLevelSessionStateMachinesFile(defDir)),
            """{"machines":[]}""");

        var bgDir = SavePathLayout.GetLevelDirectory(currentRel, "bg");
        fs.SeedFile(fs.CombinePath(root, SavePathLayout.GetLevelSndSceneFile(bgDir)), "[]");
        fs.SeedFile(fs.CombinePath(root, SavePathLayout.GetLevelSessionFile(bgDir)), "{}");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            SaveStorageFacade.ReadSavePayloadFromCurrent(handle, "001", "default"));
        Assert.Contains("session_state_machines.json", ex.Message, StringComparison.Ordinal);
        Assert.Contains("bg", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveStorageFacade_ReadCurrent_WhenWriteMarkerExists_Throws()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var root = "root";
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, root);
        var currentRel = SavePathLayout.GetCurrentDirectory();
        fs.SeedFile(fs.CombinePath(root, SavePathLayout.GetProgressFile(currentRel)), "{}");
        fs.SeedFile(fs.CombinePath(root, SavePathLayout.GetProgressStateMachinesFile(currentRel)),
            """{"machines":[]}""");
        var levelDir = SavePathLayout.GetLevelDirectory(currentRel, "default");
        fs.SeedFile(fs.CombinePath(root, SavePathLayout.GetLevelSndSceneFile(levelDir)), "[]");
        fs.SeedFile(fs.CombinePath(root, SavePathLayout.GetLevelSessionFile(levelDir)), "{}");
        fs.SeedFile(fs.CombinePath(root, SavePathLayout.GetLevelSessionStateMachinesFile(levelDir)),
            """{"machines":[]}""");
        fs.SeedFile(fs.CombinePath(root, SavePathLayout.GetWriteInProgressMarker(currentRel)), string.Empty);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            SaveStorageFacade.ReadSavePayloadFromCurrent(handle, "001", "default"));
        Assert.Contains("write-in-progress marker", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SavePayloadReader_TryReadLevelPayloadFromCurrent_AllFilesAbsent_ReturnsNull()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        fs.SeedFile("root/current/progress.json", "{}");
        fs.SeedFile("root/current/progress_state_machines.json", """{"machines":[]}""");

        Assert.Null(SavePayloadReader.TryReadLevelPayloadFromCurrent(handle, "no_such_level"));
    }

    [Fact]
    public void SavePayloadReader_TryReadLevelPayloadFromCurrent_WhenWriteMarkerExists_Throws()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        fs.SeedFile("root/current/level_default/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_default/session.json", "{}");
        fs.SeedFile("root/current/level_default/session_state_machines.json", """{"machines":[]}""");
        var markerRel = SavePathLayout.GetWriteInProgressMarker(SavePathLayout.GetCurrentDirectory());
        fs.SeedFile(fs.CombinePath("root", markerRel), string.Empty);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            SavePayloadReader.TryReadLevelPayloadFromCurrent(handle, "default"));
        Assert.Contains("write-in-progress marker", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultSaveStorageService_ResolveLevelPayload_WhenWriteMarkerExists_Throws()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var service = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");
        fs.SeedFile("root/current/level_default/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_default/session.json", "{}");
        fs.SeedFile("root/current/level_default/session_state_machines.json", """{"machines":[]}""");
        fs.SeedFile("root/save_001/level_default/snd_scene.json", "[]");
        fs.SeedFile("root/save_001/level_default/session.json", "{}");
        fs.SeedFile("root/save_001/level_default/session_state_machines.json", """{"machines":[]}""");
        var markerRel = SavePathLayout.GetWriteInProgressMarker(SavePathLayout.GetCurrentDirectory());
        fs.SeedFile(fs.CombinePath("root", markerRel), string.Empty);

        var ex = Assert.Throws<InvalidOperationException>(() => service.ResolveLevelPayload("001", "default"));
        Assert.Contains("write-in-progress marker", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSavePayloadToCurrentThenSnapshot_NullLogger_Throws()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        var payload = new SaveGamePayload
        {
            SaveId = "1",
            ActiveLevelId = "d",
            ProgressNode = TestFactory.NodeFromJson("{}"),
            ProgressStateMachinesNode = TestFactory.NodeFromJson("""{"machines":[]}"""),
            Levels = new Dictionary<string, LevelPayload>
            {
                ["d"] = new()
                {
                    LevelId = "d",
                    SndSceneNode = TestFactory.NodeFromJson("[]"),
                    SessionNode = TestFactory.NodeFromJson("{}"),
                    SessionStateMachinesNode = TestFactory.NodeFromJson("""{"machines":[]}""")
                }
            }
        };
        Assert.Throws<ArgumentNullException>(() =>
            SaveStorageFacade.WriteSavePayloadToCurrentThenSnapshot(handle, payload, "1", null!));
    }

    [Fact]
    public void WriteSavePayloadToCurrentThenSnapshot_WhenSnapshotFails_LogsError_LeavesMarkerAndUpdatedCurrent()
    {
        var fs = new FailOnCopyFileSystem("save_new.tmp");
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        var logger = new TestLogger();
        var progressSm = """{"machines":[]}""";
        var payload = new SaveGamePayload
        {
            SaveId = "001",
            ActiveLevelId = "default",
            ProgressNode = TestFactory.NodeFromJson("""{"marker":"after_write"}"""),
            ProgressStateMachinesNode = TestFactory.NodeFromJson(progressSm),
            Levels = new Dictionary<string, LevelPayload>
            {
                ["default"] = new()
                {
                    LevelId = "default",
                    SndSceneNode = TestFactory.NodeFromJson("[]"),
                    SessionNode = TestFactory.NodeFromJson("{}"),
                    SessionStateMachinesNode = TestFactory.NodeFromJson(progressSm)
                }
            }
        };

        Assert.Throws<InvalidOperationException>(() =>
            SaveStorageFacade.WriteSavePayloadToCurrentThenSnapshot(handle, payload, "new", logger));

        Assert.NotEmpty(logger.Errors);
        Assert.True(fs.Exists("root/current/progress.json"));
        Assert.Contains("marker", fs.ReadAllText("root/current/progress.json"), StringComparison.Ordinal);
        var markerRel = SavePathLayout.GetWriteInProgressMarker(SavePathLayout.GetCurrentDirectory());
        Assert.True(fs.Exists(fs.CombinePath("root", markerRel)));
        Assert.False(fs.DirectoryExists("root/save_new"));
    }

    [Fact]
    public void SnapshotCurrentToSave_TempToFinalRenameFailsTwice_PreservesPreviousBackup()
    {
        // A failed backup-replace swap leaves the previous snapshot only in
        // save_001.bak. A retry must not delete that backup before the new
        // snapshot is safely installed, or a second failure loses the last
        // known-good snapshot.
        var fs = new FailOnRenameFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");

        fs.SeedFile("root/current/progress.json", """{"v":"old"}""");
        fs.SeedFile("root/current/progress_state_machines.json", """{"machines":[]}""");
        SaveStorageFacade.SnapshotCurrentToSave(handle, "001");

        fs.SeedFile("root/current/progress.json", """{"v":"new"}""");
        fs.FailTempToFinalRename = true;
        Assert.Throws<IOException>(() =>
            SaveStorageFacade.SnapshotCurrentToSave(handle, "001"));

        // Retry while the temp-to-final rename still fails.
        Assert.Throws<IOException>(() =>
            SaveStorageFacade.SnapshotCurrentToSave(handle, "001"));

        Assert.True(fs.DirectoryExists("root/save_001"),
            "the previous known-good snapshot must survive a second failed swap");
        Assert.Equal("""{"v":"old"}""", fs.ReadAllText("root/save_001/progress.json"));
        Assert.False(fs.DirectoryExists("root/save_001.bak"),
            "a successful rollback leaves no backup directory behind");
    }

    [Fact]
    public void SnapshotCurrentToSave_WhenRollbackRenameFails_PreservesBackupAndCanRecover()
    {
        var fs = new FailOnRenameFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");

        fs.SeedFile("root/current/progress.json", """{"v":"old"}""");
        fs.SeedFile("root/current/progress_state_machines.json", """{"machines":[]}""");
        SaveStorageFacade.SnapshotCurrentToSave(handle, "001");

        fs.SeedFile("root/current/progress.json", """{"v":"new"}""");
        fs.FailTempToFinalRename = true;
        fs.FailBackupRollbackRename = true;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            SaveStorageFacade.SnapshotCurrentToSave(handle, "001"));
        Assert.Contains("previous snapshot could not be restored", ex.Message, StringComparison.Ordinal);

        // The old snapshot is preserved only in .bak; it must survive the
        // rollback failure so a later retry can recover.
        Assert.True(fs.DirectoryExists("root/save_001.bak"));
        Assert.Equal("""{"v":"old"}""", fs.ReadAllText("root/save_001.bak/progress.json"));
        Assert.False(fs.DirectoryExists("root/save_001"));
        Assert.True(fs.DirectoryExists("root/save_001.tmp"));

        // A retry after the filesystem recovers installs the new snapshot and
        // removes the backup now that it is no longer the only known-good copy.
        fs.FailTempToFinalRename = false;
        fs.FailBackupRollbackRename = false;
        SaveStorageFacade.SnapshotCurrentToSave(handle, "001");

        Assert.Equal("""{"v":"new"}""", fs.ReadAllText("root/save_001/progress.json"));
        Assert.False(fs.DirectoryExists("root/save_001.bak"));
        Assert.False(fs.DirectoryExists("root/save_001.tmp"));
    }

    [Fact]
    public void SaveStorageFacade_SnapshotCurrentToSave_CleansUpTempOnFailure()
    {
        var fs = new FailOnCopyFileSystem("save_001.tmp");
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        fs.SeedFile("root/current/progress.json", "{}");
        fs.SeedFile("root/current/level_default/snd_scene.json", "[]");

        Assert.Throws<InvalidOperationException>(() =>
            SaveStorageFacade.SnapshotCurrentToSave(handle, "001"));

        // The incomplete .tmp directory must be cleaned up.
        Assert.False(fs.DirectoryExists("root/save_001.tmp"));
    }

    private static (IFileMetaAccess MetaAccess, IDataSourceIoGateway DataSourceIo, IPathResolver PathResolver)
        CreateGateways(IFileSystem fs) =>
        (DataSourceFactory.CreateFileMetaAccess(fs),
         DataSourceFactory.CreateDefaultIoGateway(fs),
         DataSourceFactory.CreatePathResolver(fs));

    /// <summary>
    ///     File system that delegates to <see cref="TestMemoryFileSystem" /> but
    ///     throws when enumerating a directory that does not exist, matching
    ///     the Godot file-system behavior that a missing save root must not be
    ///     enumerated.
    /// </summary>
    private sealed class ThrowOnMissingRootEnumerationFileSystem : IFileSystem
    {
        private readonly TestMemoryFileSystem _inner = new();

        public bool Exists(string path) => _inner.Exists(path);

        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);

        public string ReadAllText(string path) => _inner.ReadAllText(path);

        public void WriteAllText(string path, string content, bool overwrite) =>
            _inner.WriteAllText(path, content, overwrite);

        public void Copy(string sourcePath, string destinationPath, bool overwrite) =>
            _inner.Copy(sourcePath, destinationPath, overwrite);

        public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive) =>
            _inner.EnumerateFiles(directoryPath, searchPattern, recursive);

        public void CreateDirectory(string directoryPath) => _inner.CreateDirectory(directoryPath);

        public void Delete(string path) => _inner.Delete(path);

        public string CombinePath(string basePath, string relativePath) => _inner.CombinePath(basePath, relativePath);

        public string GetParentDirectory(string path) => _inner.GetParentDirectory(path);

        public IEnumerable<string> EnumerateDirectories(string directoryPath) =>
            _inner.DirectoryExists(directoryPath)
                ? _inner.EnumerateDirectories(directoryPath)
                : throw new DirectoryNotFoundException($"Simulated missing directory: {directoryPath}");

        public void Rename(string sourcePath, string destinationPath) => _inner.Rename(sourcePath, destinationPath);

        public void DeleteDirectory(string directoryPath) => _inner.DeleteDirectory(directoryPath);
    }

    /// <summary>
    ///     A path policy whose slot directories use a custom prefix, with the
    ///     interrupted-snapshot enumeration overridden so recovery follows the
    ///     same layout as writes.
    /// </summary>
    private sealed class PrefixedRecoveryPathPolicy : ISavePathPolicy
    {
        private const string _prefix = "custom_save_";
        private readonly DefaultSavePathPolicy _inner = new();

        public string GetCurrentDirectory() => _inner.GetCurrentDirectory();

        public string GetSaveDirectory(string saveId) => $"{_prefix}{saveId}";

        public string GetProgressFile(string baseDirectory) => _inner.GetProgressFile(baseDirectory);

        public string GetProgressStateMachinesFile(string baseDirectory) =>
            _inner.GetProgressStateMachinesFile(baseDirectory);

        public string GetCustomMetaFile(string baseDirectory) => _inner.GetCustomMetaFile(baseDirectory);

        public string GetLevelDirectory(string baseDirectory, string levelId) =>
            $"{baseDirectory}/custom_level_{levelId}";

        public string GetLevelSndSceneFile(string levelDirectory) =>
            _inner.GetLevelSndSceneFile(levelDirectory);

        public string GetLevelSessionFile(string levelDirectory) =>
            _inner.GetLevelSessionFile(levelDirectory);

        public string GetLevelSessionStateMachinesFile(string levelDirectory) =>
            _inner.GetLevelSessionStateMachinesFile(levelDirectory);

        public string GetWriteInProgressMarker(string baseDirectory) =>
            _inner.GetWriteInProgressMarker(baseDirectory);

        public string GetPayloadShaFile(string baseDirectory) => _inner.GetPayloadShaFile(baseDirectory);

        public string GetExtraDirectory(string baseDirectory) => _inner.GetExtraDirectory(baseDirectory);

        public IReadOnlyList<string> EnumerateSaveIds(IFileMetaAccess metaAccess, string saveRootPath)
        {
            var ids = new List<string>();
            foreach (var directory in metaAccess.EnumerateDirectories(saveRootPath))
            {
                var leaf = SaveFileHandle.GetLeafDirectoryName(directory);
                if (!leaf.StartsWith(_prefix, StringComparison.Ordinal)
                    || leaf.EndsWith(SaveStorageFacade.SaveBackupDirectorySuffix, StringComparison.OrdinalIgnoreCase))
                    continue;

                var id = leaf[_prefix.Length..];
                if (id.Length > 0)
                    ids.Add(id);
            }

            ids.Sort(StringComparer.Ordinal);
            return ids;
        }

        public IReadOnlyDictionary<string, string> EnumerateInterruptedSnapshots(
            IFileMetaAccess metaAccess,
            string saveRootPath)
        {
            var snapshots = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var directory in metaAccess.EnumerateDirectories(saveRootPath))
            {
                var leaf = SaveFileHandle.GetLeafDirectoryName(directory);
                if (!leaf.StartsWith(_prefix, StringComparison.Ordinal)
                    || !leaf.EndsWith(SaveStorageFacade.SaveBackupDirectorySuffix, StringComparison.OrdinalIgnoreCase))
                    continue;

                var id = leaf[
                    _prefix.Length..^SaveStorageFacade.SaveBackupDirectorySuffix.Length];
                if (id.Length > 0)
                    snapshots.TryAdd(id, leaf);
            }

            return snapshots;
        }

        public string? TryGetLevelIdFromDirectory(string directoryPath)
        {
            var leaf = SaveFileHandle.GetLeafDirectoryName(directoryPath);
            const string levelPrefix = "custom_level_";
            if (!leaf.StartsWith(levelPrefix, StringComparison.Ordinal))
                return null;

            var id = leaf[levelPrefix.Length..];
            return id.Length == 0 ? null : id;
        }

    }

    /// <summary>
    ///     A file system that delegates to <see cref="TestMemoryFileSystem" /> but can be
    ///     configured to fail the temp-to-final rename in
    ///     <see cref="SaveAtomicWriter.SwapSnapshotDirectory" />. Used to simulate
    ///     an interrupted backup-replace swap.
    /// </summary>
    private sealed class FailOnRenameFileSystem : IFileSystem
    {
        private readonly TestMemoryFileSystem _inner = new();

        public bool FailTempToFinalRename { get; set; }

        public bool FailBackupRollbackRename { get; set; }

        public bool Exists(string path) => _inner.Exists(path);

        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);

        public string ReadAllText(string path) => _inner.ReadAllText(path);

        public void WriteAllText(string path, string content, bool overwrite) =>
            _inner.WriteAllText(path, content, overwrite);

        public void Copy(string sourcePath, string destinationPath, bool overwrite) =>
            _inner.Copy(sourcePath, destinationPath, overwrite);

        public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive) =>
            _inner.EnumerateFiles(directoryPath, searchPattern, recursive);

        public void CreateDirectory(string directoryPath) => _inner.CreateDirectory(directoryPath);

        public void Delete(string path) => _inner.Delete(path);

        public string CombinePath(string basePath, string relativePath) => _inner.CombinePath(basePath, relativePath);

        public string GetParentDirectory(string path) => _inner.GetParentDirectory(path);

        public IEnumerable<string> EnumerateDirectories(string directoryPath) =>
            _inner.EnumerateDirectories(directoryPath);

        public void Rename(string sourcePath, string destinationPath)
        {
            var source = sourcePath.Replace('\\', '/').TrimEnd('/');
            var destination = destinationPath.Replace('\\', '/').TrimEnd('/');
            if (FailTempToFinalRename
                && source.EndsWith("save_001.tmp", StringComparison.Ordinal)
                && destination.EndsWith("save_001", StringComparison.Ordinal))
                throw new IOException($"Simulated rename failure for '{sourcePath}' -> '{destinationPath}'.");
            if (FailBackupRollbackRename
                && source.EndsWith("save_001.bak", StringComparison.Ordinal)
                && destination.EndsWith("save_001", StringComparison.Ordinal))
                throw new IOException($"Simulated rollback rename failure for '{sourcePath}' -> '{destinationPath}'.");

            _inner.Rename(sourcePath, destinationPath);
        }

        public void DeleteDirectory(string directoryPath) => _inner.DeleteDirectory(directoryPath);

        public void SeedFile(string path, string content) => _inner.SeedFile(path, content);
    }

    /// <summary>
    ///     A file system that delegates to <see cref="TestMemoryFileSystem" /> but throws on
    ///     <see cref="IFileSystem.Copy" /> when the destination path contains the configured substring.
    ///     Used to simulate snapshot copy failures.
    /// </summary>
    private sealed class FailOnCopyFileSystem(string failTargetSubstring) : IFileSystem
    {
        private readonly string _failTargetSubstring = failTargetSubstring;
        private readonly TestMemoryFileSystem _inner = new();

        public bool Exists(string path) => _inner.Exists(path);

        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);

        public string ReadAllText(string path) => _inner.ReadAllText(path);

        public void WriteAllText(string path, string content, bool overwrite) =>
            _inner.WriteAllText(path, content, overwrite);

        public void Copy(string sourcePath, string destinationPath, bool overwrite)
        {
            if (destinationPath.Replace('\\', '/').Contains(_failTargetSubstring, StringComparison.Ordinal))
                throw new InvalidOperationException($"Simulated copy failure for '{destinationPath}'.");
            _inner.Copy(sourcePath, destinationPath, overwrite);
        }

        public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive) =>
            _inner.EnumerateFiles(directoryPath, searchPattern, recursive);

        public void CreateDirectory(string directoryPath) => _inner.CreateDirectory(directoryPath);

        public void Delete(string path) => _inner.Delete(path);

        public string CombinePath(string basePath, string relativePath) => _inner.CombinePath(basePath, relativePath);

        public string GetParentDirectory(string path) => _inner.GetParentDirectory(path);

        public IEnumerable<string> EnumerateDirectories(string directoryPath) =>
            _inner.EnumerateDirectories(directoryPath);

        public void Rename(string sourcePath, string destinationPath) => _inner.Rename(sourcePath, destinationPath);

        public void DeleteDirectory(string directoryPath) => _inner.DeleteDirectory(directoryPath);

        public void SeedFile(string path, string content) => _inner.SeedFile(path, content);
    }
}
