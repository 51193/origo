using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Logging;
using Origo.Core.DataSource;
using Origo.Core.Save.Meta;

namespace Origo.Core.Save.Storage;

/// <summary>
///     Default implementation of <see cref="ISaveStorageService" />.
///     Holds I/O dependencies through a single <see cref="SaveFileHandle" />
///     and delegates to <see cref="SavePayloadWriter" /> /
///     <see cref="SavePayloadReader" /> / <see cref="SaveStorageFacade" />,
///     freeing callers from repeating I/O parameter passing.
/// </summary>
internal sealed class DefaultSaveStorageService : ISaveStorageService
{
    private readonly SaveFileHandle _handle;

    public DefaultSaveStorageService(
        IFileMetaAccess metaAccess,
        IDataSourceIoGateway ioGateway,
        IPathResolver pathResolver,
        string saveRootPath,
        ISavePathPolicy? pathPolicy = null)
    {
        SaveFileHandle.ValidateRootPath(saveRootPath, nameof(saveRootPath),
            "Save root path cannot be null or whitespace.");
        _handle = new SaveFileHandle(metaAccess, ioGateway, pathResolver, saveRootPath, pathPolicy);
    }

    public IReadOnlyList<string> EnumerateSaveIds() =>
        SaveStorageFacade.EnumerateSaveIds(_handle);

    public IReadOnlyList<SaveMetaDataEntry> EnumerateSavesWithMetaData() =>
        SaveStorageFacade.EnumerateSavesWithMetaData(_handle);

    /// <summary>
    ///     Writes a payload to current/ without touching .payload.sha: this
    ///     is the load-recovery path, where the snapshot that follows is
    ///     deduplicated via the idempotent-skip hash written by
    ///     <see cref="WriteSavePayloadToCurrentThenSnapshot" />. A recovery
    ///     write has no idempotency contract of its own.
    /// </summary>
    public void WriteSavePayloadToCurrent(SaveGamePayload payload) =>
        SavePayloadWriter.WriteToCurrent(_handle, payload);

    public void WriteSavePayloadToCurrentThenSnapshot(
        SaveGamePayload payload,
        string newSaveId,
        ILogger logger)
    {
        SaveStorageFacade.WriteSavePayloadToCurrentThenSnapshot(
            _handle, payload, newSaveId, logger);
    }

    public void WriteLevelPayloadOnly(
        string baseDirectoryRel,
        LevelPayload levelPayload,
        bool overwrite = true) => SavePayloadWriter.WriteLevelPayloadOnly(_handle, baseDirectoryRel, levelPayload, overwrite);

    public void WriteLevelPayloadOnlyToCurrent(LevelPayload levelPayload, bool overwrite = true)
    {
        var currentRel = _handle.PathPolicy.GetCurrentDirectory();
        var markerRel = _handle.PathPolicy.GetWriteInProgressMarker(currentRel);
        var markerAbs = _handle.GetAbsolutePath(markerRel);
        _handle.MetaAccess.CreateDirectory(_handle.GetAbsolutePath(currentRel));
        _handle.IoGateway.WriteTree(markerAbs, DataSourceNode.CreateString(""));

        // If the write throws, the marker is intentionally left on disk so
        // that readers reject the partially written level checkpoint.
        WriteLevelPayloadOnly(currentRel, levelPayload, overwrite);

        _handle.MetaAccess.Delete(markerAbs);
    }

    public void WriteProgressOnlyToCurrent(
        DataSourceNode progressNode,
        DataSourceNode progressStateMachinesNode,
        bool overwrite = true) => SavePayloadWriter.WriteProgressOnlyToCurrent(_handle, progressNode, progressStateMachinesNode, overwrite);

    public SaveGamePayload ReadSavePayloadFromCurrent(
        string saveId,
        string activeLevelId,
        ILogger? logger = null) => SavePayloadReader.ReadFromCurrent(_handle, saveId, activeLevelId, logger);

    public SaveGamePayload ReadSavePayloadFromSnapshot(
        string saveId,
        string activeLevelId) => SavePayloadReader.ReadFromSnapshot(_handle, saveId, activeLevelId);

    public DataSourceNode? ReadProgressNodeFromSnapshot(string saveId) => SavePayloadReader.ReadProgressNodeFromSnapshot(_handle, saveId);

    public LevelPayload? TryReadLevelPayloadFromCurrent(string levelId) => SavePayloadReader.TryReadLevelPayloadFromCurrent(_handle, levelId);

    public LevelPayload? TryReadLevelPayloadFromSnapshot(string saveId, string levelId) => SavePayloadReader.TryReadLevelPayloadFromSnapshot(_handle, saveId, levelId);

    public LevelPayload? ResolveLevelPayload(string saveId, string levelId)
    {
        var fromCurrent = TryReadLevelPayloadFromCurrent(levelId);
        if (fromCurrent is not null)
            return fromCurrent;
        return TryReadLevelPayloadFromSnapshot(saveId, levelId);
    }

    public void SnapshotCurrentToSave(string newSaveId) =>
        SaveStorageFacade.SnapshotCurrentToSave(_handle, newSaveId);

    public void DeleteCurrentDirectory()
    {
        var currentRel = _handle.PathPolicy.GetCurrentDirectory();
        var currentAbs = _handle.GetAbsolutePath(currentRel);
        if (_handle.MetaAccess.DirectoryExists(currentAbs))
            _handle.MetaAccess.DeleteDirectory(currentAbs);
    }

    public void RestoreExtraFilesFromSnapshot(string saveId)
    {
        SaveStorageFacade.CopyDirectoryFromSnapshot(
            _handle, saveId, SavePathLayout.ExtraDirectoryName);
    }
}
