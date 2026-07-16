using System.Collections.Generic;
using Origo.Core.Abstractions.Logging;
using Origo.Core.DataSource;
using Origo.Core.Save.Meta;

namespace Origo.Core.Save.Storage;

/// <summary>
///     Abstraction for save read/write operations. Encapsulates storage layout
///     and I/O as a replaceable interface, decoupling callers such as
///     SessionRun / ProgressRun / Workflow from the concrete storage
///     implementation, enabling smooth substitution across different run modes
///     (foreground/background/test/cloud save). All path assembly is governed by
///     the implementation's internal <see cref="ISavePathPolicy" />; callers
///     do not need to be aware of the layout.
/// </summary>
public interface ISaveStorageService
{
    /// <summary>Enumerates all save slot IDs.</summary>
    IReadOnlyList<string> EnumerateSaveIds();

    /// <summary>Enumerates all save slots with their metadata.</summary>
    IReadOnlyList<SaveMetaDataEntry> EnumerateSavesWithMetaData();

    /// <summary>Writes a save payload to the current/ directory.</summary>
    void WriteSavePayloadToCurrent(SaveGamePayload payload);

    /// <summary>Writes a save payload to current/, then snapshots it to the save_* directory.</summary>
    void WriteSavePayloadToCurrentThenSnapshot(
        SaveGamePayload payload,
        string newSaveId,
        ILogger logger);

    /// <summary>Writes only a single level payload to the specified base directory.</summary>
    void WriteLevelPayloadOnly(
        string baseDirectoryRel,
        LevelPayload levelPayload,
        bool overwrite = true);

    /// <summary>Writes only a single level payload to the current/ directory.</summary>
    void WriteLevelPayloadOnlyToCurrent(LevelPayload levelPayload, bool overwrite = true);

    /// <summary>Writes only Progress-related files to the current/ directory.</summary>
    void WriteProgressOnlyToCurrent(
        DataSourceNode progressNode,
        DataSourceNode progressStateMachinesNode,
        bool overwrite = true);

    /// <summary>Reads a complete save payload from current/.</summary>
    SaveGamePayload ReadSavePayloadFromCurrent(
        string saveId,
        string activeLevelId,
        ILogger? logger = null);

    /// <summary>Reads a complete save payload from a save_* snapshot directory.</summary>
    SaveGamePayload ReadSavePayloadFromSnapshot(
        string saveId,
        string activeLevelId);

    /// <summary>Reads only the Progress node from a save_* snapshot directory.</summary>
    DataSourceNode? ReadProgressNodeFromSnapshot(string saveId);

    /// <summary>Attempts to read the payload of the specified level from current/; returns null when not found.</summary>
    LevelPayload? TryReadLevelPayloadFromCurrent(string levelId);

    /// <summary>Attempts to read the payload of the specified level from a save_* snapshot directory; returns null when not found.</summary>
    LevelPayload? TryReadLevelPayloadFromSnapshot(string saveId, string levelId);

    /// <summary>
    ///     Resolves and reads the payload of the specified level by priority:
    ///     reads from current/ first, falling back to the save_* snapshot when
    ///     not found. Returns null when neither location has data. This method
    ///     encapsulates the internal storage tier (current/ vs snapshot) of the
    ///     save module; external callers do not need to be aware of the storage
    ///     location.
    /// </summary>
    /// <param name="saveId">The current save slot ID (used to locate the save_* directory during snapshot fallback).</param>
    /// <param name="levelId">The target level ID.</param>
    /// <returns>The resolved LevelPayload, or null if neither location has data.</returns>
    LevelPayload? ResolveLevelPayload(string saveId, string levelId);

    /// <summary>Snapshots current/ to a save_* directory.</summary>
    void SnapshotCurrentToSave(string newSaveId);

    /// <summary>
    ///     Deletes the current/ temporary active directory and all its contents.
    ///     Design intent:
    ///     - before reading from a snapshot and copying to current/, clean up
    ///       the previous temporary data to avoid stale file leftovers;
    ///     - after the ProgressRun lifecycle ends (exiting the current workflow),
    ///       clean up current/ to free space and avoid misuse.
    ///     The implementation should be idempotent: no exception is thrown if
    ///     the directory does not exist.
    /// </summary>
    void DeleteCurrentDirectory();

    /// <summary>
    ///     Copies the extra/ subdirectory from a specified save_* snapshot back
    ///     to current/. Used during load to restore files written by the strategy
    ///     via ISndArchiveFileAccess. This operation is symmetric with
    ///     <see cref="WriteSavePayloadToCurrent" />. Silently skipped if the
    ///     extra/ directory does not exist in the snapshot.
    /// </summary>
    void RestoreExtraFilesFromSnapshot(string saveId);
}
