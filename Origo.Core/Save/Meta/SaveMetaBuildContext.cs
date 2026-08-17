using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Scene;

namespace Origo.Core.Save.Meta;

/// <summary>
///     Read-only context passed to <see cref="ISaveMetaContributor" /> during
///     a single save operation (for display meta.map, unrelated to business
///     payload serialization).
/// </summary>
public readonly struct SaveMetaBuildContext
{
    /// <summary>
    ///     Creates the read-only context for a save operation.
    /// </summary>
    public SaveMetaBuildContext(
        string saveId,
        string currentLevelId,
        IBlackboard progress,
        IBlackboard session,
        ISndSceneReadAccess sceneAccess)
    {
        ArgumentNullException.ThrowIfNull(saveId);
        ArgumentNullException.ThrowIfNull(currentLevelId);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sceneAccess);
        SaveId = saveId;
        CurrentLevelId = currentLevelId;
        Progress = progress;
        Session = session;
        SceneAccess = sceneAccess;
    }

    /// <summary>The target slot ID of the current save operation.</summary>
    public string SaveId { get; }

    /// <summary>The currently active level ID.</summary>
    public string CurrentLevelId { get; }

    /// <summary>The progress-level blackboard (read-only snapshot).</summary>
    public IBlackboard Progress { get; }

    /// <summary>The current session-level blackboard (read-only snapshot).</summary>
    public IBlackboard Session { get; }

    /// <summary>Read-only access interface for the current scene; can be used
    /// to serialize entity metadata lists.</summary>
    public ISndSceneReadAccess SceneAccess { get; }
}
