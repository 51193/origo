using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Scene;
using Origo.Core.DataSource;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;

namespace Origo.Core.Save.Serialization;

/// <summary>
///     Save/load context for a single ProgressRun. Composes
///     <see cref="BlackboardSerializer" />, <see cref="SndSceneSerializer" />,
///     and <see cref="SaveGamePayloadFactory" />. Progress and session
///     blackboard deserialization is transactionally safe: on failure,
///     the original state is restored.
/// </summary>
internal sealed class SaveContext
{
    private readonly BlackboardSerializer _blackboardSerializer;
    private readonly SaveGamePayloadFactory _payloadFactory;
    private readonly SndSceneSerializer _sceneSerializer;

    public SaveContext(
        IBlackboard progress,
        IBlackboard session,
        SndWorld sndWorld)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sndWorld);
        Progress = progress;
        Session = session;
        SndWorld = sndWorld;

        _blackboardSerializer = new BlackboardSerializer(SndWorld.ConverterRegistry);
        _sceneSerializer = new SndSceneSerializer(SndWorld);
        _payloadFactory = new SaveGamePayloadFactory(Progress, Session, _blackboardSerializer, _sceneSerializer);
    }

    public IBlackboard Progress { get; }

    public IBlackboard Session { get; }

    public SndWorld SndWorld { get; }

    public DataSourceNode SerializeProgress() => _blackboardSerializer.Serialize(Progress);

    public void DeserializeProgress(DataSourceNode serializedNode)
    {
        ArgumentNullException.ThrowIfNull(serializedNode);
        var snapshot = Progress.SerializeAll();
        try
        {
            _blackboardSerializer.DeserializeInto(Progress, serializedNode);
        }
        catch
        {
            Progress.DeserializeAll(snapshot);
            throw;
        }
    }

    public DataSourceNode SerializeSession() => _blackboardSerializer.Serialize(Session);

    public void DeserializeSession(DataSourceNode serializedNode)
    {
        ArgumentNullException.ThrowIfNull(serializedNode);
        var snapshot = Session.SerializeAll();
        try
        {
            _blackboardSerializer.DeserializeInto(Session, serializedNode);
        }
        catch
        {
            Session.DeserializeAll(snapshot);
            throw;
        }
    }

    public DataSourceNode BuildSndScene(ISndSceneAccess sceneAccess) => _sceneSerializer.Build(sceneAccess);

    public void RecoverSndScene(ISndSceneAccess sceneHost, DataSourceNode serializedNode) =>
        _sceneSerializer.RecoverInto(sceneHost, serializedNode);

    public SaveGamePayload SaveGame(
        ISndSceneAccess sceneAccess,
        string saveId,
        string currentLevelId,
        IReadOnlyDictionary<string, string>? customMeta = null,
        DataSourceNode? progressStateMachinesNode = null,
        DataSourceNode? sessionStateMachinesNode = null)
    {
        return _payloadFactory.Create(
            sceneAccess,
            saveId,
            currentLevelId,
            customMeta,
            progressStateMachinesNode ?? DataSourceNode.CreateNull(),
            sessionStateMachinesNode ?? DataSourceNode.CreateNull());
    }
}
