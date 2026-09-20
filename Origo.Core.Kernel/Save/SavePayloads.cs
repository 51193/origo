using System;
using System.Collections.Generic;
using Origo.Core.DataSource;

namespace Origo.Core.Save;

/// <summary>
///     Represents a single level within a save, containing the serialized
///     SND scene and Session blackboard.
/// </summary>
/// <remarks>
///     The caller owns the contained <see cref="DataSourceNode" /> trees and
///     must dispose them when no longer needed; the framework disposes
///     payloads it reads or builds at internal load/mount/write boundaries.
/// </remarks>
public sealed class LevelPayload
{
    /// <summary>
    ///     The unique identifier of the level.
    /// </summary>
    public string LevelId { get; set; } = string.Empty;

    /// <summary>
    ///     The serialized node of the SND scene for this level.
    /// </summary>
    public DataSourceNode SndSceneNode { get; set; } = DataSourceNode.CreateNull();

    /// <summary>
    ///     The serialized node of the Session blackboard for this level.
    /// </summary>
    public DataSourceNode SessionNode { get; set; } = DataSourceNode.CreateNull();

    /// <summary>
    ///     The session-level string-stack state machine snapshot node
    ///     (corresponds to <c>session_state_machines.json</c> in the same
    ///     directory as <c>session.json</c>).
    /// </summary>
    public DataSourceNode SessionStateMachinesNode { get; set; } = DataSourceNode.CreateNull();
}

/// <summary>
///     The data package required for a complete save. The domain payload is
///     <see cref="DataSourceNode" />; on-disk encoding is handled by
///     <see cref="IDataSourceIoGateway" />.
/// </summary>
/// <remarks>
///     The caller owns every contained <see cref="DataSourceNode" /> tree and
///     must dispose them when no longer needed; the framework disposes
///     payloads it reads or builds at internal load/mount/write boundaries.
/// </remarks>
public sealed class SaveGamePayload
{
    /// <summary>
    ///     The current save format version number. Incremented on change,
    ///     used to validate format compatibility when loading.
    /// </summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>
    ///     Framework-reserved <c>meta.map</c> key carrying the save format
    ///     version written by <see cref="Origo.Core.Save.Storage.SavePayloadWriter" />
    ///     and validated by the reader.
    /// </summary>
    public const string FormatVersionMetaKey = "origo.format_version";

    /// <summary>
    ///     The unique identifier of the save slot.
    /// </summary>
    public string SaveId { get; set; } = string.Empty;

    /// <summary>
    ///     The identifier of the currently active level.
    /// </summary>
    public string ActiveLevelId { get; set; } = string.Empty;

    /// <summary>
    ///     The serialized node of the progress-level blackboard.
    /// </summary>
    public DataSourceNode ProgressNode { get; set; } = DataSourceNode.CreateNull();

    /// <summary>
    ///     The progress-level string-stack state machine snapshot node
    ///     (corresponds to <c>progress_state_machines.json</c> in the same
    ///     directory as <c>progress.json</c>).
    /// </summary>
    public DataSourceNode ProgressStateMachinesNode { get; set; } = DataSourceNode.CreateNull();

    /// <summary>
    ///     Optional save display metadata (sidecar map content).
    ///     Does not participate in ProgressBlackboard semantics; used only for
    ///     standalone fast reads.
    /// </summary>
    public IReadOnlyDictionary<string, string>? CustomMeta { get; set; }

    /// <summary>
    ///     All level save data indexed by level ID.
    /// </summary>
    public Dictionary<string, LevelPayload> Levels { get; set; } = [];
}

/// <summary>
///     Releases the <see cref="DataSourceNode" /> trees held by save payloads
///     at internal mount/write boundaries; the nodes are not needed after the
///     payload has been persisted or deserialized into runtime state.
/// </summary>
internal static class SavePayloadDisposal
{
    public static void Dispose(SaveGamePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        payload.ProgressNode.Dispose();
        payload.ProgressStateMachinesNode.Dispose();

        foreach (var level in payload.Levels.Values)
            Dispose(level);
    }

    public static void Dispose(LevelPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        payload.SndSceneNode.Dispose();
        payload.SessionNode.Dispose();
        payload.SessionStateMachinesNode.Dispose();
    }
}
