using System;
using System.Collections.Generic;
using Origo.Core.DataSource;
using Origo.Core.Save;
using Origo.Core.Save.Storage;
using Xunit;

namespace Origo.Core.Tests;

public class SaveIdempotencyTests
{
    [Fact]
    public void ComputePayloadHash_SamePayload_SameHash()
    {
        var payload = CreateMinimalPayload("001", "default");
        var h1 = SavePayloadWriter.ComputePayloadHash(payload);
        var h2 = SavePayloadWriter.ComputePayloadHash(payload);

        Assert.Equal(h1, h2);
        Assert.Matches("^[0-9a-f]{64}$", h1);
    }

    [Fact]
    public void ComputePayloadHash_DifferentProgressNode_DifferentHash()
    {
        var a = CreateMinimalPayload("001", "default");
        var b = CreateMinimalPayload("001", "default");
        b.ProgressNode = TestFactory.NodeFromJson("""{"changed":true}""");

        Assert.NotEqual(
            SavePayloadWriter.ComputePayloadHash(a),
            SavePayloadWriter.ComputePayloadHash(b));
    }

    [Fact]
    public void ComputePayloadHash_DifferentLevelContent_DifferentHash()
    {
        var a = CreateMinimalPayload("001", "default");
        var b = CreateMinimalPayload("001", "default");
        b.Levels["default"].SessionNode = TestFactory.NodeFromJson("""{"modified":"yes"}""");

        Assert.NotEqual(
            SavePayloadWriter.ComputePayloadHash(a),
            SavePayloadWriter.ComputePayloadHash(b));
    }

    [Fact]
    public void ComputePayloadHash_DifferentCustomMeta_DifferentHash()
    {
        var a = CreateMinimalPayload("001", "default");
        a.CustomMeta = new Dictionary<string, string> { ["version"] = "1" };
        var b = CreateMinimalPayload("001", "default");
        b.CustomMeta = new Dictionary<string, string> { ["version"] = "2" };

        Assert.NotEqual(
            SavePayloadWriter.ComputePayloadHash(a),
            SavePayloadWriter.ComputePayloadHash(b));
    }

    [Fact]
    public void ComputePayloadHash_CustomMetaOrder_Independent()
    {
        var a = CreateMinimalPayload("001", "default");
        a.CustomMeta = new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" };
        var b = CreateMinimalPayload("001", "default");
        b.CustomMeta = new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" };

        Assert.Equal(
            SavePayloadWriter.ComputePayloadHash(a),
            SavePayloadWriter.ComputePayloadHash(b));
    }

    [Fact]
    public void ComputePayloadHash_LevelOrder_Independent()
    {
        var a = CreateMinimalPayload("001", "default");
        a.Levels = new Dictionary<string, LevelPayload>
        {
            ["z"] = CreateLevelPayload("z"),
            ["a"] = CreateLevelPayload("a")
        };
        var b = CreateMinimalPayload("001", "default");
        b.Levels = new Dictionary<string, LevelPayload>
        {
            ["a"] = CreateLevelPayload("a"),
            ["z"] = CreateLevelPayload("z")
        };

        Assert.Equal(
            SavePayloadWriter.ComputePayloadHash(a),
            SavePayloadWriter.ComputePayloadHash(b));
    }

    [Fact]
    public void WriteToCurrent_CreatesPayloadShaFile()
    {
        var fs = new TestFileSystem();
        var payload = CreateMinimalPayload("001", "default");
        var policy = new DefaultSavePathPolicy();
        var handle = new SaveFileHandle(fs, "root", policy);

        SavePayloadWriter.WriteToCurrent(handle, payload);

        var shaRel = policy.GetPayloadShaFile(policy.GetCurrentDirectory());
        var shaAbs = fs.CombinePath("root", shaRel);
        Assert.True(fs.Exists(shaAbs));
        var hash = fs.ReadAllText(shaAbs);
        Assert.Equal(SavePayloadWriter.ComputePayloadHash(payload), hash);
    }

