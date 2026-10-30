using System;
using System.Collections.Generic;
using Origo.Core.DataSource;

namespace Origo.Core.Save.Storage;

/// <summary>
///     Save path policy interface. Provides path assembly rules for save
///     directories and files, decoupling the path layout from business logic
///     so different path strategies can be injected per environment.
/// </summary>
/// <remarks>
///     Directory/slot members (<see cref="GetCurrentDirectory" />,
///     <see cref="GetSaveDirectory" />, <see cref="GetLevelDirectory" />, …) may
///     rename any directory segment. File-path members, however, must keep the
///     file suffix that the codec registry routes by (for example
///     <c>.json</c>, <c>.map</c>, <c>.sha</c>, <c>.write_in_progress</c>):
///     all save content I/O passes through <c>IDataSourceIoGateway</c> suffix
///     routing, so renaming a payload file's suffix makes every write and read
///     fail with "no codec configured" (fail-fast, not silent). The manual's
///     Storage entry and the persistence-flow usage guide state the same
///     constraint.
/// </remarks>
public interface ISavePathPolicy
{
    /// <summary>Get the relative path of the active save directory (current/).</summary>
    string GetCurrentDirectory();

    /// <summary>Get the relative path of a snapshot directory by save ID.</summary>
    string GetSaveDirectory(string saveId);

    /// <summary>Get the relative path of the progress blackboard JSON file.</summary>
    string GetProgressFile(string baseDirectory);

    /// <summary>Get the relative path of the progress state machine snapshot JSON file.</summary>
    string GetProgressStateMachinesFile(string baseDirectory);

    /// <summary>Get the relative path of the custom metadata file.</summary>
    string GetCustomMetaFile(string baseDirectory);

    /// <summary>Get the relative path of a level save subdirectory.</summary>
    string GetLevelDirectory(string baseDirectory, string levelId);

    /// <summary>Get the relative path of the level SND scene JSON file.</summary>
    string GetLevelSndSceneFile(string levelDirectory);

    /// <summary>Get the relative path of the level session blackboard JSON file.</summary>
    string GetLevelSessionFile(string levelDirectory);

    /// <summary>Get the relative path of the level session state machine snapshot JSON file.</summary>
    string GetLevelSessionStateMachinesFile(string levelDirectory);

    /// <summary>Get the relative path of the write-in-progress marker file.</summary>
    string GetWriteInProgressMarker(string baseDirectory);

    /// <summary>Get the relative path of the payload SHA digest file.</summary>
    string GetPayloadShaFile(string baseDirectory);

    /// <summary>Get the relative path of the extra data subdirectory (e.g. current/extra).</summary>
    string GetExtraDirectory(string baseDirectory);

    /// <summary>
    ///     Enumerates save IDs visible under <paramref name="saveRootPath" />.
    ///     The default implementation recognizes the standard <c>save_</c>
    ///     prefix; a custom layout overrides it so enumeration and metadata
    ///     listing use the same path policy as writes.
    /// </summary>
    IReadOnlyList<string> EnumerateSaveIds(IFileMetaAccess metaAccess, string saveRootPath)
    {
        ArgumentNullException.ThrowIfNull(metaAccess);
        if (string.IsNullOrWhiteSpace(saveRootPath))
            return [];

        var ids = new List<string>();
        foreach (var directory in metaAccess.EnumerateDirectories(saveRootPath))
        {
            var leaf = SaveFileHandle.GetLeafDirectoryName(directory);
            if (!leaf.StartsWith(SaveStorageFacade.SaveDirectoryPrefix, StringComparison.Ordinal))
                continue;
            var id = leaf[SaveStorageFacade.SaveDirectoryPrefix.Length..];
            if (id.Length > 0)
                ids.Add(id);
        }

        return ids;
    }

    /// <summary>
    ///     Enumerates interrupted snapshot backups under
    ///     <paramref name="saveRootPath" />, keyed by save ID and carrying the
    ///     actual backup directory path relative to the save root (for example
    ///     <c>save_{id}.bak</c>). The default implementation recognizes the
    ///     standard backup naming; a custom layout overrides it when its slot
    ///     directory naming differs, so recovery renames the path that was
    ///     actually found instead of reconstructing a name.
    /// </summary>
    IReadOnlyDictionary<string, string> EnumerateInterruptedSnapshots(
        IFileMetaAccess metaAccess,
        string saveRootPath)
    {
        ArgumentNullException.ThrowIfNull(metaAccess);
        if (string.IsNullOrWhiteSpace(saveRootPath))
            return new Dictionary<string, string>(StringComparer.Ordinal);

        var directories = new List<string>(metaAccess.EnumerateDirectories(saveRootPath));
        directories.Sort(StringComparer.Ordinal);

        var snapshots = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var directory in directories)
        {
            var leaf = SaveFileHandle.GetLeafDirectoryName(directory);
            if (!leaf.StartsWith(SaveStorageFacade.SaveDirectoryPrefix, StringComparison.Ordinal)
                || !leaf.EndsWith(SaveStorageFacade.SaveBackupDirectorySuffix, StringComparison.OrdinalIgnoreCase))
                continue;

            var id = leaf[
                SaveStorageFacade.SaveDirectoryPrefix.Length..^SaveStorageFacade.SaveBackupDirectorySuffix.Length];
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

            snapshots.TryAdd(id, leaf);
        }

        return snapshots;
    }

    /// <summary>
    ///     Extracts a level ID from a directory path when that directory is a
    ///     level directory for this layout. The default implementation
    ///     recognizes the standard <c>level_{id}</c> name; a custom layout
    ///     overrides it so discovery of background level payloads follows the
    ///     same layout as writes.
    /// </summary>
    string? TryGetLevelIdFromDirectory(string directoryPath)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        var leaf = SaveFileHandle.GetLeafDirectoryName(directoryPath);
        if (!leaf.StartsWith(SavePathLayout.LevelDirectoryPrefix, StringComparison.Ordinal))
            return null;

        var id = leaf[SavePathLayout.LevelDirectoryPrefix.Length..];
        return string.IsNullOrWhiteSpace(id) ? null : id;
    }
}
