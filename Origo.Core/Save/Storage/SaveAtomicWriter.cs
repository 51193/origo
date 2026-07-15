using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Origo.Core.Abstractions.Logging;
using Origo.Core.DataSource;
using Origo.Core.Logging;

namespace Origo.Core.Save.Storage;

internal static class SaveAtomicWriter
{
    public static string ComputeCombinedHash(SaveFileHandle handle, SaveGamePayload payload)
    {
        var payloadHash = SavePayloadWriter.ComputePayloadHash(payload);
        var sideHash = ComputeSideDirectoryHash(handle, SavePathLayout.ExtraDirectoryName);
        return CombineHashes(payloadHash, sideHash);
    }

    public static bool TryIdempotentSkip(
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
    public static string WriteCurrentWithMarker(
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

    public static string PrepareTempDirectory(SaveFileHandle handle, string saveRel)
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
    public static void SwapSnapshotDirectory(
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

    public static void CopyCurrentToTempDirectory(
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
            logger?.Log(LogLevel.Warning, nameof(SaveAtomicWriter),
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
                logger?.Log(LogLevel.Warning, nameof(SaveAtomicWriter),
                    new LogMessageBuilder()
                        .AddContext("tempPath", tempAbs)
                        .Build(
                            $"Snapshot temp directory cleanup failed: {cleanupEx.Message}"));
            }

            throw new InvalidOperationException(
                "Snapshot from current/ to temp directory failed during copy phase.", ex);
        }
    }

    public static string CombineHashes(string payloadHash, string sideHash)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        sha.AppendData(Encoding.UTF8.GetBytes("P:" + payloadHash));
        sha.AppendData(Encoding.UTF8.GetBytes("|S:" + sideHash));
        return Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
    }

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

    private static string StripPathPrefix(string fullPath, string prefix)
    {
        var normalized = prefix + SavePathLayout.PathSeparator;
        return fullPath.StartsWith(normalized, StringComparison.Ordinal)
            ? fullPath[normalized.Length..]
            : fullPath;
    }

    internal static void WritePayloadSha(SaveFileHandle handle, string currentRel, string hash)
    {
        var shaRel = handle.PathPolicy.GetPayloadShaFile(currentRel);
        var shaAbs = handle.GetAbsolutePath(shaRel);
        handle.EnsureParentDirectory(shaRel);
        handle.IoGateway.WriteTree(shaAbs, DataSourceNode.CreateString(hash), true);
    }
}
