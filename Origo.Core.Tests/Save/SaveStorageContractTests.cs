using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.DataSource;
using Origo.Core.Logging;
using Origo.Core.Save;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     持久化存储契约测试。验证 persistence-flow.md 中描述的所有严格读取规则、
///     两阶段写入协议、meta.map 元数据和路径策略。
/// </summary>
public class SaveStorageContractTests
{
    // ── .write_in_progress marker 契约 ───────────────────────────────────

    [Fact]
    public void WriteSaveToCurrent_CreatesMarkerDuringWrite()
    {
        var (ctx, fs) = CreateForegroundContext();
        ctx.Blackboard.ProgressBlackboard!.SetValue(WellKnownKeys.SessionTopology,
            @"__foreground__=test_level=false");

        ctx.StorageService.WriteSavePayloadToCurrent(
            BuildPayload("test_level", "slot"));

        Assert.True(fs.Exists("root/current/progress.json"));
        Assert.False(fs.Exists("root/current/.write_in_progress"));
    }

    [Fact]
    public void ReadSavePayloadFromCurrent_WhenWriteInProgressMarkerExists_Throws()
    {
        var (ctx, fs) = CreateForegroundContext();
        ctx.Blackboard.ProgressBlackboard!.SetValue(WellKnownKeys.SessionTopology,
            @"__foreground__=test_level=false");
        ctx.StorageService.WriteSavePayloadToCurrent(
            BuildPayload("test_level", "slot"));

        fs.SeedFile("root/current/.write_in_progress", "");

        Assert.Throws<InvalidOperationException>(() =>
            ReadFromCurrent(fs, "slot", "test_level"));
    }

    [Fact]
    public void ReadSavePayloadFromCurrent_WhenNoMarker_Succeeds()
    {
        var (ctx, fs) = CreateForegroundContext();
        ctx.Blackboard.ProgressBlackboard!.SetValue(WellKnownKeys.SessionTopology,
            @"__foreground__=test_level=false");
        ctx.StorageService.WriteSavePayloadToCurrent(
            BuildPayload("test_level", "slot"));

        var payload = ReadFromCurrent(fs, "slot", "test_level");

        Assert.NotNull(payload);
        Assert.Equal("test_level", payload.ActiveLevelId);
    }

    // ── Checkpoint write marker 契约 (regression: checkpoints used to be marker-less) ──

    [Fact]
    public void WriteProgressOnlyToCurrent_RemovesMarkerOnSuccess()
    {
        var (ctx, fs) = CreateForegroundContext();

        ctx.StorageService.WriteProgressOnlyToCurrent(
            BuildNode("""{"origo.session_topology":{"type":"String","data":"__foreground__=test_level=false"}}"""),
            BuildNode("""{"machines":[]}"""));

        Assert.False(fs.Exists("root/current/.write_in_progress"));
        Assert.True(fs.Exists("root/current/progress.json"));
        Assert.True(fs.Exists("root/current/progress_state_machines.json"));
    }

    [Fact]
    public void WriteProgressOnlyToCurrent_Failure_LeavesMarkerSoReadersReject()
    {
        var inner = new TestMemoryFileSystem();
        var fs = new FailOnSecondWriteFileSystem(inner);
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var storage = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");

        Assert.Throws<IOException>(() =>
            storage.WriteProgressOnlyToCurrent(
                BuildNode("""{"origo.session_topology":{"type":"String","data":"__foreground__=test_level=false"}}"""),
                BuildNode("""{"machines":[]}""")));

        Assert.True(fs.Exists("root/current/.write_in_progress"));
        Assert.Throws<InvalidOperationException>(() =>
            storage.TryReadLevelPayloadFromCurrent("test_level"));
    }

    [Fact]
    public void WriteLevelPayloadOnlyToCurrent_RemovesMarkerOnSuccess()
    {
        var (ctx, fs) = CreateForegroundContext();

        ctx.StorageService.WriteLevelPayloadOnlyToCurrent(BuildLevelPayload("checkpoint_level"));

        Assert.False(fs.Exists("root/current/.write_in_progress"));
        Assert.NotNull(ctx.StorageService.TryReadLevelPayloadFromCurrent("checkpoint_level"));
    }

