using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        SaveAtomicWriter.WritePayloadSha(handle, handle.PathPolicy.GetCurrentDirectory(),
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

        var combinedHash = SaveAtomicWriter.ComputeCombinedHash(handle, payload);

        if (SaveAtomicWriter.TryIdempotentSkip(handle, newSaveId, combinedHash, logger, watch))
            return;

        var currentRel = handle.PathPolicy.GetCurrentDirectory();
        var markerAbs = SaveAtomicWriter.WriteCurrentWithMarker(handle, payload, currentRel, combinedHash);

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

    private static string StripPathPrefix(string fullPath, string prefix)
    {
        var normalized = prefix + SavePathLayout.PathSeparator;
        return fullPath.StartsWith(normalized, StringComparison.Ordinal)
            ? fullPath[normalized.Length..]
            : fullPath;
    }
}
