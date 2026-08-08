using System;
using System.Collections.Generic;
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

        SaveStorageFacade.WriteSavePayloadToCurrent(handle, payload);
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
    public void SaveStorageFacade_EnumerateSaveIds_SuffixNamedSlotWithoutRealSlot_IsEnumerated()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        fs.CreateDirectory("root/save_foo.tmp");
        fs.CreateDirectory("root/save_bar");
        fs.CreateDirectory("root/save_bar.tmp");

        var ids = SaveStorageFacade.EnumerateSaveIds(handle);

        // "bar.tmp" is a leftover of an interrupted snapshot (real slot
        // exists); "foo.tmp" is a user-chosen id that ends with the suffix
        // and must stay enumerable.
        Assert.Equal(["bar", "foo.tmp"], ids);
    }

    [Fact]
    public void SaveStorageFacade_EnumerateSaveIds_SuffixOnlyId_DoesNotThrow()
    {
        // A user-chosen id that is itself only the suffix (".tmp") produces
        // the directory save_.tmp; stripping the suffix would yield an empty
        // id, which must not crash the enumeration (there is no valid real
        // slot for an empty id).
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        fs.CreateDirectory("root/save_.tmp");

        var ids = SaveStorageFacade.EnumerateSaveIds(handle);

        Assert.Equal([".tmp"], ids);
    }

    [Fact]
    public void SaveStorageFacade_EnumerateSaveIds_NullFileSystem_Throws() =>
        Assert.Throws<ArgumentNullException>(() => SaveStorageFacade.EnumerateSaveIds(null!));

    [Fact]
    public void SaveStorageFacade_SnapshotCurrentToSave_WhitespaceSaveRoot_Throws()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        Assert.Throws<ArgumentException>(() => new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "  "));
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
    public void SaveStorageFacade_ReadSavePayloadFromSnapshot_WhitespaceSaveRoot_Throws()
    {
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        Assert.Throws<ArgumentException>(() => new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, " "));
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
        SaveStorageFacade.WriteSavePayloadToCurrent(handle, payload);

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
    public void SaveStorageFacade_SnapshotCurrentToSave_CleansUpTempOnFailure()
    {
        var fs = new FailOnCopyFileSystem("save_001.tmp");
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        fs.SeedFile("root/current/progress.json", "{}");
        fs.SeedFile("root/current/level_default/snd_scene.json", "[]");

        Assert.ThrowsAny<Exception>(() =>
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