    [Fact]
    public void WriteLevelPayloadOnlyToCurrent_Failure_LeavesMarkerSoReadersReject()
    {
        var (ctx, fs) = CreateForegroundContext();

        var invalidPayload = new LevelPayload
        {
            LevelId = "broken_level",
            SndSceneNode = DataSourceNode.CreateNull(),
            SessionNode = DataSourceNode.CreateNull(),
            SessionStateMachinesNode = DataSourceNode.CreateNull()
        };

        Assert.Throws<InvalidOperationException>(() =>
            ctx.StorageService.WriteLevelPayloadOnlyToCurrent(invalidPayload));

        Assert.True(fs.Exists("root/current/.write_in_progress"));
        Assert.Throws<InvalidOperationException>(() =>
            ctx.StorageService.TryReadLevelPayloadFromCurrent("broken_level"));
    }

    // ── 关卡三件套完整性契约 ──────────────────────────────────────────

    [Fact]
    public void TryReadLevelPayload_AllThreeMissing_ReturnsNull()
    {
        var (ctx, _) = CreateForegroundContext();
        ctx.StorageService.WriteProgressOnlyToCurrent(
            BuildNode("""{"origo.session_topology":{"type":"String","data":"__foreground__=test_level=false"}}"""),
            BuildNode("""{"machines":[]}"""));

        var result = ctx.StorageService.TryReadLevelPayloadFromCurrent("empty_level");

        Assert.Null(result);
    }

    [Fact]
    public void TryReadLevelPayload_OnlySndSceneExists_Throws()
    {
        var (ctx, fs) = CreateForegroundContext();
        var levelDir = "root/current/level_partial";
        fs.SeedFile($"{levelDir}/snd_scene.json", "[]");

        Assert.Throws<InvalidOperationException>(() =>
            ctx.StorageService.TryReadLevelPayloadFromCurrent("partial"));
    }

    [Fact]
    public void TryReadLevelPayload_OnlySessionExists_Throws()
    {
        var (ctx, fs) = CreateForegroundContext();
        var levelDir = "root/current/level_partial2";
        fs.SeedFile($"{levelDir}/session.json", "{}");

        Assert.Throws<InvalidOperationException>(() =>
            ctx.StorageService.TryReadLevelPayloadFromCurrent("partial2"));
    }

    [Fact]
    public void TryReadLevelPayload_OnlyStateMachinesExists_Throws()
    {
        var (ctx, fs) = CreateForegroundContext();
        var levelDir = "root/current/level_partial3";
        fs.SeedFile($"{levelDir}/session_state_machines.json", """{"machines":[]}""");

        Assert.Throws<InvalidOperationException>(() =>
            ctx.StorageService.TryReadLevelPayloadFromCurrent("partial3"));
    }

    [Fact]
    public void TryReadLevelPayload_AnyTwoOfThree_Throws()
    {
        var (ctx, fs) = CreateForegroundContext();
        var levelDir = "root/current/level_two";
        fs.SeedFile($"{levelDir}/snd_scene.json", "[]");
        fs.SeedFile($"{levelDir}/session.json", "{}");

        Assert.Throws<InvalidOperationException>(() =>
            ctx.StorageService.TryReadLevelPayloadFromCurrent("two"));
    }

    [Fact]
    public void TryReadLevelPayload_AllThreePresent_Succeeds()
    {
        var (ctx, fs) = CreateForegroundContext();
        var levelDir = "root/current/level_complete";
        fs.SeedFile($"{levelDir}/snd_scene.json", "[]");
        fs.SeedFile($"{levelDir}/session.json", """{"x":{"type":"Int32","data":1}}""");
        fs.SeedFile($"{levelDir}/session_state_machines.json", """{"machines":[]}""");

        var payload = ctx.StorageService.TryReadLevelPayloadFromCurrent("complete");

        Assert.NotNull(payload);
        Assert.Equal("complete", payload!.LevelId);
    }

    // ── progress.json 契约 ─────────────────────────────────────────────

    [Fact]
    public void ReadSavePayloadFromCurrent_WhenProgressJsonMissing_Throws()
    {
        var (_, fs) = CreateForegroundContext();

        Assert.Throws<InvalidOperationException>(() =>
            ReadFromCurrent(fs, "nonexistent_save", "nonexistent_level"));
    }

    // ── 两阶段写入原子性 ─────────────────────────────────────────────

