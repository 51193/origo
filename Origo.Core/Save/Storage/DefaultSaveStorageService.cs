using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Logging;
using Origo.Core.DataSource;
using Origo.Core.Save.Meta;

namespace Origo.Core.Save.Storage;

/// <summary>
///     <see cref="ISaveStorageService" /> 的默认实现。
///     通过注入 <see cref="ISavePathPolicy" /> 驱动所有路径拼装，确保路径策略可完整替换。
///     内部委托给 <see cref="SavePayloadWriter" /> / <see cref="SavePayloadReader" /> 的策略感知重载，
///     持有 <see cref="IFileSystem" />、saveRootPath 与路径策略，使调用方无需重复传递。
/// </summary>
internal sealed class DefaultSaveStorageService : ISaveStorageService
{
    private readonly IFileSystem _fileSystem;
    private readonly IDataSourceIoGateway _ioGateway;
    private readonly ISavePathPolicy _pathPolicy;
    private readonly string _saveRootPath;

    public DefaultSaveStorageService(IFileSystem fileSystem, string saveRootPath, ISavePathPolicy? pathPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        SaveStorageGatewayFactory.ValidateRootPath(saveRootPath, nameof(saveRootPath),
            "Save root path cannot be null or whitespace.");
        _fileSystem = fileSystem;
        _saveRootPath = saveRootPath;
        _pathPolicy = pathPolicy ?? new DefaultSavePathPolicy();
        _ioGateway = SaveStorageGatewayFactory.CreateIoGateway(fileSystem);
    }

    public IReadOnlyList<string> EnumerateSaveIds() =>
        SaveStorageFacade.EnumerateSaveIds(_fileSystem, _saveRootPath, _pathPolicy);

    public IReadOnlyList<SaveMetaDataEntry> EnumerateSavesWithMetaData() =>
        SaveStorageFacade.EnumerateSavesWithMetaData(_fileSystem, _saveRootPath, _pathPolicy);

    public void WriteSavePayloadToCurrent(SaveGamePayload payload) =>
        SavePayloadWriter.WriteToCurrent(_fileSystem, _ioGateway, _saveRootPath, payload, _pathPolicy);

    public void WriteSavePayloadToCurrentThenSnapshot(
        SaveGamePayload payload,
        string newSaveId,
        ILogger logger)
    {
        SaveStorageFacade.WriteSavePayloadToCurrentThenSnapshot(
            _fileSystem, _ioGateway, _saveRootPath, payload, newSaveId, logger, _pathPolicy);
    }

    public void WriteLevelPayloadOnly(
        string baseDirectoryRel,
        LevelPayload levelPayload,
        bool overwrite = true)
    {
        SavePayloadWriter.WriteLevelPayloadOnly(
            _fileSystem, _ioGateway, _saveRootPath, baseDirectoryRel, levelPayload, _pathPolicy, overwrite);
    }

    public void WriteLevelPayloadOnlyToCurrent(LevelPayload levelPayload, bool overwrite = true)
    {
        var currentRel = _pathPolicy.GetCurrentDirectory();
        WriteLevelPayloadOnly(currentRel, levelPayload, overwrite);
    }

    public void WriteProgressOnlyToCurrent(
        DataSourceNode progressNode,
        DataSourceNode progressStateMachinesNode,
        bool overwrite = true)
    {
        SavePayloadWriter.WriteProgressOnlyToCurrent(
            _fileSystem, _ioGateway, _saveRootPath, progressNode, progressStateMachinesNode, _pathPolicy, overwrite);
    }

    public SaveGamePayload ReadSavePayloadFromCurrent(
        string saveId,
        string activeLevelId,
        ILogger? logger = null)
    {
        return SavePayloadReader.ReadFromCurrent(
            _fileSystem, _ioGateway, _saveRootPath, saveId, activeLevelId, _pathPolicy, logger);
    }

    public SaveGamePayload ReadSavePayloadFromSnapshot(
        string saveId,
        string activeLevelId)
    {
        return SavePayloadReader.ReadFromSnapshot(_fileSystem, _ioGateway, _saveRootPath, saveId, activeLevelId,
            _pathPolicy);
    }

    public DataSourceNode? ReadProgressNodeFromSnapshot(string saveId)
    {
        return SavePayloadReader.ReadProgressNodeFromSnapshot(_fileSystem, _ioGateway, _saveRootPath, saveId,
            _pathPolicy);
    }

    public LevelPayload? TryReadLevelPayloadFromCurrent(string levelId)
    {
        return SavePayloadReader.TryReadLevelPayloadFromCurrent(_fileSystem, _ioGateway, _saveRootPath, levelId,
            _pathPolicy);
    }

    public LevelPayload? TryReadLevelPayloadFromSnapshot(string saveId, string levelId)
    {
        return SavePayloadReader.TryReadLevelPayloadFromSnapshot(
            _fileSystem, _ioGateway, _saveRootPath, saveId, levelId, _pathPolicy);
    }

    public LevelPayload? ResolveLevelPayload(string saveId, string levelId)
    {
        // Priority: current/ first, then snapshot fallback.
        var fromCurrent = TryReadLevelPayloadFromCurrent(levelId);
        if (fromCurrent is not null)
            return fromCurrent;
        return TryReadLevelPayloadFromSnapshot(saveId, levelId);
    }

    public void SnapshotCurrentToSave(string newSaveId) =>
        SaveStorageFacade.SnapshotCurrentToSave(_fileSystem, _saveRootPath, newSaveId, _pathPolicy);

    public void DeleteCurrentDirectory()
    {
        var currentRel = _pathPolicy.GetCurrentDirectory();
        var currentAbs = _fileSystem.CombinePath(_saveRootPath, currentRel);
        if (_fileSystem.DirectoryExists(currentAbs))
            _fileSystem.DeleteDirectory(currentAbs);
    }
}
