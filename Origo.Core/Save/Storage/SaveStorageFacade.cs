using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Logging;
using Origo.Core.DataSource;
using Origo.Core.Logging;
using Origo.Core.Save.Meta;

namespace Origo.Core.Save.Storage;

/// <summary>
///     存档 I/O 编排层：枚举、读写编排、幂等性去重、快照复制。
///     纯编排逻辑，具体文件解析/序列化委托给 SavePayloadReader / SavePayloadWriter。
/// </summary>
internal static class SaveStorageFacade
{
    public const string SaveDirectoryPrefix = "save_";

    public static IReadOnlyList<string> EnumerateSaveIds(SaveFileHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);

        if (!handle.MetaAccess.DirectoryExists(handle.SaveRootPath))
            return Array.Empty<string>();

        var result = new List<string>();
        foreach (var dir in handle.MetaAccess.EnumerateDirectories(handle.SaveRootPath))
        {
            var leaf = SaveFileHandle.GetLeafDirectoryName(dir);
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

    public static IReadOnlyList<SaveMetaDataEntry> EnumerateSavesWithMetaData(SaveFileHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);

        var ids = EnumerateSaveIds(handle);
        var list = new List<SaveMetaDataEntry>(ids.Count);
        foreach (var id in ids)
        {
            var saveRel = handle.PathPolicy.GetSaveDirectory(id);
            var metaRel = handle.PathPolicy.GetCustomMetaFile(saveRel);
            var metaAbs = handle.GetAbsolutePath(metaRel);
            var meta = SavePayloadReader.TryReadStringMap(handle, metaAbs) ??
                       new Dictionary<string, string>();
            list.Add(new SaveMetaDataEntry { SaveId = id, MetaData = meta });
        }

        return list;
    }

    public static void WriteSavePayloadToCurrent(SaveFileHandle handle, SaveGamePayload payload)
    {
        SavePayloadWriter.WriteToCurrent(handle, payload);
    }