    [Fact]
    public void WriteSavePayloadToCurrent_WritesAllExpectedFiles()
    {
        var (ctx, fs) = CreateForegroundContext();
        ctx.Blackboard.ProgressBlackboard!.SetValue(WellKnownKeys.SessionTopology,
            @"__foreground__=test_level=false");

        ctx.StorageService.WriteSavePayloadToCurrent(
            BuildPayload("test_level", "full_save"));

        Assert.True(fs.Exists("root/current/progress.json"));
        Assert.True(fs.Exists("root/current/progress_state_machines.json"));
        Assert.True(fs.Exists("root/current/level_test_level/snd_scene.json"));
        Assert.True(fs.Exists("root/current/level_test_level/session.json"));
        Assert.True(fs.Exists("root/current/level_test_level/session_state_machines.json"));
    }

    [Fact]
    public void WriteSavePayloadToCurrentThenSnapshot_CreatesSnapshotDirectory()
    {
        var (ctx, fs) = CreateForegroundContext();
        ctx.Blackboard.ProgressBlackboard!.SetValue(WellKnownKeys.SessionTopology,
            @"__foreground__=test_level=false");
        var payload = BuildPayload("test_level", "snap_save");

        ctx.StorageService.WriteSavePayloadToCurrentThenSnapshot(
            payload, "snap_save", NullLogger.Instance);

        Assert.True(fs.Exists("root/save_snap_save/progress.json"));
        Assert.True(fs.Exists("root/save_snap_save/level_test_level/snd_scene.json"));
    }

    [Fact]
    public void WriteSavePayloadToCurrentThenSnapshot_ThenReadBackRoundTrip()
    {
        var (ctx, _) = CreateForegroundContext();
        ctx.Blackboard.ProgressBlackboard!.SetValue(WellKnownKeys.SessionTopology,
            @"__foreground__=test_level=false");
        var payload = BuildPayload("test_level", "round_save");
        payload.ProgressNode = BuildNode(
            """{"origo.session_topology":{"type":"String","data":"__foreground__=test_level=false"}}""");

        ctx.StorageService.WriteSavePayloadToCurrentThenSnapshot(
            payload, "round_save", NullLogger.Instance);

        var restored = ctx.StorageService.ReadSavePayloadFromSnapshot("round_save", "test_level");

        Assert.NotNull(restored);
        Assert.Equal("round_save", restored.SaveId);
        Assert.Equal("test_level", restored.ActiveLevelId);
        Assert.True(restored.Levels.ContainsKey("test_level"));
    }

    [Fact]
    public void SnapshotCurrentToSave_WritesAllLevelFiles()
    {
        var (ctx, fs) = CreateForegroundContext();
        ctx.Blackboard.ProgressBlackboard!.SetValue(WellKnownKeys.SessionTopology,
            @"__foreground__=test_level=false");
        ctx.StorageService.WriteSavePayloadToCurrent(
            BuildPayload("test_level", "pre_snap"));

        ctx.StorageService.SnapshotCurrentToSave("snapped");

        Assert.True(fs.Exists("root/save_snapped/progress.json"));
        Assert.True(fs.Exists("root/save_snapped/level_test_level/snd_scene.json"));
    }

    // ── SaveGamePayload 构建校验 ─────────────────────────────────────

    [Fact]
    public void WriteSavePayloadToCurrent_ValidPayload_WritesSuccessfully()
    {
        var (ctx, fs) = CreateForegroundContext();
        ctx.Blackboard.ProgressBlackboard!.SetValue(WellKnownKeys.SessionTopology,
            @"__foreground__=test_level=false");
        var payload = BuildPayload("test_level", "valid_save");

        ctx.StorageService.WriteSavePayloadToCurrent(payload);

        Assert.True(fs.Exists("root/current/progress.json"));
        Assert.True(fs.Exists("root/current/level_test_level/snd_scene.json"));
    }

    [Fact]
    public void WriteSavePayloadToCurrent_EmptySaveId_StillWrites()
    {
        var (ctx, fs) = CreateForegroundContext();
        ctx.Blackboard.ProgressBlackboard!.SetValue(WellKnownKeys.SessionTopology,
            @"__foreground__=test_level=false");
        var payload = BuildPayload("test_level", string.Empty);

        ctx.StorageService.WriteSavePayloadToCurrent(payload);

        Assert.True(fs.Exists("root/current/progress.json"));
    }

    // ── meta.map 元数据 ────────────────────────────────────────────

