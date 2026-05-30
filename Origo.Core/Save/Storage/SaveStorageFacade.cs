using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Logging;
using Origo.Core.DataSource;
using Origo.Core.Logging;
using Origo.Core.Save.Meta;

namespace Origo.Core.Save.Storage;

/// <summary>
///     存档 I/O 门面：严格模式（early stage）实现。
///     仅负责编排文件读写与目录布局，具体 payload 解析/序列化委托给 Reader/Writer。
/// </summary>
internal static class SaveStorageFacade
{
    /// <summary>Directory name prefix for on-disk save slots (e.g. <c>save_001</c>).</summary>
    public const string SaveDirectoryPrefix = "save_";

    /// <summary>
    ///     枚举存档根目录下所有合法的存档槽 ID，按字典序排序返回。
    /// </summary>
    public static IReadOnlyList<string> EnumerateSaveIds(IFileSystem fileSystem, string saveRootPath) =>
        EnumerateSaveIds(fileSystem, saveRootPath, new DefaultSavePathPolicy());

    /// <summary>
    ///     枚举存档根目录下所有合法的存档槽 ID，按字典序排序返回（策略感知）。
    /// </summary>
    public static IReadOnlyList<string> EnumerateSaveIds(
        IFileSystem fileSystem, string saveRootPath, ISavePathPolicy pathPolicy)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(pathPolicy);
        SaveStorageGatewayFactory.ValidateRootPath(saveRootPath, nameof(saveRootPath),
            "Save root path cannot be null or whitespace.");

        if (!fileSystem.DirectoryExists(saveRootPath))
            return Array.Empty<string>();

        var result = new List<string>();
        foreach (var dir in fileSystem.EnumerateDirectories(saveRootPath))
        {
            var leaf = SavePathResolver.GetLeafDirectoryName(dir);
            if (!leaf.StartsWith(SaveDirectoryPrefix, StringComparison.Ordinal))
                continue;
            var id = leaf.Substring(SaveDirectoryPrefix.Length);
            if (id.Length == 0)
                continue;
            result.Add(id);
        }

