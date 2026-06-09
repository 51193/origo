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

        if (!handle.FileSystem.DirectoryExists(handle.SaveRootPath))
            return Array.Empty<string>();

        var result = new List<string>();
        foreach (var dir in handle.FileSystem.EnumerateDirectories(handle.SaveRootPath))
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
        if (handle.FileSystem.Exists(snapshotShaAbs))
        {
            string existingHash;
            try
            {
                existingHash = handle.FileSystem.ReadAllText(snapshotShaAbs).Trim();
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
        handle.FileSystem.CreateDirectory(currentAbs);
        handle.FileSystem.WriteAllText(markerAbs, "", true);

        SavePayloadWriter.WriteToCurrent(handle, payload);

        handle.FileSystem.WriteAllText(markerAbs, "", true);

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

        handle.FileSystem.Delete(markerAbs);
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
        if (!handle.FileSystem.DirectoryExists(currentAbs))
            throw new InvalidOperationException("Missing required current/ directory.");

        var saveRel = handle.PathPolicy.GetSaveDirectory(newSaveId);
        var saveAbs = handle.GetAbsolutePath(saveRel);
        var tempRel = $"{saveRel}.tmp";
        var tempAbs = handle.GetAbsolutePath(tempRel);

        if (handle.FileSystem.DirectoryExists(tempAbs))
            handle.FileSystem.DeleteDirectory(tempAbs);

        handle.FileSystem.CreateDirectory(tempAbs);
        CopyCurrentToTempDirectory(handle, currentRel, tempRel, logger);

        if (handle.FileSystem.DirectoryExists(saveAbs))
            handle.FileSystem.DeleteDirectory(saveAbs);

        handle.FileSystem.Rename(tempAbs, saveAbs);
    }

    private static void CopyCurrentToTempDirectory(
        SaveFileHandle handle, string currentRel, string tempRel,
        ILogger? logger = null)
    {
        try
        {
            var currentAbs = handle.GetAbsolutePath(currentRel);
            foreach (var srcAbs in handle.FileSystem.EnumerateFiles(currentAbs, "*", true))
            {
                var relFromRoot = handle.GetRelativePath(srcAbs);
                var prefix = currentRel + "/";
                var relFromCurrent = relFromRoot.StartsWith(prefix, StringComparison.Ordinal)
                    ? relFromRoot.Substring(prefix.Length)
                    : relFromRoot;
                var destRel = $"{tempRel}/{relFromCurrent}";
                var destAbs = handle.GetAbsolutePath(destRel);
                handle.EnsureParentDirectory(destRel);
                handle.FileSystem.Copy(srcAbs, destAbs, true);
            }
        }
        catch (Exception ex)
        {
            var tempAbs = handle.GetAbsolutePath(tempRel);
            try
            {
                handle.FileSystem.DeleteDirectory(tempAbs);
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

        if (!handle.FileSystem.DirectoryExists(srcAbs))
            return;

        var currentRel = handle.PathPolicy.GetCurrentDirectory();
        var destRel = SavePathLayout.Combine(currentRel, relativeDirName);
        var destAbs = handle.GetAbsolutePath(destRel);

        handle.FileSystem.CreateDirectory(destAbs);

        foreach (var srcFileAbs in handle.FileSystem.EnumerateFiles(srcAbs, "*", true))
        {
            var relFromRoot = handle.GetRelativePath(srcFileAbs);
            var prefix = srcRel + "/";
            var relFromSrc = relFromRoot.StartsWith(prefix, StringComparison.Ordinal)
                ? relFromRoot.Substring(prefix.Length)
                : relFromRoot;
            var destFileRel = $"{destRel}/{relFromSrc}";
            var destFileAbs = handle.GetAbsolutePath(destFileRel);
            handle.EnsureParentDirectory(destFileRel);
            handle.FileSystem.Copy(srcFileAbs, destFileAbs, true);
        }
    }
}