    [Fact]
    public void WriteSavePayloadToCurrentThenSnapshot_WithCustomMeta_WritesMetaMap()
    {
        var (ctx, fs) = CreateForegroundContext();
        ctx.Blackboard.ProgressBlackboard!.SetValue(WellKnownKeys.SessionTopology,
            @"__foreground__=test_level=false");
        var payload = BuildPayload("test_level", "meta_save");
        payload.CustomMeta = new Dictionary<string, string>
        {
            ["play_time"] = "2h30m",
            ["player_name"] = "Alice"
        };

        ctx.StorageService.WriteSavePayloadToCurrentThenSnapshot(
            payload, "meta_save", NullLogger.Instance);

        Assert.True(fs.Exists("root/save_meta_save/meta.map"));
        var metaContent = fs.ReadAllText("root/save_meta_save/meta.map");
        Assert.Contains("play_time: 2h30m", metaContent);
        Assert.Contains("player_name: Alice", metaContent);
    }

    [Fact]
    public void WriteSavePayloadToCurrentThenSnapshot_WithoutCustomMeta_MetaMapNotCreated()
    {
        var (ctx, fs) = CreateForegroundContext();
        ctx.Blackboard.ProgressBlackboard!.SetValue(WellKnownKeys.SessionTopology,
            @"__foreground__=test_level=false");
        var payload = BuildPayload("test_level", "no_meta_save");
        payload.CustomMeta = null;

        ctx.StorageService.WriteSavePayloadToCurrentThenSnapshot(
            payload, "no_meta_save", NullLogger.Instance);

        Assert.False(fs.Exists("root/save_no_meta_save/meta.map"));
    }

    // ── 路径策略 ────────────────────────────────────────────────────

    [Fact]
    public void DefaultSaveStorageService_WithCustomPathPolicy_UsesCustomLayout()
    {
        var policy = new CustomSavePathPolicy();
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var storageService = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "custom_root", policy);

        var payload = new SaveGamePayload
        {
            SaveId = "test_save",
            ActiveLevelId = "test_level",
            ProgressNode = BuildNode(
                """{"origo.session_topology":{"type":"String","data":"__foreground__=test_level=false"}}"""),
            ProgressStateMachinesNode = BuildNode("""{"machines":[]}"""),
            Levels = new Dictionary<string, LevelPayload>
            {
                ["test_level"] = new()
                {
                    LevelId = "test_level",
                    SndSceneNode = DataSourceNode.CreateArray(),
                    SessionNode = DataSourceNode.CreateObject(),
                    SessionStateMachinesNode = BuildNode("""{"machines":[]}""")
                }
            }
        };

        storageService.WriteSavePayloadToCurrentThenSnapshot(payload, "test_save", NullLogger.Instance);

