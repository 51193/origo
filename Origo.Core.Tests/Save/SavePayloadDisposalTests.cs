using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Logging;
using Origo.Core.DataSource;
using Origo.Core.Save;
using Origo.Core.Save.Meta;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;
using Origo.TestSupport;
using Xunit;

namespace Origo.Core.Tests;

public class SavePayloadDisposalTests
{
    [Fact]
    public void LoadGame_AfterMount_DisposesPayloadNodeTrees()
    {
        var storage = new TrackingSaveStorageService();
        var (ctx, _) = DisposeSemanticsTestInfrastructure.CreateForegroundContext(
            storageService: storage);

        var payload = TestFactory.CreateMinimalPayload("slot", "default");
        storage.PayloadToRead = payload;
        storage.ProgressToRead = TestFactory.NodeFromJson(
            """{"origo.session_topology":{"type":"String","data":"__foreground__=default=false"}}""");

        ctx.Save.RequestLoadGame("slot");
        ctx.FlushFrame();

        var level = payload.Levels["default"];
        AssertNodeDisposed(payload.ProgressNode);
        AssertNodeDisposed(payload.ProgressStateMachinesNode);
        AssertNodeDisposed(level.SndSceneNode);
        AssertNodeDisposed(level.SessionNode);
        AssertNodeDisposed(level.SessionStateMachinesNode);
    }

    [Fact]
    public void LoadInitialSave_AfterMount_DisposesPayloadNodeTrees()
    {
        var runtimeStorage = new TrackingSaveStorageService();
        var initialStorage = new TrackingSaveStorageService();
        var (ctx, _) = DisposeSemanticsTestInfrastructure.CreateForegroundContext(
            storageService: runtimeStorage,
            initialStorageService: initialStorage);

        var payload = TestFactory.CreateMinimalPayload(SndDefaults.InitialSaveId, "default");
        initialStorage.PayloadToRead = payload;

        ctx.Lifecycle.RequestLoadInitialSave();
        ctx.FlushFrame();

        var level = payload.Levels["default"];
        AssertNodeDisposed(payload.ProgressNode);
        AssertNodeDisposed(payload.ProgressStateMachinesNode);
        AssertNodeDisposed(level.SndSceneNode);
        AssertNodeDisposed(level.SessionNode);
        AssertNodeDisposed(level.SessionStateMachinesNode);
    }

    [Fact]
    public void SaveGame_AfterWrite_DisposesBuiltPayloadNodeTrees()
    {
        var storage = new TrackingSaveStorageService();
        var (ctx, _) = DisposeSemanticsTestInfrastructure.CreateForegroundContext(
            storageService: storage);

        ctx.Save.RequestSaveGame("slot");
        ctx.FlushFrame();

        var payload = storage.LastWrittenPayload;
        Assert.NotNull(payload);

        var level = payload!.Levels[payload.ActiveLevelId];
        AssertNodeDisposed(payload.ProgressNode);
        AssertNodeDisposed(payload.ProgressStateMachinesNode);
        AssertNodeDisposed(level.SndSceneNode);
        AssertNodeDisposed(level.SessionNode);
        AssertNodeDisposed(level.SessionStateMachinesNode);
    }

    [Fact]
    public void SwitchForeground_DisposesPersistedAndTargetPayloadNodeTrees()
    {
        var storage = new TrackingSaveStorageService();
        var (ctx, _) = DisposeSemanticsTestInfrastructure.CreateForegroundContext(
            storageService: storage);

        var target = CreateLevelPayload("target_level");
        storage.LevelPayloadToResolve = target;

        ctx.Save.RequestSwitchForegroundLevel("target_level");
        ctx.FlushFrame();

        var persisted = storage.LastWrittenLevelPayload;
        Assert.NotNull(persisted);

        AssertNodeDisposed(persisted!.SndSceneNode);
        AssertNodeDisposed(persisted.SessionNode);
        AssertNodeDisposed(persisted.SessionStateMachinesNode);
        AssertNodeDisposed(target.SndSceneNode);
        AssertNodeDisposed(target.SessionNode);
        AssertNodeDisposed(target.SessionStateMachinesNode);
        AssertNodeDisposed(storage.LastWrittenProgressNode!);
        AssertNodeDisposed(storage.LastWrittenProgressStateMachinesNode!);
    }

    private static LevelPayload CreateLevelPayload(string levelId) =>
        new()
        {
            LevelId = levelId,
            SndSceneNode = TestFactory.NodeFromJson("[]"),
            SessionNode = TestFactory.NodeFromJson("{}"),
            SessionStateMachinesNode = TestFactory.NodeFromJson("""{"machines":[]}""")
        };

    private static void AssertNodeDisposed(DataSourceNode node) =>
        Assert.Throws<ObjectDisposedException>(() => _ = node.Kind);

    private sealed class TrackingSaveStorageService : ISaveStorageService
    {
        public SaveGamePayload? PayloadToRead { get; set; }

        public DataSourceNode? ProgressToRead { get; set; }

        public LevelPayload? LevelPayloadToResolve { get; set; }

        public SaveGamePayload? LastWrittenPayload { get; private set; }

        public LevelPayload? LastWrittenLevelPayload { get; private set; }

        public DataSourceNode? LastWrittenProgressNode { get; private set; }

        public DataSourceNode? LastWrittenProgressStateMachinesNode { get; private set; }

        public IReadOnlyList<string> EnumerateSaveIds() => [];

        public IReadOnlyList<SaveMetaDataEntry> EnumerateSavesWithMetaData() => [];

        public void WriteSavePayloadToCurrent(SaveGamePayload payload) =>
            LastWrittenPayload = payload;

        public void WriteSavePayloadToCurrentThenSnapshot(
            SaveGamePayload payload,
            string newSaveId,
            ILogger logger) =>
            LastWrittenPayload = payload;

        public void WriteLevelPayloadOnlyToCurrent(LevelPayload levelPayload) =>
            LastWrittenLevelPayload = levelPayload;

        public void WriteProgressOnlyToCurrent(
            DataSourceNode progressNode,
            DataSourceNode progressStateMachinesNode)
        {
            LastWrittenProgressNode = progressNode;
            LastWrittenProgressStateMachinesNode = progressStateMachinesNode;
        }

        public SaveGamePayload ReadSavePayloadFromSnapshot(string saveId, string activeLevelId) =>
            PayloadToRead
            ?? throw new InvalidOperationException(
                $"No payload was configured for read of save '{saveId}'.");

        public DataSourceNode? ReadProgressNodeFromSnapshot(string saveId) => ProgressToRead;

        public LevelPayload? TryReadLevelPayloadFromCurrent(string levelId) => LevelPayloadToResolve;

        public LevelPayload? TryReadLevelPayloadFromSnapshot(string saveId, string levelId) =>
            LevelPayloadToResolve;

        public LevelPayload? ResolveLevelPayload(string saveId, string levelId) =>
            LevelPayloadToResolve;

        public void SnapshotCurrentToSave(string newSaveId)
        {
        }

        public void DeleteCurrentDirectory()
        {
        }

        public void RestoreExtraFilesFromSnapshot(string saveId)
        {
        }

        public void RestoreExtraFilesFromSnapshot(
            ISaveStorageService sourceStorage,
            string saveId)
        {
        }
    }
}
