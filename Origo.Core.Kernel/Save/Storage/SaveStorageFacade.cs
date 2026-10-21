using System;
using System.Collections.Generic;
using System.Diagnostics;
using Origo.Core.Abstractions.Logging;
using Origo.Core.DataSource;
using Origo.Core.Logging;
using Origo.Core.Save.Meta;

namespace Origo.Core.Save.Storage;

/// <summary>
///     Save I/O orchestration layer: enumeration, read/write orchestration,
///     and snapshot copying. Pure orchestration logic delegates file parsing
///     to <see cref="SavePayloadReader" />, serialization to
///     <see cref="SavePayloadWriter" />, and atomic writes to
///     <see cref="SaveAtomicWriter" />.
/// </summary>
internal static class SaveStorageFacade
{
    public const string SaveDirectoryPrefix = "save_";

    /// <summary>Suffix of the staging directory used during snapshot writes.</summary>
    public const string SaveTempDirectorySuffix = ".tmp";

    /// <summary>Suffix of the backup directory kept while swapping a snapshot.</summary>
    public const string SaveBackupDirectorySuffix = ".bak";

    public static IReadOnlyList<string> EnumerateSaveIds(SaveFileHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);

        if (!handle.MetaAccess.DirectoryExists(handle.SaveRootPath))
            return [];

