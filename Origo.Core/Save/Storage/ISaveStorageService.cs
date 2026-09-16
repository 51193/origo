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
    /// <remarks>
    ///     The implementation must fully consume the payload's
    ///     <see cref="DataSourceNode" /> trees during the call; callers may
    ///     dispose them as soon as the method returns.
    /// </remarks>
    void WriteSavePayloadToCurrent(SaveGamePayload payload);

    /// <summary>Writes a save payload to current/, then snapshots it to the save_* directory.</summary>
    /// <remarks>
    ///     The implementation must fully consume the payload's
    ///     <see cref="DataSourceNode" /> trees during the call; callers may
    ///     dispose them as soon as the method returns.
    /// </remarks>
    void WriteSavePayloadToCurrentThenSnapshot(
        SaveGamePayload payload,
        string newSaveId,
        ILogger logger);

    /// <summary>Writes only a single level payload to the current/ directory.</summary>
    /// <remarks>
    ///     The implementation must fully consume the payload's
    ///     <see cref="DataSourceNode" /> trees during the call; callers may
    ///     dispose them as soon as the method returns.
    /// </remarks>
    void WriteLevelPayloadOnlyToCurrent(LevelPayload levelPayload);

    /// <summary>Writes only Progress-related files to the current/ directory.</summary>
    /// <remarks>
    ///     The implementation must fully consume both node trees during the
    ///     call; callers may dispose them as soon as the method returns.
    /// </remarks>
    void WriteProgressOnlyToCurrent(
        DataSourceNode progressNode,
        DataSourceNode progressStateMachinesNode);

    /// <summary>Reads a complete save payload from a save_* snapshot directory.</summary>
    /// <remarks>
    ///     The returned payload and all of its node trees are owned by the
    ///     caller and must be disposed when no longer needed.
    /// </remarks>
    SaveGamePayload ReadSavePayloadFromSnapshot(
        string saveId,
        string activeLevelId);

    /// <summary>Reads only the Progress node from a save_* snapshot directory.</summary>
    /// <remarks>The returned node is owned by the caller and must be disposed.</remarks>
    DataSourceNode? ReadProgressNodeFromSnapshot(string saveId);

    /// <summary>Attempts to read the payload of the specified level from current/; returns null when not found.</summary>
    /// <remarks>
    ///     The returned payload and all of its node trees are owned by the
    ///     caller and must be disposed when no longer needed.
    /// </remarks>
    LevelPayload? TryReadLevelPayloadFromCurrent(string levelId);

    /// <summary>Attempts to read the payload of the specified level from a save_* snapshot directory; returns null when not found.</summary>
    /// <remarks>
    ///     The returned payload and all of its node trees are owned by the
    ///     caller and must be disposed when no longer needed.
    /// </remarks>
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
    /// <remarks>
    ///     The returned payload and all of its node trees are owned by the
    ///     caller and must be disposed when no longer needed.
    /// </remarks>
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

    /// <summary>
    ///     Copies the extra/ subdirectory from the specified save snapshot of
    ///     <paramref name="sourceStorage" /> into this service's current/
    ///     directory. Used when payload data and extra files are loaded from
    ///     different storage roots (for example, an initial save stored under
    ///     a read-only <c>res://</c> root and restored into the writable
    ///     runtime save root). Silently skipped when the source snapshot has
    ///     no extra/ directory.
    /// </summary>
    /// <param name="sourceStorage">The storage service that owns the source save snapshot.</param>
    /// <param name="saveId">The save slot ID in <paramref name="sourceStorage" />.</param>
    /// <exception cref="System.ArgumentNullException">
    ///     Thrown when <paramref name="sourceStorage" /> is null.
    /// </exception>
    /// <exception cref="System.InvalidOperationException">
    ///     Thrown when the destination implementation cannot interpret the
    ///     source service's snapshot layout. The default implementation
    ///     supports only a default source; custom source/destination pairs
    ///     must be implemented as a pair.
    /// </exception>
    void RestoreExtraFilesFromSnapshot(ISaveStorageService sourceStorage, string saveId);
}
