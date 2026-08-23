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

    internal SaveFileHandle Handle => _handle;

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

    /// <inheritdoc/>
    public IReadOnlyList<string> EnumerateSaveIds() =>
        SaveStorageFacade.EnumerateSaveIds(_handle);

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public void WriteSavePayloadToCurrentThenSnapshot(
        SaveGamePayload payload,
        string newSaveId,
        ILogger logger)
    {
        SaveStorageFacade.WriteSavePayloadToCurrentThenSnapshot(
            _handle, payload, newSaveId, logger);
    }

    /// <inheritdoc/>
    public void WriteLevelPayloadOnlyToCurrent(LevelPayload levelPayload)
    {
        var currentRel = _handle.PathPolicy.GetCurrentDirectory();
        var markerAbs = SavePayloadWriter.WriteCheckpointMarker(_handle, currentRel);

        // If the write throws, the marker is intentionally left on disk so
        // that readers reject the partially written level checkpoint.
        SavePayloadWriter.WriteLevelPayloadOnly(_handle, currentRel, levelPayload, overwrite: true);

        _handle.MetaAccess.Delete(markerAbs);
    }

    /// <inheritdoc/>
    public void WriteProgressOnlyToCurrent(
        DataSourceNode progressNode,
        DataSourceNode progressStateMachinesNode) => SavePayloadWriter.WriteProgressOnlyToCurrent(_handle, progressNode, progressStateMachinesNode, overwrite: true);

    /// <inheritdoc/>
    public SaveGamePayload ReadSavePayloadFromSnapshot(
        string saveId,
        string activeLevelId) => SavePayloadReader.ReadFromSnapshot(_handle, saveId, activeLevelId);

    /// <inheritdoc/>
    public DataSourceNode? ReadProgressNodeFromSnapshot(string saveId) => SavePayloadReader.ReadProgressNodeFromSnapshot(_handle, saveId);

    /// <inheritdoc/>
    public LevelPayload? TryReadLevelPayloadFromCurrent(string levelId) => SavePayloadReader.TryReadLevelPayloadFromCurrent(_handle, levelId);

    /// <inheritdoc/>
    public LevelPayload? TryReadLevelPayloadFromSnapshot(string saveId, string levelId) => SavePayloadReader.TryReadLevelPayloadFromSnapshot(_handle, saveId, levelId);

    /// <inheritdoc/>
    public LevelPayload? ResolveLevelPayload(string saveId, string levelId)
    {
        var fromCurrent = TryReadLevelPayloadFromCurrent(levelId);
        if (fromCurrent is not null)
            return fromCurrent;
        return TryReadLevelPayloadFromSnapshot(saveId, levelId);
    }

    /// <inheritdoc/>
    public void SnapshotCurrentToSave(string newSaveId) =>
        SaveStorageFacade.SnapshotCurrentToSave(_handle, newSaveId);

    /// <inheritdoc/>
    public void DeleteCurrentDirectory()
    {
        var currentRel = _handle.PathPolicy.GetCurrentDirectory();
        var currentAbs = _handle.GetAbsolutePath(currentRel);
        if (_handle.MetaAccess.DirectoryExists(currentAbs))
            _handle.MetaAccess.DeleteDirectory(currentAbs);
    }

    /// <inheritdoc/>
    public void RestoreExtraFilesFromSnapshot(string saveId)
    {
        SaveStorageFacade.CopyDirectoryFromSnapshot(
            _handle, saveId, SavePathLayout.ExtraDirectoryName);
    }

    /// <inheritdoc/>
    public void RestoreExtraFilesFromSnapshot(
        ISaveStorageService sourceStorage,
        string saveId)
    {
        ArgumentNullException.ThrowIfNull(sourceStorage);
        if (sourceStorage is not DefaultSaveStorageService source)
            throw new InvalidOperationException(
                "DefaultSaveStorageService can only restore extra files from another " +
                "DefaultSaveStorageService. Custom storage services must be paired with a " +
                "custom destination implementation that understands their snapshot layout.");

        SaveStorageFacade.CopyDirectoryFromSnapshot(
            source.Handle,
            saveId,
            SavePathLayout.ExtraDirectoryName,
            _handle);
    }
}