        result.Sort(StringComparer.Ordinal);
        return result;
    }

    /// <summary>
    ///     枚举所有存档槽并读取各自的展示元数据（meta.map）。
    /// </summary>
    public static IReadOnlyList<SaveMetaDataEntry> EnumerateSavesWithMetaData(IFileSystem fileSystem,
        string saveRootPath) =>
        EnumerateSavesWithMetaData(fileSystem, saveRootPath, new DefaultSavePathPolicy());

    /// <summary>
    ///     枚举所有存档槽并读取各自的展示元数据（meta.map）（策略感知）。
    /// </summary>
    public static IReadOnlyList<SaveMetaDataEntry> EnumerateSavesWithMetaData(
        IFileSystem fileSystem, string saveRootPath, ISavePathPolicy pathPolicy)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(pathPolicy);
        SaveStorageGatewayFactory.ValidateRootPath(saveRootPath, nameof(saveRootPath),
            "Save root path cannot be null or whitespace.");

        var dataSourceIo = SaveStorageGatewayFactory.CreateIoGateway(fileSystem);
        var ids = EnumerateSaveIds(fileSystem, saveRootPath, pathPolicy);
        var list = new List<SaveMetaDataEntry>(ids.Count);
        foreach (var id in ids)
        {
            var saveRel = pathPolicy.GetSaveDirectory(id);
            var metaRel = pathPolicy.GetCustomMetaFile(saveRel);
            var metaAbs = fileSystem.CombinePath(saveRootPath, metaRel);
            var meta = SavePayloadReader.TryReadStringMap(fileSystem, dataSourceIo, metaAbs) ??
                       new Dictionary<string, string>();
            list.Add(new SaveMetaDataEntry { SaveId = id, MetaData = meta });
        }

        return list;
    }

    /// <summary>
    ///     将存档 payload 写入 <c>current/</c> 活动目录。
    /// </summary>
    public static void
        WriteSavePayloadToCurrent(IFileSystem fileSystem, string saveRootPath, SaveGamePayload payload)
    {
        WriteSavePayloadToCurrent(fileSystem, SaveStorageGatewayFactory.CreateIoGateway(fileSystem), saveRootPath,
            payload);
    }

    public static void
        WriteSavePayloadToCurrent(IFileSystem fileSystem, IDataSourceIoGateway dataSourceIo, string saveRootPath,
            SaveGamePayload payload) =>
        SavePayloadWriter.WriteToCurrent(fileSystem, dataSourceIo, saveRootPath, payload);

    /// <summary>
    ///     将存档 payload 写入 <c>current/</c> 活动目录（策略感知）。
    /// </summary>
    public static void
        WriteSavePayloadToCurrent(IFileSystem fileSystem, IDataSourceIoGateway dataSourceIo, string saveRootPath,
            SaveGamePayload payload,
            ISavePathPolicy pathPolicy) =>
        SavePayloadWriter.WriteToCurrent(fileSystem, dataSourceIo, saveRootPath, payload, pathPolicy);

    /// <summary>
    ///     将存档 payload 写入 <c>current/</c> 后，再将 <c>current/</c> 快照复制到 <c>save_{newSaveId}/</c>。
    ///     该两步操作<strong>非原子</strong>：若第二步失败，<c>current/</c> 可能已更新而快照目录未创建，
    ///     活动存档槽与磁盘状态可能不一致；调用方应依赖日志并视需要人工恢复或重试。
    /// </summary>
    public static void WriteSavePayloadToCurrentThenSnapshot(
        IFileSystem fileSystem,
        string saveRootPath,
        SaveGamePayload payload,
        string newSaveId,
        ILogger logger)
    {
        WriteSavePayloadToCurrentThenSnapshot(fileSystem, SaveStorageGatewayFactory.CreateIoGateway(fileSystem),
            saveRootPath, payload, newSaveId, logger, new DefaultSavePathPolicy());
    }

    public static void WriteSavePayloadToCurrentThenSnapshot(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        SaveGamePayload payload,
        string newSaveId,
        ILogger logger)
    {
        WriteSavePayloadToCurrentThenSnapshot(fileSystem, dataSourceIo, saveRootPath, payload, newSaveId, logger,
            new DefaultSavePathPolicy());
    }

    /// <summary>
    ///     将存档 payload 写入 <c>current/</c> 后，再将 <c>current/</c> 快照复制到 <c>save_{newSaveId}/</c>（策略感知）。
    /// </summary>
    public static void WriteSavePayloadToCurrentThenSnapshot(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        SaveGamePayload payload,
        string newSaveId,
        ILogger logger,
        ISavePathPolicy pathPolicy)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(pathPolicy);

        // 幂等性去重：若目标快照已存在且内容 hash 未变，直接返回。
        var snapshotDirRel = pathPolicy.GetSaveDirectory(newSaveId);
        var snapshotShaRel = pathPolicy.GetPayloadShaFile(snapshotDirRel);
        var snapshotShaAbs = fileSystem.CombinePath(saveRootPath, snapshotShaRel);
        if (fileSystem.Exists(snapshotShaAbs))
        {
            string existingHash;
            try
            {
                existingHash = fileSystem.ReadAllText(snapshotShaAbs).Trim();
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Warning, nameof(SaveStorageFacade),
                    $"Failed to read existing payload SHA for save '{newSaveId}'; will overwrite. {ex.Message}");
                existingHash = string.Empty;
            }

            var newHash = SavePayloadWriter.ComputePayloadHash(payload);
            if (existingHash.Length > 0
                && string.Equals(existingHash, newHash, StringComparison.Ordinal))
            {
                logger.Log(LogLevel.Info, nameof(SaveStorageFacade),
                    $"Idempotent save skip: payload hash unchanged for save '{newSaveId}'.");
                return;
            }
        }

        var currentRel = pathPolicy.GetCurrentDirectory();
        var markerRel = pathPolicy.GetWriteInProgressMarker(currentRel);
        var markerAbs = fileSystem.CombinePath(saveRootPath, markerRel);
        var currentAbs = fileSystem.CombinePath(saveRootPath, currentRel);
        fileSystem.CreateDirectory(currentAbs);
        fileSystem.WriteAllText(markerAbs, "", true);

        WriteSavePayloadToCurrent(fileSystem, dataSourceIo, saveRootPath, payload, pathPolicy);

        // WriteToCurrent deletes the marker; re-establish for the snapshot phase
        fileSystem.WriteAllText(markerAbs, "", true);

        try
        {
            SnapshotCurrentToSave(fileSystem, saveRootPath, newSaveId, pathPolicy, logger);
        }
        catch (InvalidOperationException ex)
        {
            logger.Log(LogLevel.Error, nameof(SaveStorageFacade),
                new LogMessageBuilder()
                    .AddSuffix("saveRootPath", saveRootPath)
                    .AddSuffix("newSaveId", newSaveId)
                    .Build(
                        $"Snapshot failed after current/ was written; save index and disk may be inconsistent. {ex.Message}"));
            throw;
        }

        fileSystem.Delete(markerAbs);
    }

    /// <summary>
    ///     仅写入单个关卡的 payload 数据到指定基目录下。
    /// </summary>
    public static void WriteLevelPayloadOnly(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string baseDirectoryRel,
        LevelPayload levelPayload,
        bool overwrite = true)
    {
        SavePayloadWriter.WriteLevelPayloadOnly(fileSystem, dataSourceIo, saveRootPath, baseDirectoryRel, levelPayload,
            overwrite);
    }

    /// <summary>
    ///     仅写入单个关卡的 payload 数据到指定基目录下（策略感知）。
    /// </summary>
    public static void WriteLevelPayloadOnly(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string baseDirectoryRel,
        LevelPayload levelPayload,
        ISavePathPolicy pathPolicy,
        bool overwrite = true)
    {
        SavePayloadWriter.WriteLevelPayloadOnly(fileSystem, dataSourceIo, saveRootPath, baseDirectoryRel, levelPayload,
            pathPolicy, overwrite);
    }

    /// <summary>
    ///     从 <c>current/</c> 活动目录读取完整的存档 payload。
    /// </summary>
    public static SaveGamePayload ReadSavePayloadFromCurrent(
        IFileSystem fileSystem,
        string saveRootPath,
        string saveId,
        string activeLevelId,
        ILogger? logger = null)
    {
        return ReadSavePayloadFromCurrent(fileSystem, SaveStorageGatewayFactory.CreateIoGateway(fileSystem),
            saveRootPath,
            saveId, activeLevelId, logger);
    }

    public static SaveGamePayload ReadSavePayloadFromCurrent(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string saveId,
        string activeLevelId,
        ILogger? logger = null) =>
        SavePayloadReader.ReadFromCurrent(fileSystem, dataSourceIo, saveRootPath, saveId, activeLevelId, logger);

    /// <summary>
    ///     从 <c>current/</c> 活动目录读取完整的存档 payload（策略感知）。
    /// </summary>
    public static SaveGamePayload ReadSavePayloadFromCurrent(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string saveId,
        string activeLevelId,
        ISavePathPolicy pathPolicy,
        ILogger? logger = null)
    {
        return SavePayloadReader.ReadFromCurrent(fileSystem, dataSourceIo, saveRootPath, saveId, activeLevelId,
            pathPolicy,
            logger);
    }

    /// <summary>
    ///     从指定存档槽的快照目录读取完整的存档 payload。
    /// </summary>
    public static SaveGamePayload ReadSavePayloadFromSnapshot(
        IFileSystem fileSystem,
        string saveRootPath,
        string saveId,
        string activeLevelId)
    {
        return ReadSavePayloadFromSnapshot(fileSystem, SaveStorageGatewayFactory.CreateIoGateway(fileSystem),
            saveRootPath,
            saveId, activeLevelId);
    }

    public static SaveGamePayload ReadSavePayloadFromSnapshot(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string saveId,
        string activeLevelId) =>
        SavePayloadReader.ReadFromSnapshot(fileSystem, dataSourceIo, saveRootPath, saveId, activeLevelId);

    /// <summary>
    ///     从指定存档槽的快照目录读取完整的存档 payload（策略感知）。
    /// </summary>
    public static SaveGamePayload ReadSavePayloadFromSnapshot(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string saveId,
        string activeLevelId,
        ISavePathPolicy pathPolicy)
    {
        return SavePayloadReader.ReadFromSnapshot(fileSystem, dataSourceIo, saveRootPath, saveId, activeLevelId,
            pathPolicy);
    }

    /// <summary>
    ///     从指定存档槽的快照目录中仅读取 Progress 黑板 JSON。
    /// </summary>
    public static DataSourceNode? ReadProgressNodeFromSnapshot(
        IFileSystem fileSystem,
        string saveRootPath,
        string saveId)
    {
        return ReadProgressNodeFromSnapshot(fileSystem, SaveStorageGatewayFactory.CreateIoGateway(fileSystem),
            saveRootPath,
            saveId);
    }

    public static DataSourceNode? ReadProgressNodeFromSnapshot(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string saveId) =>
        SavePayloadReader.ReadProgressNodeFromSnapshot(fileSystem, dataSourceIo, saveRootPath, saveId);

    /// <summary>
    ///     从指定存档槽的快照目录中仅读取 Progress 黑板 JSON（策略感知）。
    /// </summary>
    public static DataSourceNode? ReadProgressNodeFromSnapshot(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string saveId,
        ISavePathPolicy pathPolicy)
    {
        return SavePayloadReader.ReadProgressNodeFromSnapshot(fileSystem, dataSourceIo, saveRootPath, saveId,
            pathPolicy);
    }

    /// <summary>
    ///     将 <c>current/</c> 目录完整复制为指定存档槽的快照。
    /// </summary>
    public static void SnapshotCurrentToSave(
        IFileSystem fileSystem,
        string saveRootPath,
        string newSaveId,
        ILogger? logger = null) =>
        SnapshotCurrentToSave(fileSystem, saveRootPath, newSaveId, new DefaultSavePathPolicy(), logger);

    /// <summary>
    ///     将 <c>current/</c> 目录完整复制为指定存档槽的快照（策略感知）。
    /// </summary>
    public static void SnapshotCurrentToSave(
        IFileSystem fileSystem,
        string saveRootPath,
        string newSaveId,
        ISavePathPolicy pathPolicy,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(pathPolicy);
        SaveStorageGatewayFactory.ValidateRootPath(saveRootPath, nameof(saveRootPath),
            "Save root path cannot be null or whitespace.");
        if (string.IsNullOrWhiteSpace(newSaveId))
            throw new ArgumentException("New save id cannot be null or whitespace.", nameof(newSaveId));

        var currentRel = pathPolicy.GetCurrentDirectory();
        var currentAbs = fileSystem.CombinePath(saveRootPath, currentRel);
        if (!fileSystem.DirectoryExists(currentAbs))
            throw new InvalidOperationException("Missing required current/ directory.");

        var saveRel = pathPolicy.GetSaveDirectory(newSaveId);
        var saveAbs = fileSystem.CombinePath(saveRootPath, saveRel);
        var tempRel = $"{saveRel}.tmp";
        var tempAbs = fileSystem.CombinePath(saveRootPath, tempRel);

        if (fileSystem.DirectoryExists(tempAbs))
            fileSystem.DeleteDirectory(tempAbs);

        fileSystem.CreateDirectory(tempAbs);
        CopyCurrentToTempDirectory(fileSystem, saveRootPath, currentRel, tempRel, logger);

        if (fileSystem.DirectoryExists(saveAbs))
            fileSystem.DeleteDirectory(saveAbs);

        fileSystem.Rename(tempAbs, saveAbs);
    }

    private static void CopyCurrentToTempDirectory(
        IFileSystem fileSystem, string saveRootPath, string currentRel, string tempRel,
        ILogger? logger = null)
    {
        try
        {
            foreach (var srcAbs in fileSystem.EnumerateFiles(
                         fileSystem.CombinePath(saveRootPath, currentRel), "*", true))
            {
                var relFromRoot = SavePathResolver.GetRelativePath(saveRootPath, srcAbs);
                var relFromCurrent = SavePathResolver.GetRelativePath(currentRel, relFromRoot);
                var destRel = $"{tempRel}/{relFromCurrent}";
                var destAbs = fileSystem.CombinePath(saveRootPath, destRel);
                SavePathResolver.EnsureParentDirectory(fileSystem, destAbs);
                fileSystem.Copy(srcAbs, destAbs, true);
            }
        }
        catch (Exception ex)
        {
            var tempAbs = fileSystem.CombinePath(saveRootPath, tempRel);
            try
            {
                fileSystem.DeleteDirectory(tempAbs);
            }
            catch (Exception cleanupEx)
            {
                logger?.Log(LogLevel.Warning, nameof(SaveStorageFacade),
                    new LogMessageBuilder()
                        .AddSuffix("tempPath", tempAbs)
                        .Build(
                            $"Snapshot temp directory cleanup failed: {cleanupEx.Message}"));
            }

            throw new InvalidOperationException(
                "Snapshot from current/ to temp directory failed during copy phase.", ex);
        }
    }
}
