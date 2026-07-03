using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
            return [];

        var result = new List<string>();
        foreach (var dir in handle.MetaAccess.EnumerateDirectories(handle.SaveRootPath))
        {
            var leaf = SaveFileHandle.GetLeafDirectoryName(dir);
            if (!leaf.StartsWith(SaveDirectoryPrefix, StringComparison.Ordinal))
                continue;
            var id = leaf[SaveDirectoryPrefix.Length..];
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
        WritePayloadSha(handle, handle.PathPolicy.GetCurrentDirectory(),
            SavePayloadWriter.ComputePayloadHash(payload));
    }

    public static void WriteSavePayloadToCurrentThenSnapshot(
        SaveFileHandle handle,
        SaveGamePayload payload,
        string newSaveId,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        var watch = Stopwatch.StartNew();

        var combinedHash = ComputeCombinedHash(handle, payload);

        if (TryIdempotentSkip(handle, newSaveId, combinedHash, logger, watch))
            return;

        var currentRel = handle.PathPolicy.GetCurrentDirectory();
        var markerAbs = WriteCurrentWithMarker(handle, payload, currentRel, combinedHash);

        try
        {
            SnapshotCurrentToSave(handle, newSaveId, logger);
        }
        catch (InvalidOperationException ex)
        {
            logger.Log(LogLevel.Error, nameof(SaveStorageFacade),
                new LogMessageBuilder()
                    .AddContext("saveRootPath", handle.SaveRootPath)
                    .AddContext("newSaveId", newSaveId)
                    .SetElapsedMs(watch.Elapsed.TotalMilliseconds)
                    .Build(
                        $"Snapshot failed after current/ was written; save index and disk may be inconsistent. {ex.Message}"));
            throw;
        }

        handle.MetaAccess.Delete(markerAbs);
        logger.Log(LogLevel.Info, nameof(SaveStorageFacade),
            new LogMessageBuilder()
                .SetElapsedMs(watch.Elapsed.TotalMilliseconds)
                .Build($"Save payload written and snapshotted for '{newSaveId}'."));
    }

    public static void WriteLevelPayloadOnly(
        SaveFileHandle handle,
        string baseDirectoryRel,
        LevelPayload levelPayload,
        bool overwrite = true) => SavePayloadWriter.WriteLevelPayloadOnly(handle, baseDirectoryRel, levelPayload, overwrite);

    public static SaveGamePayload ReadSavePayloadFromCurrent(
        SaveFileHandle handle,
        string saveId,
        string activeLevelId,
        ILogger? logger = null) => SavePayloadReader.ReadFromCurrent(handle, saveId, activeLevelId, logger);

    public static SaveGamePayload ReadSavePayloadFromSnapshot(
        SaveFileHandle handle,
        string saveId,
        string activeLevelId) => SavePayloadReader.ReadFromSnapshot(handle, saveId, activeLevelId);

    public static DataSourceNode? ReadProgressNodeFromSnapshot(
        SaveFileHandle handle,
        string saveId) => SavePayloadReader.ReadProgressNodeFromSnapshot(handle, saveId);

    public static void SnapshotCurrentToSave(
        SaveFileHandle handle,
        string newSaveId,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (string.IsNullOrWhiteSpace(newSaveId))
            throw new ArgumentException("New save id cannot be null or whitespace.", nameof(newSaveId));

        var watch = Stopwatch.StartNew();
        var currentRel = handle.PathPolicy.GetCurrentDirectory();
        var currentAbs = handle.GetAbsolutePath(currentRel);
        if (!handle.MetaAccess.DirectoryExists(currentAbs))
            throw new InvalidOperationException("Missing required current/ directory.");

        var saveRel = handle.PathPolicy.GetSaveDirectory(newSaveId);
        var saveAbs = handle.GetAbsolutePath(saveRel);
        var tempAbs = PrepareTempDirectory(handle, saveRel);

        CopyCurrentToTempDirectory(handle, currentRel, $"{saveRel}.tmp", logger);

        SwapSnapshotDirectory(handle, saveAbs, tempAbs, saveRel);

        logger?.Log(LogLevel.Info, nameof(SaveStorageFacade),
            new LogMessageBuilder()
                .SetElapsedMs(watch.Elapsed.TotalMilliseconds)
                .Build($"Snapshot completed from current/ to save '{newSaveId}'."));
    }

    private static string ComputeCombinedHash(SaveFileHandle handle, SaveGamePayload payload)
    {
        var payloadHash = SavePayloadWriter.ComputePayloadHash(payload);
        var sideHash = ComputeSideDirectoryHash(handle, SavePathLayout.ExtraDirectoryName);
        return CombineHashes(payloadHash, sideHash);
    }

    private static bool TryIdempotentSkip(
        SaveFileHandle handle, string newSaveId, string combinedHash, ILogger logger, Stopwatch watch)
    {
        var snapshotDirRel = handle.PathPolicy.GetSaveDirectory(newSaveId);
        var snapshotShaRel = handle.PathPolicy.GetPayloadShaFile(snapshotDirRel);
        var snapshotShaAbs = handle.GetAbsolutePath(snapshotShaRel);

        if (!handle.MetaAccess.FileExists(snapshotShaAbs))
            return false;

        var existingHash = handle.IoGateway.ReadTree(snapshotShaAbs).AsString().Trim();
        if (existingHash.Length > 0
            && string.Equals(existingHash, combinedHash, StringComparison.Ordinal))
        {
            logger.Log(LogLevel.Info, nameof(SaveStorageFacade),
                new LogMessageBuilder()
                    .SetElapsedMs(watch.Elapsed.TotalMilliseconds)
                    .Build($"Idempotent save skip: combined hash unchanged for save '{newSaveId}'."));
            return true;
        }

        return false;
    }

    /// <summary>
    ///     将 payload 写入 current/ 并放置 write-in-progress marker。
    ///     payload.sha 写入合并哈希（payload + extra/ 文件）。
    ///     marker 在写入完成后刷新：若后续 snapshot 阶段失败，marker 有意保留在磁盘上，
    ///     以便后续 ReadFromCurrent 拒绝 updated-but-unsnapshotted current/。
    /// </summary>
    private static string WriteCurrentWithMarker(
        SaveFileHandle handle, SaveGamePayload payload, string currentRel, string combinedHash)
    {
        var markerRel = handle.PathPolicy.GetWriteInProgressMarker(currentRel);
        var markerAbs = handle.GetAbsolutePath(markerRel);
        var currentAbs = handle.GetAbsolutePath(currentRel);
        handle.MetaAccess.CreateDirectory(currentAbs);
        handle.IoGateway.WriteTree(markerAbs, DataSourceNode.CreateString(""));

        SavePayloadWriter.WriteToCurrent(handle, payload);
        WritePayloadSha(handle, currentRel, combinedHash);

        handle.IoGateway.WriteTree(markerAbs, DataSourceNode.CreateString(""));
        return markerAbs;
    }

    private static string PrepareTempDirectory(SaveFileHandle handle, string saveRel)
    {
        var tempRel = $"{saveRel}.tmp";
        var tempAbs = handle.GetAbsolutePath(tempRel);

        if (handle.MetaAccess.DirectoryExists(tempAbs))
            handle.MetaAccess.DeleteDirectory(tempAbs);

        handle.MetaAccess.CreateDirectory(tempAbs);
        return tempAbs;
    }

    /// <summary>
    ///     通过 backup-then-rename 替换已有 snapshot，确保旧数据在新数据到位前永不被删除：
    ///     先将旧目录移开 → 将新构建的 temp 重命名到位 → 再删除 backup。
    /// </summary>
    private static void SwapSnapshotDirectory(
        SaveFileHandle handle, string saveAbs, string tempAbs, string saveRel)
    {
        var bakRel = $"{saveRel}.bak";
        var bakAbs = handle.GetAbsolutePath(bakRel);
        if (handle.MetaAccess.DirectoryExists(bakAbs))
            handle.MetaAccess.DeleteDirectory(bakAbs);

        var hadExisting = handle.MetaAccess.DirectoryExists(saveAbs);
        if (hadExisting)
            handle.MetaAccess.Rename(saveAbs, bakAbs);

        handle.MetaAccess.Rename(tempAbs, saveAbs);

        if (hadExisting)
            handle.MetaAccess.DeleteDirectory(bakAbs);
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
                var relFromCurrent = StripPathPrefix(relFromRoot, currentRel);
                var destRel = $"{tempRel}/{relFromCurrent}";
                var destAbs = handle.GetAbsolutePath(destRel);
                handle.EnsureParentDirectory(destRel);
                handle.MetaAccess.Copy(srcAbs, destAbs, true);
            }
        }
        catch (Exception ex)
        {
            logger?.Log(LogLevel.Warning, nameof(SaveStorageFacade),
                new LogMessageBuilder()
                    .AddContext("currentRel", currentRel)
                    .AddContext("tempRel", tempRel)
                    .Build($"Snapshot copy phase failed: {ex.Message}"));

            var tempAbs = handle.GetAbsolutePath(tempRel);
            try
            {
                handle.MetaAccess.DeleteDirectory(tempAbs);
            }
            catch (Exception cleanupEx)
            {
                logger?.Log(LogLevel.Warning, nameof(SaveStorageFacade),
                    new LogMessageBuilder()
                        .AddContext("tempPath", tempAbs)
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
        string relativeDirName)
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
            var relFromSrc = StripPathPrefix(relFromRoot, srcRel);
            var destFileRel = $"{destRel}/{relFromSrc}";
            var destFileAbs = handle.GetAbsolutePath(destFileRel);
            handle.EnsureParentDirectory(destFileRel);
            handle.MetaAccess.Copy(srcFileAbs, destFileAbs, true);
        }
    }

    /// <summary>
    ///     计算 current/ 下指定子目录中所有文件的 SHA-256 哈希。
    ///     文件按相对路径排序后依次哈希（路径 + 内容节点哈希），确保确定性。
    ///     若目录不存在或无文件，返回空字符串。
    /// </summary>
    internal static string ComputeSideDirectoryHash(SaveFileHandle handle, string relativeDirName)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (string.IsNullOrWhiteSpace(relativeDirName))
            throw new ArgumentException("Relative directory name cannot be null or whitespace.",
                nameof(relativeDirName));

        var currentRel = handle.PathPolicy.GetCurrentDirectory();
        var dirRel = SavePathLayout.Combine(currentRel, relativeDirName);
        var dirAbs = handle.GetAbsolutePath(dirRel);

        if (!handle.MetaAccess.DirectoryExists(dirAbs))
            return string.Empty;

        var files = handle.MetaAccess.EnumerateFiles(dirAbs, "*", true).ToList();
        if (files.Count == 0)
            return string.Empty;

        files.Sort(StringComparer.Ordinal);

        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var fileAbs in files)
        {
            var relFromRoot = handle.GetRelativePath(fileAbs);
            var relFromDir = StripPathPrefix(relFromRoot, dirRel);

            sha.AppendData(Encoding.UTF8.GetBytes(relFromDir));
            try
            {
                using var node = handle.IoGateway.ReadTree(fileAbs);
                sha.AppendData(Encoding.UTF8.GetBytes(node.ComputeSha256Hash()));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to hash extra file '{relFromDir}': {ex.Message}", ex);
            }
        }

        return Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
    }

    /// <summary>
    ///     将 payload 哈希和侧信道目录哈希合并为统一存档哈希。
    ///     始终使用域标签前缀（P: / S:）确保不同域的输入不会产生冲突，
    ///     且输出的 .payload.sha 格式在所有存档间一致。
    /// </summary>
    internal static string CombineHashes(string payloadHash, string sideHash)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        sha.AppendData(Encoding.UTF8.GetBytes("P:" + payloadHash));
        sha.AppendData(Encoding.UTF8.GetBytes("|S:" + sideHash));
        return Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
    }

    private static string StripPathPrefix(string fullPath, string prefix)
    {
        var normalized = prefix + SavePathLayout.PathSeparator;
        return fullPath.StartsWith(normalized, StringComparison.Ordinal)
            ? fullPath[normalized.Length..]
            : fullPath;
    }

    private static void WritePayloadSha(SaveFileHandle handle, string currentRel, string hash)
    {
        var shaRel = handle.PathPolicy.GetPayloadShaFile(currentRel);
        var shaAbs = handle.GetAbsolutePath(shaRel);
        handle.EnsureParentDirectory(shaRel);
        handle.IoGateway.WriteTree(shaAbs, DataSourceNode.CreateString(hash), true);
    }
}
