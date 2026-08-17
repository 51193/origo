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

/// <summary>
///     Atomic write helpers for the save storage layer. Extracted from
///     <see cref="SaveStorageFacade" /> to isolate idempotent dedup
///     (SHA-256 hash comparison), write-in-progress marker management,
///     temp directory preparation, and backup-replace snapshot swapping.
///     Called exclusively by <see cref="SaveStorageFacade" />; no external
///     caller should use this class directly.
/// </summary>
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
        // A leftover write-in-progress marker in current/ means the previous
        // save's snapshot phase failed. Skipping this write would leave
        // current/ refusing reads until the next content change; only skip
        // when current/ is clean.
        var currentRel = handle.PathPolicy.GetCurrentDirectory();
        var markerRel = handle.PathPolicy.GetWriteInProgressMarker(currentRel);
        if (handle.MetaAccess.FileExists(handle.GetAbsolutePath(markerRel)))
            return false;

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
    ///     Writes the payload to current/ and places the write-in-progress
    ///     marker. The payload.sha is written with the combined hash
    ///     (payload + extra/ files). The marker is refreshed after writing
    ///     completes: if the subsequent snapshot phase fails, the marker is
    ///     intentionally left on disk so that later ReadFromCurrent can
    ///     reject an updated-but-unsnapshotted current/.
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
    ///     Replaces the existing snapshot through backup-then-rename to ensure
    ///     the old data is never deleted before the new data is in place:
    ///     first move the old directory aside → rename the newly built temp
    ///     into place → then delete the backup.
    /// </summary>
    public static void SwapSnapshotDirectory(
        SaveFileHandle handle, string saveAbs, string tempAbs, string saveRel)
    {
        var bakRel = $"{saveRel}.bak";
        var bakAbs = handle.GetAbsolutePath(bakRel);
        var hadExisting = handle.MetaAccess.DirectoryExists(saveAbs);

        // A stale backup is only safe to remove when the real snapshot still
        // exists (the real slot is authoritative). When a previous swap failed
        // after renaming the old snapshot to .bak, the backup is the only
        // known-good copy and must survive until the new snapshot is in place.
        if (hadExisting && handle.MetaAccess.DirectoryExists(bakAbs))
            handle.MetaAccess.DeleteDirectory(bakAbs);

        if (hadExisting)
            handle.MetaAccess.Rename(saveAbs, bakAbs);

        try
        {
            handle.MetaAccess.Rename(tempAbs, saveAbs);
        }
        catch (Exception ex)
        {
            // Restore the previous snapshot to its original path when the new
            // one could not be installed. If even the rollback rename fails,
            // surface both failures; the previous generation is still present
            // in .bak and a later retry can recover it.
            try
            {
                if (hadExisting
                    && !handle.MetaAccess.DirectoryExists(saveAbs)
                    && handle.MetaAccess.DirectoryExists(bakAbs))
                    handle.MetaAccess.Rename(bakAbs, saveAbs);
            }
            catch (Exception rollbackEx)
            {
                throw new InvalidOperationException(
                    "Snapshot swap failed and the previous snapshot could not be restored from its backup. " +
                    "The backup copy is preserved on disk.",
                    new AggregateException(ex, rollbackEx));
            }

            throw;
        }

        // The new snapshot is installed; the previous generation (if any) is
        // now safe to remove.
        if (handle.MetaAccess.DirectoryExists(bakAbs))
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
                var relFromCurrent = SavePathLayout.StripPathPrefix(relFromRoot, currentRel);

                // The write-in-progress marker is a transient synchronization
                // file that only exists in current/ while a write is in flight;
                // it must not be copied into snapshots, where it would falsely
                // mark the completed snapshot as an interrupted write.
                var fileName = SaveFileHandle.GetLeafDirectoryName(relFromCurrent);
                if (string.Equals(fileName, SavePathLayout.WriteInProgressMarkerName,
                        StringComparison.Ordinal))
                    continue;

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
            var relFromDir = SavePathLayout.StripPathPrefix(relFromRoot, dirRel);

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

    internal static void WritePayloadSha(SaveFileHandle handle, string currentRel, string hash)
    {
        var shaRel = handle.PathPolicy.GetPayloadShaFile(currentRel);
        var shaAbs = handle.GetAbsolutePath(shaRel);
        handle.EnsureParentDirectory(shaRel);
        handle.IoGateway.WriteTree(shaAbs, DataSourceNode.CreateString(hash), true);
    }
}