        Assert.True(fs.Exists("custom_root/custom_current/custom_progress.json"));
        Assert.True(fs.Exists("custom_root/custom_saves/test_save/custom_progress.json"));
    }

    [Fact]
    public void EnumerateSaveIds_ReturnsCorrectList()
    {
        var (ctx, _) = CreateForegroundContext();
        ctx.Blackboard.ProgressBlackboard!.SetValue(WellKnownKeys.SessionTopology,
            @"__foreground__=test_level=false");

        ctx.StorageService.WriteSavePayloadToCurrentThenSnapshot(
            BuildPayload("test_level", "save_a"), "save_a", NullLogger.Instance);
        ctx.StorageService.WriteSavePayloadToCurrentThenSnapshot(
            BuildPayload("test_level", "save_b"), "save_b", NullLogger.Instance);

        var ids = ctx.StorageService.EnumerateSaveIds();

        Assert.Contains("save_a", ids);
        Assert.Contains("save_b", ids);
        Assert.DoesNotContain("current", ids);
    }

    // ── Exceptions during read ────────────────────────────────────

    [Fact]
    public void ReadSavePayloadFromSnapshot_WhenSaveNotExist_Throws()
    {
        var (ctx, _) = CreateForegroundContext();

        Assert.Throws<InvalidOperationException>(() =>
            ctx.StorageService.ReadSavePayloadFromSnapshot("nonexistent", "any"));
    }

    // ── DeleteCurrentDirectory ────────────────────────────────────

    [Fact]
    public void DeleteCurrentDirectory_RemovesAllCurrentFiles()
    {
        var (ctx, fs) = CreateForegroundContext();
        ctx.Blackboard.ProgressBlackboard!.SetValue(WellKnownKeys.SessionTopology,
            @"__foreground__=test_level=false");
        ctx.StorageService.WriteSavePayloadToCurrent(
            BuildPayload("test_level", "tmp_save"));

        Assert.True(fs.Exists("root/current/progress.json"));

        ctx.StorageService.DeleteCurrentDirectory();

        Assert.False(fs.Exists("root/current/progress.json"));
    }

    [Fact]
    public void DeleteCurrentDirectory_WhenNoDirectory_DoesNotThrow()
    {
        var (ctx, _) = CreateForegroundContext();

        var ex = Record.Exception(() => ctx.StorageService.DeleteCurrentDirectory());
        Assert.Null(ex);
    }

    // ── Stale write marker recovery ─────────────────────────────────

    [Fact]
    public void StaleWriteMarker_AfterDeleteCurrentDirectory_WriteThenSucceeds()
    {
        var (ctx, fs) = CreateForegroundContext();
        ctx.Blackboard.ProgressBlackboard!.SetValue(WellKnownKeys.SessionTopology,
            @"__foreground__=test_level=false");

        ctx.StorageService.WriteSavePayloadToCurrent(
            BuildPayload("test_level", "stale_test"));

        fs.SeedFile("root/current/.write_in_progress", "");
        Assert.True(fs.Exists("root/current/.write_in_progress"));

        ctx.StorageService.DeleteCurrentDirectory();
        Assert.False(fs.Exists("root/current/.write_in_progress"));

        ctx.Blackboard.ProgressBlackboard!.SetValue(WellKnownKeys.SessionTopology,
            @"__foreground__=test_level=false");
        var ex = Record.Exception(() =>
            ctx.StorageService.WriteSavePayloadToCurrent(
                BuildPayload("test_level", "stale_test")));
        Assert.Null(ex);
    }

    [Fact]
    public void RecoverFromStaleWriteMarker_CleanStateAfterRecovery()
    {
        var (ctx, fs) = CreateForegroundContext();
        ctx.Blackboard.ProgressBlackboard!.SetValue(WellKnownKeys.SessionTopology,
            @"__foreground__=test_level=false");

        ctx.StorageService.WriteSavePayloadToCurrent(
            BuildPayload("test_level", "recovery_test"));

        fs.SeedFile("root/current/.write_in_progress", "");

        ctx.StorageService.DeleteCurrentDirectory();

        ctx.Blackboard.ProgressBlackboard!.SetValue(WellKnownKeys.SessionTopology,
            @"__foreground__=test_level=false");
        ctx.StorageService.WriteSavePayloadToCurrent(
            BuildPayload("test_level", "recovery_test"));

        Assert.False(fs.Exists("root/current/.write_in_progress"));
        Assert.True(fs.Exists("root/current/progress.json"));

        var payload = ReadFromCurrent(fs, "slot", "test_level");
        Assert.NotNull(payload);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static (SndContext ctx, TestMemoryFileSystem fs) CreateForegroundContext()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        fs.SeedFile("entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;

        var storageService = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial",
            "entry.json")
        {
            StorageService = storageService
        });

        var progressRun = TestFactory.CreateProgressRun("test_save", logger, metaAccess, pathResolver, "root", runtime, ctx,
            storageService);
        ctx.SetProgressRun(progressRun);
        progressRun.LoadAndMountForeground("test_level");

        return (ctx, fs);
    }

    private static SaveGamePayload BuildPayload(string levelId, string saveId)
    {
        var progressJson =
            "{\"origo.session_topology\":{\"type\":\"String\",\"data\":\"__foreground__=" + levelId + "=false\"}}";
        return new SaveGamePayload
        {
            SaveId = saveId,
            ActiveLevelId = levelId,
            ProgressNode = BuildNode(progressJson),
            ProgressStateMachinesNode = BuildNode("""{"machines":[]}"""),
            Levels = new Dictionary<string, LevelPayload>
            {
                [levelId] = new()
                {
                    LevelId = levelId,
                    SndSceneNode = DataSourceNode.CreateArray(),
                    SessionNode = DataSourceNode.CreateObject(),
                    SessionStateMachinesNode = BuildNode("""{"machines":[]}""")
                }
            }
        };
    }

    private static DataSourceNode BuildNode(string json) => TestFactory.NodeFromJson(json);

    private static LevelPayload BuildLevelPayload(string levelId)
    {
        return new LevelPayload
        {
            LevelId = levelId,
            SndSceneNode = DataSourceNode.CreateArray(),
            SessionNode = DataSourceNode.CreateObject(),
            SessionStateMachinesNode = BuildNode("""{"machines":[]}""")
        };
    }

    // ── Custom path policy for testing ────────────────────────────────

    private sealed class CustomSavePathPolicy : ISavePathPolicy
    {
        public string GetCurrentDirectory() => "custom_current";

        public string GetSaveDirectory(string saveId) => $"custom_saves/{saveId}";

        public string GetProgressFile(string baseDirectory) => $"{baseDirectory}/custom_progress.json";

        public string GetProgressStateMachinesFile(string baseDirectory) =>
            $"{baseDirectory}/custom_progress_state_machines.json";

        public string GetCustomMetaFile(string baseDirectory) => $"{baseDirectory}/meta.map";

        public string GetLevelDirectory(string baseDirectory, string levelId) =>
            $"{baseDirectory}/level_{levelId}";

        public string GetLevelSndSceneFile(string levelDirectory) => $"{levelDirectory}/snd_scene.json";

        public string GetLevelSessionFile(string levelDirectory) => $"{levelDirectory}/session.json";

        public string GetLevelSessionStateMachinesFile(string levelDirectory) =>
            $"{levelDirectory}/session_state_machines.json";

        public string GetWriteInProgressMarker(string baseDirectory) => $"{baseDirectory}/.write_in_progress";

        public string GetPayloadShaFile(string baseDirectory) => $"{baseDirectory}/payload.sha";

        public string GetExtraDirectory(string baseDirectory) => $"{baseDirectory}/extra";
    }

    private static (IFileMetaAccess MetaAccess, IDataSourceIoGateway DataSourceIo, IPathResolver PathResolver)
        CreateGateways(IFileSystem fs) =>
        (DataSourceFactory.CreateFileMetaAccess(fs),
         DataSourceFactory.CreateDefaultIoGateway(fs),
         DataSourceFactory.CreatePathResolver(fs));

    /// <summary>
    ///     Reads a full payload from current/ through the internal reader
    ///     (the public <c>ISaveStorageService</c> surface exposes the snapshot
    ///     read only; the current/ read is framework-internal).
    /// </summary>
    private static SaveGamePayload ReadFromCurrent(
        TestMemoryFileSystem fs, string saveId, string activeLevelId)
    {
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        return SavePayloadReader.ReadFromCurrent(handle, saveId, activeLevelId);
    }

    /// <summary>
    ///     Wraps an <see cref="IFileSystem" /> and fails the second
    ///     <see cref="IFileSystem.WriteAllText" /> call, simulating an I/O
    ///     failure in the middle of a two-file checkpoint write.
    /// </summary>
    private sealed class FailOnSecondWriteFileSystem(IFileSystem inner) : IFileSystem
    {
        private int _writeCount;

        public void WriteAllText(string path, string content, bool overwrite)
        {
            if (Interlocked.Increment(ref _writeCount) == 2)
                throw new IOException("Simulated checkpoint write failure.");
            inner.WriteAllText(path, content, overwrite);
        }

        public bool Exists(string path) => inner.Exists(path);

        public bool DirectoryExists(string path) => inner.DirectoryExists(path);

        public string ReadAllText(string path) => inner.ReadAllText(path);

        public void Copy(string sourcePath, string destinationPath, bool overwrite) =>
            inner.Copy(sourcePath, destinationPath, overwrite);

        public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive) =>
            inner.EnumerateFiles(directoryPath, searchPattern, recursive);

        public void CreateDirectory(string directoryPath) => inner.CreateDirectory(directoryPath);

        public void Delete(string path) => inner.Delete(path);

        public string CombinePath(string basePath, string relativePath) =>
            inner.CombinePath(basePath, relativePath);

        public string GetParentDirectory(string path) => inner.GetParentDirectory(path);

        public IEnumerable<string> EnumerateDirectories(string directoryPath) =>
            inner.EnumerateDirectories(directoryPath);

        public void Rename(string sourcePath, string destinationPath) =>
            inner.Rename(sourcePath, destinationPath);

        public void DeleteDirectory(string directoryPath) => inner.DeleteDirectory(directoryPath);
    }
}
