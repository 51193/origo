using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Logging;
using Origo.Core.DataSource;
using Origo.Core.Save.Meta;

namespace Origo.Core.Save.Storage;

/// <summary>
///     <see cref="ISaveStorageService" /> 的默认实现。
///     通过 <see cref="SaveFileHandle" /> 统一持有 I/O 依赖，委托给
///     <see cref="SavePayloadWriter" /> / <see cref="SavePayloadReader" /> /
///     <see cref="SaveStorageFacade" />，使调用方无需重复传递 I/O 参数。
/// </summary>
internal sealed class DefaultSaveStorageService : ISaveStorageService
{
    private readonly SaveFileHandle _handle;

    public DefaultSaveStorageService(IFileSystem fileSystem, string saveRootPath, ISavePathPolicy? pathPolicy = null)
    {
        SaveFileHandle.ValidateRootPath(saveRootPath, nameof(saveRootPath),
            "Save root path cannot be null or whitespace.");
        _handle = new SaveFileHandle(fileSystem, saveRootPath, pathPolicy);
    }

    public IReadOnlyList<string> EnumerateSaveIds() =>
        SaveStorageFacade.EnumerateSaveIds(_handle);

    public IReadOnlyList<SaveMetaDataEntry> EnumerateSavesWithMetaData() =>
        SaveStorageFacade.EnumerateSavesWithMetaData(_handle);

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
        bool overwrite = true)
    {
        SavePayloadWriter.WriteLevelPayloadOnly(_handle, baseDirectoryRel, levelPayload, overwrite);
    }

    public void WriteLevelPayloadOnlyToCurrent(LevelPayload levelPayload, bool overwrite = true)
    {
        var currentRel = _handle.PathPolicy.GetCurrentDirectory();
        WriteLevelPayloadOnly(currentRel, levelPayload, overwrite);
    }

    public void WriteProgressOnlyToCurrent(
        DataSourceNode progressNode,
        DataSourceNode progressStateMachinesNode,
        bool overwrite = true)
    {
        SavePayloadWriter.WriteProgressOnlyToCurrent(_handle, progressNode, progressStateMachinesNode, overwrite);
    }

    public SaveGamePayload ReadSavePayloadFromCurrent(
        string saveId,
        string activeLevelId,
        ILogger? logger = null)
    {
        return SavePayloadReader.ReadFromCurrent(_handle, saveId, activeLevelId, logger);
    }

    public SaveGamePayload ReadSavePayloadFromSnapshot(
        string saveId,
        string activeLevelId)
    {
        return SavePayloadReader.ReadFromSnapshot(_handle, saveId, activeLevelId);
    }

    public DataSourceNode? ReadProgressNodeFromSnapshot(string saveId)
    {
        return SavePayloadReader.ReadProgressNodeFromSnapshot(_handle, saveId);
    }

    public LevelPayload? TryReadLevelPayloadFromCurrent(string levelId)
    {
        return SavePayloadReader.TryReadLevelPayloadFromCurrent(_handle, levelId);
    }

    public LevelPayload? TryReadLevelPayloadFromSnapshot(string saveId, string levelId)
    {
        return SavePayloadReader.TryReadLevelPayloadFromSnapshot(_handle, saveId, levelId);
    }

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
        if (_handle.FileSystem.DirectoryExists(currentAbs))
            _handle.FileSystem.DeleteDirectory(currentAbs);
    }

    public void RestoreExtraFilesFromSnapshot(string saveId)
    {
        SaveStorageFacade.CopyDirectoryFromSnapshot(
            _handle, saveId, SavePathLayout.ExtraDirectoryName);
    }
}