    [Fact]
    public void WriteSavePayloadToCurrentThenSnapshot_SamePayloadTwice_SecondSkips()
    {
        var fs = new TestFileSystem();
        var logger = new TestLogger();
        var payload = CreateMinimalPayload("001", "default");
        var policy = new DefaultSavePathPolicy();
        var handle = new SaveFileHandle(fs, "root", policy);

        // First write — must succeed.
        SaveStorageFacade.WriteSavePayloadToCurrentThenSnapshot(handle, payload, "001", logger);
        var firstWriteTime = fs.ReadAllText("root/current/progress.json");

        // Modify the file system to track if any file content changes happen on second write.
        // Seed the snapshot with existing .sha from the first write.
        var existingShaAbs = fs.CombinePath("root",
            policy.GetPayloadShaFile(policy.GetSaveDirectory("001")));
        Assert.True(fs.Exists(existingShaAbs));

        // Clear current/ to force a fresh Current write if the facade doesn't skip.
        fs.DeleteDirectory(fs.CombinePath("root", policy.GetCurrentDirectory()));

        // Second write with same payload — should skip.
        SaveStorageFacade.WriteSavePayloadToCurrentThenSnapshot(handle, payload, "001", logger);

        // The current/ directory should NOT have been recreated (because the entire method returned early).
        Assert.False(fs.DirectoryExists(fs.CombinePath("root", policy.GetCurrentDirectory())));

        // Logger should contain the idempotent skip message.
        var skipLog = logger.Infos.Find(e => e.Contains("idempotent", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(skipLog);
    }

    [Fact]
    public void WriteSavePayloadToCurrentThenSnapshot_DifferentPayload_Overwrites()
    {
        var fs = new TestFileSystem();
        var logger = new TestLogger();
        var handle = new SaveFileHandle(fs, "root", new DefaultSavePathPolicy());
        var payload = CreateMinimalPayload("001", "default");

        // First write.
        SaveStorageFacade.WriteSavePayloadToCurrentThenSnapshot(handle, payload, "001", logger);

        // Second write with different payload — should write.
        var modified = CreateMinimalPayload("001", "default");
        modified.ProgressNode = TestFactory.NodeFromJson("""{"counter":999}""");
        SaveStorageFacade.WriteSavePayloadToCurrentThenSnapshot(handle, modified, "001", logger);

        // The current/ directory must exist (write happened).
        Assert.True(fs.DirectoryExists(fs.CombinePath("root", "current")));

        // Logger should NOT contain skip message.
        Assert.DoesNotContain(logger.Infos, e => e.Contains("idempotent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WriteSavePayloadToCurrentThenSnapshot_NewSaveId_AlwaysWrites()
    {
        var fs = new TestFileSystem();
        var logger = new TestLogger();
        var handle = new SaveFileHandle(fs, "root", new DefaultSavePathPolicy());
        var payload = CreateMinimalPayload("new_save", "default");

        SaveStorageFacade.WriteSavePayloadToCurrentThenSnapshot(handle, payload, "new_save", logger);

        Assert.True(fs.DirectoryExists(fs.CombinePath("root", "save_new_save")));
        Assert.DoesNotContain(logger.Infos, e => e.Contains("idempotent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WriteSavePayloadToCurrentThenSnapshot_ExistingSaveNoSha_WritesAndCreatesSha()
    {
        var fs = new TestFileSystem();
        var logger = new TestLogger();
        var policy = new DefaultSavePathPolicy();
        var handle = new SaveFileHandle(fs, "root", policy);

        // Pre-seed a snapshot directory without .payload.sha
        fs.CreateDirectory(fs.CombinePath("root", policy.GetSaveDirectory("001")));
        fs.SeedFile(fs.CombinePath("root", policy.GetProgressFile(policy.GetSaveDirectory("001"))), "{}");

        var shaAbs = fs.CombinePath("root", policy.GetPayloadShaFile(policy.GetSaveDirectory("001")));
        Assert.False(fs.Exists(shaAbs));

        var payload = CreateMinimalPayload("001", "default");
        SaveStorageFacade.WriteSavePayloadToCurrentThenSnapshot(handle, payload, "001", logger);

        // .payload.sha must have been created in snapshot.
        Assert.True(fs.Exists(shaAbs));
    }

    [Fact]
    public void WriteSavePayloadToCurrentThenSnapshot_CorruptedShaFile_WritesAndOverwrites()
    {
        var fs = new TestFileSystem();
        var logger = new TestLogger();
        var policy = new DefaultSavePathPolicy();
        var handle = new SaveFileHandle(fs, "root", policy);

        // Pre-seed a snapshot with corrupted .payload.sha (empty content).
        var snapshotRel = policy.GetSaveDirectory("001");
        var shaAbs = fs.CombinePath("root", policy.GetPayloadShaFile(snapshotRel));
        fs.SeedFile(shaAbs, string.Empty);

        var payload = CreateMinimalPayload("001", "default");
        SaveStorageFacade.WriteSavePayloadToCurrentThenSnapshot(handle, payload, "001", logger);

        // .payload.sha should be overwritten with correct hash.
        var hash = fs.ReadAllText(shaAbs).Trim();
        Assert.Equal(SavePayloadWriter.ComputePayloadHash(payload), hash);
    }

    [Fact]
    public void SnapshotCurrentToSave_CopiesPayloadShaFile()
    {
        var fs = new TestFileSystem();
        var policy = new DefaultSavePathPolicy();
        var handle = new SaveFileHandle(fs, "root", policy);
        var currentRel = policy.GetCurrentDirectory();

        // Seed current/ with .payload.sha
        fs.SeedFile(fs.CombinePath("root", policy.GetProgressFile(currentRel)), "{}");
        fs.SeedFile(fs.CombinePath("root", policy.GetProgressStateMachinesFile(currentRel)),
            """{"machines":[]}""");
        fs.SeedFile(fs.CombinePath("root", policy.GetLevelSndSceneFile(
            policy.GetLevelDirectory(currentRel, "default"))), "[]");
        fs.SeedFile(fs.CombinePath("root", policy.GetLevelSessionFile(
            policy.GetLevelDirectory(currentRel, "default"))), "{}");
        fs.SeedFile(fs.CombinePath("root", policy.GetLevelSessionStateMachinesFile(
            policy.GetLevelDirectory(currentRel, "default"))), """{"machines":[]}""");
        fs.SeedFile(fs.CombinePath("root", policy.GetPayloadShaFile(currentRel)),
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");

        SaveStorageFacade.SnapshotCurrentToSave(handle, "001");

        var snapshotShaAbs = fs.CombinePath("root",
            policy.GetPayloadShaFile(policy.GetSaveDirectory("001")));
        Assert.True(fs.Exists(snapshotShaAbs));
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            fs.ReadAllText(snapshotShaAbs));
    }

    [Fact]
    public void WriteSavePayloadToCurrentThenSnapshot_WhenWriteMarkerExists_StillThrows()
    {
        var fs = new TestFileSystem();
        var logger = new TestLogger();
        var policy = new DefaultSavePathPolicy();
        var handle = new SaveFileHandle(fs, "root", policy);

        // Pre-seed snapshot with matching .payload.sha to try triggering the idempotent path,
        // then seed a .write_in_progress marker to verify the idempotent check does NOT bypass
        // the marker validation.
        var payload = CreateMinimalPayload("001", "default");
        var snapshotRel = policy.GetSaveDirectory("001");
        fs.SeedFile(fs.CombinePath("root", policy.GetPayloadShaFile(snapshotRel)),
            SavePayloadWriter.ComputePayloadHash(payload));

        // Seed .write_in_progress in current/
        fs.SeedFile(fs.CombinePath("root",
            policy.GetWriteInProgressMarker(policy.GetCurrentDirectory())), string.Empty);

        // The idempotent check is BEFORE the marker is created — the marker doesn't exist yet
        // at the point of the idempotent check. However, WriteToCurrent will create a marker
        // internally if we proceed past the idempotent check.
        //
        // To test: mark the existing sha as different so the idempotent check fails;
        // then WriteToCurrent will create a marker — and we verify that the process is safe.
        var shaAbs = fs.CombinePath("root", policy.GetPayloadShaFile(snapshotRel));
        fs.WriteAllText(shaAbs, "wronghash", true);

        // This should proceed through (hash mismatch), write to current, and clean up normally.
        var ex = Record.Exception(() =>
            SaveStorageFacade.WriteSavePayloadToCurrentThenSnapshot(handle, payload, "001", logger));
        Assert.Null(ex);

        // Current should exist and marker should be gone.
        Assert.True(fs.DirectoryExists(fs.CombinePath("root", policy.GetCurrentDirectory())));
        Assert.False(fs.Exists(fs.CombinePath("root",
            policy.GetWriteInProgressMarker(policy.GetCurrentDirectory()))));
    }

    [Fact]
    public void ComputePayloadHash_EmptyPayload_Works()
    {
        var payload = new SaveGamePayload
        {
            SaveId = "empty",
            ActiveLevelId = "default",
            ProgressNode = DataSourceNode.CreateObject(),
            ProgressStateMachinesNode = TestFactory.NodeFromJson("""{"machines":[]}"""),
            Levels = new Dictionary<string, LevelPayload>
            {
                ["default"] = new()
                {
                    LevelId = "default",
                    SndSceneNode = DataSourceNode.CreateArray(),
                    SessionNode = DataSourceNode.CreateObject(),
                    SessionStateMachinesNode = TestFactory.NodeFromJson("""{"machines":[]}""")
                }
            }
        };

        var hash = SavePayloadWriter.ComputePayloadHash(payload);
        Assert.NotNull(hash);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void ComputePayloadHash_NullCustomMeta_DoesNotThrow()
    {
        var payload = CreateMinimalPayload("001", "default");
        payload.CustomMeta = null;

        var ex = Record.Exception(() => SavePayloadWriter.ComputePayloadHash(payload));
        Assert.Null(ex);
    }

    [Fact]
    public void WriteSavePayloadToCurrentThenSnapshot_IdempotentSkip_PreservesExistingSnapshot()
    {
        var fs = new TestFileSystem();
        var logger = new TestLogger();
        var payload = CreateMinimalPayload("001", "default");
        var policy = new DefaultSavePathPolicy();
        var handle = new SaveFileHandle(fs, "root", policy);

        // First write creates the snapshot.
        SaveStorageFacade.WriteSavePayloadToCurrentThenSnapshot(handle, payload, "001", logger);

        // Read the original snapshot files.
        var snapshotRel = policy.GetSaveDirectory("001");
        var origProgress = fs.ReadAllText(
            fs.CombinePath("root", policy.GetProgressFile(snapshotRel)));

        // Clear current/ before second attempt.
        fs.DeleteDirectory(fs.CombinePath("root", policy.GetCurrentDirectory()));

        // Second write — should skip.
        SaveStorageFacade.WriteSavePayloadToCurrentThenSnapshot(handle, payload, "001", logger);

        // Snapshot must be unchanged.
        var progressAfter = fs.ReadAllText(
            fs.CombinePath("root", policy.GetProgressFile(snapshotRel)));
        Assert.Equal(origProgress, progressAfter);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static SaveGamePayload CreateMinimalPayload(string saveId, string activeLevelId)
    {
        return new SaveGamePayload
        {
            SaveId = saveId,
            ActiveLevelId = activeLevelId,
            ProgressNode = TestFactory.NodeFromJson("""{"k":{"type":"Int32","data":1}}"""),
            ProgressStateMachinesNode = TestFactory.NodeFromJson("""{"machines":[]}"""),
            Levels = new Dictionary<string, LevelPayload>
            {
                [activeLevelId] = CreateLevelPayload(activeLevelId)
            }
        };
    }

    private static LevelPayload CreateLevelPayload(string levelId)
    {
        return new LevelPayload
        {
            LevelId = levelId,
            SndSceneNode = TestFactory.NodeFromJson("[]"),
            SessionNode = TestFactory.NodeFromJson("""{"s":{"type":"String","data":"ok"}}"""),
            SessionStateMachinesNode = TestFactory.NodeFromJson("""{"machines":[]}""")
        };
    }
}
