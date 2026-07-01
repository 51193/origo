using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Snd;
using Origo.Core.DataSource;
using Origo.Core.Save;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public class SaveExtraFilesRoundTripTests
{
    private static (IFileMetaAccess MetaAccess, IDataSourceIoGateway DataSourceIo, IPathResolver PathResolver)
        CreateGateways(IFileSystem fs) =>
        (DataSourceFactory.CreateFileMetaAccess(fs),
         DataSourceFactory.CreateDefaultIoGateway(fs),
         DataSourceFactory.CreatePathResolver(fs));

    // ── CopyDirectoryFromSnapshot low-level tests ──────────────────────

    [Fact]
    public void CopyDirectoryFromSnapshot_SeededFiles_AllCopiedToCurrent()
    {
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");

        fs.SeedFile("root/save_001/extra/config.json", """{"key":"value"}""");
        fs.SeedFile("root/save_001/extra/data.txt", "hello world");

        SaveStorageFacade.CopyDirectoryFromSnapshot(handle, "001", "extra");

        Assert.True(fs.Exists("root/current/extra/config.json"));
        Assert.True(fs.Exists("root/current/extra/data.txt"));
        Assert.Equal("""{"key":"value"}""", fs.ReadAllText("root/current/extra/config.json"));
        Assert.Equal("hello world", fs.ReadAllText("root/current/extra/data.txt"));
    }

    [Fact]
    public void CopyDirectoryFromSnapshot_SubdirectoryStructurePreserved()
    {
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");

        fs.SeedFile("root/save_001/extra/nested/a.json", "1");
        fs.SeedFile("root/save_001/extra/nested/deep/b.json", "2");

        SaveStorageFacade.CopyDirectoryFromSnapshot(handle, "001", "extra");

        Assert.True(fs.Exists("root/current/extra/nested/a.json"));
        Assert.True(fs.Exists("root/current/extra/nested/deep/b.json"));
        Assert.Equal("1", fs.ReadAllText("root/current/extra/nested/a.json"));
        Assert.Equal("2", fs.ReadAllText("root/current/extra/nested/deep/b.json"));
    }

    [Fact]
    public void CopyDirectoryFromSnapshot_SourceDirectoryDoesNotExist_ReturnsSilently()
    {
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");

        var ex = Record.Exception(() =>
            SaveStorageFacade.CopyDirectoryFromSnapshot(handle, "001", "extra"));

        Assert.Null(ex);
        Assert.False(fs.DirectoryExists("root/current/extra"));
    }

    [Fact]
    public void CopyDirectoryFromSnapshot_EmptySourceDirectory_DoesNothing()
    {
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");

        fs.CreateDirectory("root/save_001/extra");

        SaveStorageFacade.CopyDirectoryFromSnapshot(handle, "001", "extra");

        Assert.True(fs.DirectoryExists("root/current/extra"));
        Assert.Empty(fs.EnumerateFiles("root/current/extra", "*", true));
    }

    [Fact]
    public void CopyDirectoryFromSnapshot_NullHandle_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            SaveStorageFacade.CopyDirectoryFromSnapshot(null!, "001", "extra"));
    }

    [Fact]
    public void CopyDirectoryFromSnapshot_EmptySaveId_Throws()
    {
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");

        Assert.Throws<ArgumentException>(() =>
            SaveStorageFacade.CopyDirectoryFromSnapshot(handle, "", "extra"));
        Assert.Throws<ArgumentException>(() =>
            SaveStorageFacade.CopyDirectoryFromSnapshot(handle, "  ", "extra"));
    }

    [Fact]
    public void CopyDirectoryFromSnapshot_EmptyDirName_Throws()
    {
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");

        Assert.Throws<ArgumentException>(() =>
            SaveStorageFacade.CopyDirectoryFromSnapshot(handle, "001", ""));
        Assert.Throws<ArgumentException>(() =>
            SaveStorageFacade.CopyDirectoryFromSnapshot(handle, "001", "  "));
    }

    [Fact]
    public void CopyDirectoryFromSnapshot_ExistingFilesInCurrent_Overwrites()
    {
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");

        fs.SeedFile("root/save_001/extra/overlap.json", "from_snapshot");
        fs.SeedFile("root/current/extra/overlap.json", "existing");

        SaveStorageFacade.CopyDirectoryFromSnapshot(handle, "001", "extra");

        Assert.Equal("from_snapshot", fs.ReadAllText("root/current/extra/overlap.json"));
    }

    // ── Full save/load round-trip through SndContext ──────────────────

    [Fact]
    public void ExtraFiles_FullSaveLoadRoundTrip_PreservesMultipleFiles()
    {
        var ctx = CreateContextWithEntry(out var fs);
        var archive = (ISndArchiveFileAccess)ctx;

        var objNode = DataSourceNode.CreateObject()
            .Add("game_data", DataSourceNode.CreateString("persisted"));
        archive.WriteFile("state.json", objNode);

        Assert.True(archive.FileExists("state.json"), "File should exist after write");

        var arrNode = DataSourceNode.CreateArray()
            .Add(DataSourceNode.CreateNumber(1))
            .Add(DataSourceNode.CreateNumber(2));
        archive.WriteFile("sequence.json", arrNode);

        ctx.RequestSaveGame("slot_01");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists("root/save_slot_01/extra/state.json"), "state.json should be in snapshot");
        Assert.True(fs.Exists("root/save_slot_01/extra/sequence.json"), "sequence.json should be in snapshot");

        ctx.RequestLoadGame("slot_01");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.True(archive.FileExists("state.json"), "state.json should exist after reload");
        var readObj = archive.ReadFile("state.json");
        Assert.Equal("persisted", readObj["game_data"].AsString());

        var readArr = archive.ReadFile("sequence.json");
        Assert.Equal(2, readArr.Count);
        Assert.Equal(1, readArr[0].AsInt());
        Assert.Equal(2, readArr[1].AsInt());
    }

    [Fact]
    public void ExtraFiles_SaveLoadRoundTrip_SubdirectoryPreserved()
    {
        var ctx = CreateContextWithEntry(out var fs);
        var archive = (ISndArchiveFileAccess)ctx;

        var node = DataSourceNode.CreateObject()
            .Add("data", DataSourceNode.CreateString("nested_content"));
        archive.WriteFile("sub/dir/file.json", node);

        ctx.RequestSaveGame("slot_01");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists("root/save_slot_01/extra/sub/dir/file.json"));

        ctx.RequestLoadGame("slot_01");
        ctx.FlushDeferredActionsForCurrentFrame();

        var readBack = archive.ReadFile("sub/dir/file.json");
        Assert.Equal("nested_content", readBack["data"].AsString());
    }

    [Fact]
    public void ExtraFiles_SaveTwice_SameSlot_HasLatestContent()
    {
        var ctx = CreateContextWithEntry(out _);
        var archive = (ISndArchiveFileAccess)ctx;

        var v1 = DataSourceNode.CreateObject()
            .Add("version", DataSourceNode.CreateNumber(1));
        archive.WriteFile("data.json", v1);
        ctx.RequestSaveGame("slot_01");
        ctx.FlushDeferredActionsForCurrentFrame();

        var v2 = DataSourceNode.CreateObject()
            .Add("version", DataSourceNode.CreateNumber(2));
        archive.WriteFile("data.json", v2);
        ctx.RequestSaveGame("slot_01");
        ctx.FlushDeferredActionsForCurrentFrame();

        ctx.RequestLoadGame("slot_01");
        ctx.FlushDeferredActionsForCurrentFrame();

        var readBack = archive.ReadFile("data.json");
        Assert.Equal(2, readBack["version"].AsInt());
    }

    [Fact]
    public void ExtraFiles_DifferentContent_DifferentCombinedHash()
    {
        var ctx = CreateContextWithEntry(out var fs);
        var archive = (ISndArchiveFileAccess)ctx;

        var node = DataSourceNode.CreateObject()
            .Add("data", DataSourceNode.CreateString("v1"));
        archive.WriteFile("state.json", node);
        ctx.RequestSaveGame("slot_01");
        ctx.FlushDeferredActionsForCurrentFrame();

        var sha1 = fs.ReadAllText("root/save_slot_01/.payload.sha");

        archive.WriteFile("state.json",
            DataSourceNode.CreateObject().Add("data", DataSourceNode.CreateString("v2")));
        ctx.RequestSaveGame("slot_01");
        ctx.FlushDeferredActionsForCurrentFrame();

        var sha2 = fs.ReadAllText("root/save_slot_01/.payload.sha");

        Assert.NotEqual(sha1, sha2);
    }

    [Fact]
    public void ExtraFiles_LoadWithoutExtra_DoesNotThrowAndPreviousStateCleared()
    {
        var ctx = CreateContextWithEntry(out var fs);
        var archive = (ISndArchiveFileAccess)ctx;

        var node = DataSourceNode.CreateObject()
            .Add("marker", DataSourceNode.CreateString("before_save"));
        archive.WriteFile("temp.json", node);
        ctx.RequestSaveGame("slot_01");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists("root/save_slot_01/extra/temp.json"));

        ctx.RequestLoadGame("slot_01");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.True(archive.FileExists("temp.json"));
    }

    [Fact]
    public void ExtraFiles_SaveLoadRoundTrip_TypeDataRoundTrip_PreservesNumbers()
    {
        var ctx = CreateContextWithEntry(out _);
        var archive = (ISndArchiveFileAccess)ctx;

        archive.WriteObject("numbers.json", 42);
        archive.WriteObject("flag.json", true);
        archive.WriteObject("text.json", "hello");

        ctx.RequestSaveGame("slot_01");
        ctx.FlushDeferredActionsForCurrentFrame();

        ctx.RequestLoadGame("slot_01");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.Equal(42, archive.ReadObject<int>("numbers.json"));
        Assert.True(archive.ReadObject<bool>("flag.json"));
        Assert.Equal("hello", archive.ReadObject<string>("text.json"));
    }

    [Fact]
    public void ExtraFiles_DeleteFileThenSave_FileNotInSnapshot()
    {
        var ctx = CreateContextWithEntry(out var fs);
        var archive = (ISndArchiveFileAccess)ctx;

        var node = DataSourceNode.CreateObject()
            .Add("tmp", DataSourceNode.CreateString("to_delete"));
        archive.WriteFile("remove_me.json", node);

        archive.DeleteFile("remove_me.json");

        ctx.RequestSaveGame("slot_01");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.False(fs.Exists("root/save_slot_01/extra/remove_me.json"));
    }

    // ── ComputeSideDirectoryHash / CombineHashes unit tests ──────────

    [Fact]
    public void ComputeSideDirectoryHash_NoExtraDir_ReturnsEmpty()
    {
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        fs.CreateDirectory("root/current");

        var hash = SaveStorageFacade.ComputeSideDirectoryHash(handle, "extra");
        Assert.Equal(string.Empty, hash);
    }

    [Fact]
    public void ComputeSideDirectoryHash_EmptyExtraDir_ReturnsEmpty()
    {
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        fs.CreateDirectory("root/current/extra");

        var hash = SaveStorageFacade.ComputeSideDirectoryHash(handle, "extra");
        Assert.Equal(string.Empty, hash);
    }

    [Fact]
    public void ComputeSideDirectoryHash_WithFiles_ReturnsNonEmpty()
    {
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        fs.SeedFile("root/current/extra/a.json", """{"k":"v"}""");
        fs.SeedFile("root/current/extra/b.json", """{"x":1}""");

        var hash = SaveStorageFacade.ComputeSideDirectoryHash(handle, "extra");
        Assert.NotEmpty(hash);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void ComputeSideDirectoryHash_SameContent_SameHash()
    {
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        fs.SeedFile("root/current/extra/data.json", """{"k":"v"}""");

        var h1 = SaveStorageFacade.ComputeSideDirectoryHash(handle, "extra");
        var h2 = SaveStorageFacade.ComputeSideDirectoryHash(handle, "extra");
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void ComputeSideDirectoryHash_DifferentContent_DifferentHash()
    {
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        fs.SeedFile("root/current/extra/data.json", """{"k":"v1"}""");

        var h1 = SaveStorageFacade.ComputeSideDirectoryHash(handle, "extra");

        fs.Delete("root/current/extra/data.json");
        fs.SeedFile("root/current/extra/data.json", """{"k":"v2"}""");

        var h2 = SaveStorageFacade.ComputeSideDirectoryHash(handle, "extra");
        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void ComputeSideDirectoryHash_CustomDirectoryName_Works()
    {
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        fs.SeedFile("root/current/custom/file.json", """{"v":1}""");

        var hash = SaveStorageFacade.ComputeSideDirectoryHash(handle, "custom");
        Assert.NotEmpty(hash);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void ComputeSideDirectoryHash_CustomDirectory_Empty_ReturnsEmpty()
    {
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        fs.CreateDirectory("root/current/custom");

        var hash = SaveStorageFacade.ComputeSideDirectoryHash(handle, "custom");
        Assert.Equal(string.Empty, hash);
    }

    [Fact]
    public void CombineHashes_EmptySide_ProducesConsistentFormat()
    {
        var payloadHash = "abc123";
        var combined = SaveStorageFacade.CombineHashes(payloadHash, string.Empty);
        Assert.NotEqual(payloadHash, combined);
        Assert.Matches("^[0-9a-f]{64}$", combined);
    }

    [Fact]
    public void CombineHashes_SamePayload_EmptyAndNonEmptySide_DifferentResult()
    {
        var payloadHash = "abc123";
        var emptySide = SaveStorageFacade.CombineHashes(payloadHash, string.Empty);
        var withSide = SaveStorageFacade.CombineHashes(payloadHash, "def456");
        Assert.NotEqual(emptySide, withSide);
    }

    [Fact]
    public void CombineHashes_WithExtra_DifferentFromPayloadHash()
    {
        var payloadHash = "abc123";
        var extraHash = "def456";
        var combined = SaveStorageFacade.CombineHashes(payloadHash, extraHash);
        Assert.NotEqual(payloadHash, combined);
        Assert.Matches("^[0-9a-f]{64}$", combined);
    }

    [Fact]
    public void IdempotentSkip_UnchangedPayloadAndExtra_SkipHappens()
    {
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var logger = new TestLogger();
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root", new DefaultSavePathPolicy());

        var h1 = SavePayloadWriter.ComputePayloadHash(TestFactory.CreateMinimalPayload("001", "default"));
        var h2 = SavePayloadWriter.ComputePayloadHash(TestFactory.CreateMinimalPayload("001", "default"));
        Assert.Equal(h1, h2);

        SaveStorageFacade.WriteSavePayloadToCurrentThenSnapshot(handle, TestFactory.CreateMinimalPayload("001", "default"), "001", logger);
        SaveStorageFacade.WriteSavePayloadToCurrentThenSnapshot(handle, TestFactory.CreateMinimalPayload("001", "default"), "001", logger);

        Assert.Contains("Idempotent save skip", logger.Infos.Last());
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static SndContext CreateContextWithEntry(out TestFileSystem fs)
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var tm = new TypeStringMapping();
        var bb = new Blackboard.Blackboard();
        fs = new TestFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, tm, bb, fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial", "entry.json"));
        fs.SeedFile("entry.json", "[]");
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();
        return ctx;
    }
}