        RecoverInterruptedSnapshots(handle);

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in handle.PathPolicy.EnumerateSaveIds(handle.MetaAccess, handle.SaveRootPath))
        {
            if (string.IsNullOrWhiteSpace(id))
                continue;

            // A stray directory can contain characters that are not legal in
            // a save id. Skip it instead of failing listing/metadata for every
            // valid slot; suffix-ending directories are atomic-write remnants
            // and are never user slots.
            try
            {
                SavePathLayout.ValidateSaveId(id, nameof(id));
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (!seen.Add(id))
                continue;

            var saveRel = handle.PathPolicy.GetSaveDirectory(id);
            if (handle.MetaAccess.DirectoryExists(handle.GetAbsolutePath(saveRel)))
                result.Add(id);
        }

        result.Sort(StringComparer.Ordinal);
        return result;
    }

    /// <summary>
    ///     Recover the crash window between the two renames in
    ///     <see cref="SaveAtomicWriter.SwapSnapshotDirectory" />: when the real
    ///     slot is missing but a complete <c>.bak</c> snapshot exists, restore
    ///     the backup as the real slot. Incomplete backups are left untouched
    ///     and are never enumerated as slots.
    /// </summary>
    internal static void RecoverInterruptedSnapshots(SaveFileHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (!handle.MetaAccess.DirectoryExists(handle.SaveRootPath))
            return;

        foreach (var directory in handle.MetaAccess.EnumerateDirectories(handle.SaveRootPath))
        {
            var leaf = SaveFileHandle.GetLeafDirectoryName(directory);
            if (!leaf.StartsWith(SaveDirectoryPrefix, StringComparison.Ordinal)
                || !leaf.EndsWith(SaveBackupDirectorySuffix, StringComparison.OrdinalIgnoreCase))
                continue;

            var id = leaf[SaveDirectoryPrefix.Length..^SaveBackupDirectorySuffix.Length];
            if (id.Length == 0)
                continue;
            try
            {
                SavePathLayout.ValidateSaveId(id, nameof(id));
            }
            catch (ArgumentException)
            {
                continue;
            }

            var realRel = handle.PathPolicy.GetSaveDirectory(id);
            var realAbs = handle.GetAbsolutePath(realRel);
            if (handle.MetaAccess.DirectoryExists(realAbs))
                continue;

            if (IsCompleteSnapshot(handle, leaf))
                handle.MetaAccess.Rename(directory, realAbs);
        }
    }

    private static void RecoverInterruptedSnapshot(SaveFileHandle handle, string saveId)
    {
        var realRel = handle.PathPolicy.GetSaveDirectory(saveId);
        var realAbs = handle.GetAbsolutePath(realRel);
        if (handle.MetaAccess.DirectoryExists(realAbs))
            return;

        var backupRel = realRel + SaveBackupDirectorySuffix;
        var backupAbs = handle.GetAbsolutePath(backupRel);
        if (!handle.MetaAccess.DirectoryExists(backupAbs))
            return;

        if (IsCompleteSnapshot(handle, backupRel))
            handle.MetaAccess.Rename(backupAbs, realAbs);
    }

    private static bool IsCompleteSnapshot(SaveFileHandle handle, string snapshotRel)
    {
        var progressRel = handle.PathPolicy.GetProgressFile(snapshotRel);
        var stateMachinesRel = handle.PathPolicy.GetProgressStateMachinesFile(snapshotRel);
        if (!handle.MetaAccess.FileExists(handle.GetAbsolutePath(progressRel))
            || !handle.MetaAccess.FileExists(handle.GetAbsolutePath(stateMachinesRel)))
            return false;

        var snapshotAbs = handle.GetAbsolutePath(snapshotRel);
        var hasLevel = false;
        foreach (var levelDir in handle.MetaAccess.EnumerateDirectories(snapshotAbs))
        {
            var leaf = SaveFileHandle.GetLeafDirectoryName(levelDir);
            if (!leaf.StartsWith(SavePathLayout.LevelDirectoryPrefix, StringComparison.Ordinal))
                continue;

            var levelRel = SavePathLayout.Combine(snapshotRel, leaf);
            if (!handle.MetaAccess.FileExists(handle.GetAbsolutePath(
                    handle.PathPolicy.GetLevelSndSceneFile(levelRel)))
                || !handle.MetaAccess.FileExists(handle.GetAbsolutePath(
                    handle.PathPolicy.GetLevelSessionFile(levelRel)))
                || !handle.MetaAccess.FileExists(handle.GetAbsolutePath(
                    handle.PathPolicy.GetLevelSessionStateMachinesFile(levelRel))))
                return false;

            hasLevel = true;
        }

        return hasLevel;
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
            var rawMeta = SavePayloadReader.TryReadStringMap(handle, metaAbs) ??
                          new Dictionary<string, string>();

            // Framework-reserved keys (the origo. namespace, e.g. the format
            // version) are internal bookkeeping and are not display metadata.
            var displayMeta = SavePayloadReader.StripFrameworkMetaKeys(rawMeta)!;

            list.Add(new SaveMetaDataEntry { SaveId = id, MetaData = displayMeta });
        }

        return list;
    }

    public static void DeleteSave(SaveFileHandle handle, string saveId)
    {
        ArgumentNullException.ThrowIfNull(handle);
        SavePathLayout.ValidateSaveId(saveId, nameof(saveId));

        var saveRel = handle.PathPolicy.GetSaveDirectory(saveId);
        var realAbs = handle.GetAbsolutePath(saveRel);
        var tempAbs = handle.GetAbsolutePath(saveRel + SaveTempDirectorySuffix);
        var backupAbs = handle.GetAbsolutePath(saveRel + SaveBackupDirectorySuffix);

        var hasReal = handle.MetaAccess.DirectoryExists(realAbs);
        var hasTemp = handle.MetaAccess.DirectoryExists(tempAbs);
        var hasBackup = handle.MetaAccess.DirectoryExists(backupAbs);
        if (!hasReal && !hasTemp && !hasBackup)
            throw new InvalidOperationException($"Save slot '{saveId}' does not exist.");

        // Remove the visible slot first: if a later remnant cleanup fails,
        // the backup cannot outlive a still-valid slot.
        if (hasReal)
            handle.MetaAccess.DeleteDirectory(realAbs);
        if (hasTemp)
            handle.MetaAccess.DeleteDirectory(tempAbs);
        if (hasBackup)
            handle.MetaAccess.DeleteDirectory(backupAbs);
    }

    public static void WriteSavePayloadToCurrentThenSnapshot(
        SaveFileHandle handle,
        SaveGamePayload payload,
        string newSaveId,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        var watch = Stopwatch.StartNew();

        var combinedHash = SaveAtomicWriter.ComputeCombinedHash(handle, payload);

        if (SaveAtomicWriter.TryIdempotentSkip(handle, newSaveId, combinedHash, logger, watch))
            return;

        var currentRel = handle.PathPolicy.GetCurrentDirectory();
        var markerAbs = SaveAtomicWriter.WriteCurrentWithMarker(handle, payload, currentRel, combinedHash);

        try
        {
            SnapshotCurrentToSave(handle, newSaveId, logger);
        }
        catch (Exception ex)
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
        string activeLevelId)
    {
        RecoverInterruptedSnapshot(handle, saveId);
        return SavePayloadReader.ReadFromSnapshot(handle, saveId, activeLevelId);
    }

    public static DataSourceNode? ReadProgressNodeFromSnapshot(
        SaveFileHandle handle,
        string saveId)
    {
        RecoverInterruptedSnapshot(handle, saveId);
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

        var watch = Stopwatch.StartNew();
        var currentRel = handle.PathPolicy.GetCurrentDirectory();
        var currentAbs = handle.GetAbsolutePath(currentRel);
        if (!handle.MetaAccess.DirectoryExists(currentAbs))
            throw new InvalidOperationException("Missing required current/ directory.");

        var saveRel = handle.PathPolicy.GetSaveDirectory(newSaveId);
        var saveAbs = handle.GetAbsolutePath(saveRel);
        var tempAbs = SaveAtomicWriter.PrepareTempDirectory(handle, saveRel);

        SaveAtomicWriter.CopyCurrentToTempDirectory(handle, currentRel, $"{saveRel}.tmp", logger);

        SaveAtomicWriter.SwapSnapshotDirectory(handle, saveAbs, tempAbs, saveRel);

        logger?.Log(LogLevel.Info, nameof(SaveStorageFacade),
            new LogMessageBuilder()
                .SetElapsedMs(watch.Elapsed.TotalMilliseconds)
                .Build($"Snapshot completed from current/ to save '{newSaveId}'."));
    }

    /// <summary>
    ///     Copy a subdirectory from a save snapshot into the corresponding
    ///     location under current/. Silently returns if the source
    ///     directory does not exist.
    /// </summary>
    public static void CopyDirectoryFromSnapshot(
        SaveFileHandle handle,
        string saveId,
        string relativeDirName) =>
        CopyDirectoryFromSnapshot(handle, saveId, relativeDirName, handle);

    /// <summary>
    ///     Copy a subdirectory from a snapshot owned by
    ///     <paramref name="sourceHandle" /> into the current/ directory owned
    ///     by <paramref name="destinationHandle" />. The two handles may use
    ///     different roots and path policies; source-relative paths are
    ///     computed with the source handle and destination paths with the
    ///     destination handle. Silently returns when the source directory
    ///     does not exist.
    /// </summary>
    public static void CopyDirectoryFromSnapshot(
        SaveFileHandle sourceHandle,
        string saveId,
        string relativeDirName,
        SaveFileHandle destinationHandle) =>
        CopyDirectoryFromSnapshot(sourceHandle, saveId, relativeDirName, destinationHandle, relativeDirName);

    /// <summary>
    ///     Copy a source-relative subdirectory from a snapshot into a
    ///     destination-relative subdirectory under current/. The two names may
    ///     differ when source and destination use different path policies
    ///     (for example a custom extra/ directory name). Silently returns when
    ///     the source directory does not exist.
    /// </summary>
    public static void CopyDirectoryFromSnapshot(
        SaveFileHandle sourceHandle,
        string saveId,
        string sourceRelativeDirName,
        SaveFileHandle destinationHandle,
        string destinationRelativeDirName)
    {
        ArgumentNullException.ThrowIfNull(sourceHandle);
        ArgumentNullException.ThrowIfNull(destinationHandle);
        if (string.IsNullOrWhiteSpace(saveId))
            throw new ArgumentException("Save id cannot be null or whitespace.", nameof(saveId));
        if (string.IsNullOrWhiteSpace(sourceRelativeDirName))
            throw new ArgumentException("Source relative directory name cannot be null or whitespace.",
                nameof(sourceRelativeDirName));
        if (string.IsNullOrWhiteSpace(destinationRelativeDirName))
            throw new ArgumentException("Destination relative directory name cannot be null or whitespace.",
                nameof(destinationRelativeDirName));

        var saveRel = sourceHandle.PathPolicy.GetSaveDirectory(saveId);
        var srcRel = SavePathLayout.Combine(saveRel, sourceRelativeDirName);
        var srcAbs = sourceHandle.GetAbsolutePath(srcRel);

        if (!sourceHandle.MetaAccess.DirectoryExists(srcAbs))
            return;

        var currentRel = destinationHandle.PathPolicy.GetCurrentDirectory();
        var destRel = SavePathLayout.Combine(currentRel, destinationRelativeDirName);
        var destAbs = destinationHandle.GetAbsolutePath(destRel);

        destinationHandle.MetaAccess.CreateDirectory(destAbs);

        foreach (var srcFileAbs in sourceHandle.MetaAccess.EnumerateFiles(srcAbs, "*", true))
        {
            var relFromRoot = sourceHandle.GetRelativePath(srcFileAbs);
            var relFromSrc = SavePathLayout.StripPathPrefix(relFromRoot, srcRel);
            var destFileRel = SavePathLayout.Combine(destRel, relFromSrc);
            var destFileAbs = destinationHandle.GetAbsolutePath(destFileRel);
            destinationHandle.EnsureParentDirectory(destFileRel);
            destinationHandle.MetaAccess.Copy(srcFileAbs, destFileAbs, true);
        }
    }
}