    public static void WriteSavePayloadToCurrentThenSnapshot(
        SaveFileHandle handle,
        SaveGamePayload payload,
        string newSaveId,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        var snapshotDirRel = handle.PathPolicy.GetSaveDirectory(newSaveId);
        var snapshotShaRel = handle.PathPolicy.GetPayloadShaFile(snapshotDirRel);
        var snapshotShaAbs = handle.GetAbsolutePath(snapshotShaRel);
        if (handle.MetaAccess.FileExists(snapshotShaAbs))
        {
            string existingHash;
            try
            {
                existingHash = handle.IoGateway.ReadTree(snapshotShaAbs).AsString().Trim();
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

        var currentRel = handle.PathPolicy.GetCurrentDirectory();
        var markerRel = handle.PathPolicy.GetWriteInProgressMarker(currentRel);
        var markerAbs = handle.GetAbsolutePath(markerRel);
        var currentAbs = handle.GetAbsolutePath(currentRel);
        handle.MetaAccess.CreateDirectory(currentAbs);
        handle.IoGateway.WriteTree(markerAbs, DataSourceNode.CreateString(""));

        SavePayloadWriter.WriteToCurrent(handle, payload);

        handle.IoGateway.WriteTree(markerAbs, DataSourceNode.CreateString(""));

        try
        {
            SnapshotCurrentToSave(handle, newSaveId, logger);
        }
        catch (InvalidOperationException ex)
        {
            logger.Log(LogLevel.Error, nameof(SaveStorageFacade),
                new LogMessageBuilder()
                    .AddSuffix("saveRootPath", handle.SaveRootPath)
                    .AddSuffix("newSaveId", newSaveId)
                    .Build(
                        $"Snapshot failed after current/ was written; save index and disk may be inconsistent. {ex.Message}"));
            throw;
        }

        handle.MetaAccess.Delete(markerAbs);
    }

    public static void WriteLevelPayloadOnly(
        SaveFileHandle handle,
        string baseDirectoryRel,
        LevelPayload levelPayload,
        bool overwrite = true)
    {
        SavePayloadWriter.WriteLevelPayloadOnly(handle, baseDirectoryRel, levelPayload, overwrite);
    }

    public static SaveGamePayload ReadSavePayloadFromCurrent(
        SaveFileHandle handle,
        string saveId,
        string activeLevelId,
        ILogger? logger = null)
    {
        return SavePayloadReader.ReadFromCurrent(handle, saveId, activeLevelId, logger);
    }

    public static SaveGamePayload ReadSavePayloadFromSnapshot(
        SaveFileHandle handle,
        string saveId,
        string activeLevelId)
    {
        return SavePayloadReader.ReadFromSnapshot(handle, saveId, activeLevelId);
    }

    public static DataSourceNode? ReadProgressNodeFromSnapshot(
        SaveFileHandle handle,
        string saveId)
    {
        return SavePayloadReader.ReadProgressNodeFromSnapshot(handle, saveId);
    }

    public static void SnapshotCurrentToSave(
        SaveFileHandle handle,
        string newSaveId,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (string.IsNullOrWhiteSpace(newSaveId))
            throw new ArgumentException("New save id cannot be null or whitespace.", nameof(newSaveId));

        var currentRel = handle.PathPolicy.GetCurrentDirectory();
        var currentAbs = handle.GetAbsolutePath(currentRel);
        if (!handle.MetaAccess.DirectoryExists(currentAbs))
            throw new InvalidOperationException("Missing required current/ directory.");

        var saveRel = handle.PathPolicy.GetSaveDirectory(newSaveId);
        var saveAbs = handle.GetAbsolutePath(saveRel);
        var tempRel = $"{saveRel}.tmp";
        var tempAbs = handle.GetAbsolutePath(tempRel);

        if (handle.MetaAccess.DirectoryExists(tempAbs))
            handle.MetaAccess.DeleteDirectory(tempAbs);

        handle.MetaAccess.CreateDirectory(tempAbs);
        CopyCurrentToTempDirectory(handle, currentRel, tempRel, logger);

        if (handle.MetaAccess.DirectoryExists(saveAbs))
            handle.MetaAccess.DeleteDirectory(saveAbs);

        handle.MetaAccess.Rename(tempAbs, saveAbs);
    }

    private static void CopyCurrentToTempDirectory(
        SaveFileHandle handle, string currentRel, string tempRel,
        ILogger? logger = null)
    {
        try
        {
            var currentAbs = handle.GetAbsolutePath(currentRel);
            foreach (var srcAbs in handle.MetaAccess.EnumerateFiles(currentAbs, "*", true))
            {
                var relFromRoot = handle.GetRelativePath(srcAbs);
                var prefix = currentRel + "/";
                var relFromCurrent = relFromRoot.StartsWith(prefix, StringComparison.Ordinal)
                    ? relFromRoot.Substring(prefix.Length)
                    : relFromRoot;
                var destRel = $"{tempRel}/{relFromCurrent}";
                var destAbs = handle.GetAbsolutePath(destRel);
                handle.EnsureParentDirectory(destRel);
                handle.MetaAccess.Copy(srcAbs, destAbs, true);
            }
        }
        catch (Exception ex)
        {
            var tempAbs = handle.GetAbsolutePath(tempRel);
            try
            {
                handle.MetaAccess.DeleteDirectory(tempAbs);
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

    /// <summary>
    ///     从指定 save_* 快照中复制一个子目录到 current/ 对应位置。
    ///     若源子目录不存在则静默返回（无错误）。
    /// </summary>
    public static void CopyDirectoryFromSnapshot(
        SaveFileHandle handle,
        string saveId,
        string relativeDirName,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (string.IsNullOrWhiteSpace(saveId))
            throw new ArgumentException("Save id cannot be null or whitespace.", nameof(saveId));
        if (string.IsNullOrWhiteSpace(relativeDirName))
            throw new ArgumentException("Relative directory name cannot be null or whitespace.", nameof(relativeDirName));

        var saveRel = handle.PathPolicy.GetSaveDirectory(saveId);
        var srcRel = SavePathLayout.Combine(saveRel, relativeDirName);
        var srcAbs = handle.GetAbsolutePath(srcRel);

        if (!handle.MetaAccess.DirectoryExists(srcAbs))
            return;

        var currentRel = handle.PathPolicy.GetCurrentDirectory();
        var destRel = SavePathLayout.Combine(currentRel, relativeDirName);
        var destAbs = handle.GetAbsolutePath(destRel);

        handle.MetaAccess.CreateDirectory(destAbs);

        foreach (var srcFileAbs in handle.MetaAccess.EnumerateFiles(srcAbs, "*", true))
        {
            var relFromRoot = handle.GetRelativePath(srcFileAbs);
            var prefix = srcRel + "/";
            var relFromSrc = relFromRoot.StartsWith(prefix, StringComparison.Ordinal)
                ? relFromRoot.Substring(prefix.Length)
                : relFromRoot;
            var destFileRel = $"{destRel}/{relFromSrc}";
            var destFileAbs = handle.GetAbsolutePath(destFileRel);
            handle.EnsureParentDirectory(destFileRel);
            handle.MetaAccess.Copy(srcFileAbs, destFileAbs, true);
        }
    }
}
