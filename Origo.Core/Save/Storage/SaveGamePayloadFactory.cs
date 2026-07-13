using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Scene;
using Origo.Core.DataSource;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save.Serialization;

namespace Origo.Core.Save.Storage;

internal sealed class SaveGamePayloadFactory
{
    private readonly BlackboardSerializer _blackboardSerializer;
    private readonly IBlackboard _progress;
    private readonly SndSceneSerializer _sceneSerializer;
    private readonly IBlackboard _session;

    public SaveGamePayloadFactory(
        IBlackboard progress,
        IBlackboard session,
        BlackboardSerializer blackboardSerializer,
        SndSceneSerializer sceneSerializer)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(blackboardSerializer);
        ArgumentNullException.ThrowIfNull(sceneSerializer);
        _progress = progress;
        _session = session;
        _blackboardSerializer = blackboardSerializer;
        _sceneSerializer = sceneSerializer;
    }

    public SaveGamePayload Create(
        ISndSceneAccess sceneAccess,
        string saveId,
        string currentLevelId,
        IReadOnlyDictionary<string, string>? customMeta,
        DataSourceNode progressStateMachinesNode,
        DataSourceNode sessionStateMachinesNode)
    {
        ArgumentNullException.ThrowIfNull(sceneAccess);
        if (string.IsNullOrWhiteSpace(saveId))
            throw new ArgumentException("Save id cannot be null or whitespace.", nameof(saveId));
        if (string.IsNullOrWhiteSpace(currentLevelId))
            throw new ArgumentException("Current level id cannot be null or whitespace.", nameof(currentLevelId));
        ArgumentNullException.ThrowIfNull(progressStateMachinesNode);
        ArgumentNullException.ThrowIfNull(sessionStateMachinesNode);

        TopologyInvariant.EnsureActiveLevel(_progress, currentLevelId, "before building save payload");

        var progressNode = _blackboardSerializer.Serialize(_progress);
        var sessionNode = _blackboardSerializer.Serialize(_session);
        var sndSceneNode = _sceneSerializer.Build(sceneAccess);

        var levelPayload = new LevelPayload
        {
            LevelId = currentLevelId,
            SndSceneNode = sndSceneNode,
            SessionNode = sessionNode,
            SessionStateMachinesNode = sessionStateMachinesNode
        };

        return new SaveGamePayload
        {
            SaveId = saveId,
            ActiveLevelId = currentLevelId,
            ProgressNode = progressNode,
            ProgressStateMachinesNode = progressStateMachinesNode,
            CustomMeta = customMeta is null
                ? null
                : new Dictionary<string, string>(customMeta, StringComparer.Ordinal),
            Levels = new Dictionary<string, LevelPayload> { [currentLevelId] = levelPayload }
        };
    }
}
