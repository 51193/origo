using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Save;
using Origo.Core.Save.Serialization;
using Origo.Core.Save.Storage;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.StateMachine;

namespace Origo.Core.Snd;

/// <summary>
///     Structured level builder that provides a fluent API for offline level scene construction
///     at the Core layer. Uses <see cref="StubSndSceneHost" /> as an in-memory scene host,
///     supports adding entities and setting session blackboard key-value pairs,
///     and ultimately produces a <see cref="LevelPayload" /> via <see cref="Build" />
///     or directly persists to disk via <see cref="Commit" />.
///     <para>
///         Decoupled from storage implementation via <see cref="ISaveStorageService" />,
///         sharing the same storage abstraction as SessionRun without directly depending
///         on SavePathLayout or static Writer.
///     </para>
/// </summary>
internal sealed class LevelBuilder
{
    private readonly StubSndSceneHost _sceneHost = new();
    private readonly Blackboard.Blackboard _sessionBlackboard = new();
    private readonly SndWorld _sndWorld;
    private readonly ISaveStorageService _storageService;
    private bool _built;

    /// <summary>
    ///     Creates a level builder instance.
    /// </summary>
    /// <param name="levelId">The unique identifier of the level.</param>
    /// <param name="sndWorld">The SND world instance providing serialization support.</param>
    /// <param name="storageService">The save storage service used by <see cref="Commit" /> for persistence.</param>
    public LevelBuilder(
        string levelId,
        SndWorld sndWorld,
        ISaveStorageService storageService)
    {
        if (string.IsNullOrWhiteSpace(levelId))
            throw new ArgumentException("Level id cannot be null or whitespace.", nameof(levelId));
        ArgumentNullException.ThrowIfNull(sndWorld);
        ArgumentNullException.ThrowIfNull(storageService);

        LevelId = levelId;
        _sndWorld = sndWorld;
        _storageService = storageService;
    }

    /// <summary>
    ///     The unique identifier of the level.
    /// </summary>
    public string LevelId { get; }

    /// <summary>
    ///     In-memory scene host that allows external code to directly query added entities.
    /// </summary>
    public ISndSceneHost SceneHost => _sceneHost;

    /// <summary>
    ///     Session-level blackboard that allows external code to directly read set key-value pairs.
    /// </summary>
    public IBlackboard SessionBlackboard => _sessionBlackboard;

    /// <summary>
    ///     Adds an entity to the level.
    /// </summary>
    public LevelBuilder AddEntity(SndMetaData metaData)
    {
        ThrowIfBuilt();
        ArgumentNullException.ThrowIfNull(metaData);
        if (string.IsNullOrWhiteSpace(metaData.Name))
            throw new ArgumentException("SndMetaData.Name cannot be null or whitespace.", nameof(metaData));
        if (_sceneHost.FindByName(metaData.Name) is not null)
            throw new InvalidOperationException($"Entity '{metaData.Name}' already exists in this level builder.");

        _sceneHost.CreateEntity(metaData);
        return this;
    }

    /// <summary>
    ///     Adds an entity by template key with an optional name override.
    /// </summary>
    public LevelBuilder AddEntityFromTemplate(string templateKey, string? overrideName = null)
    {
        ThrowIfBuilt();
        if (string.IsNullOrWhiteSpace(templateKey))
            throw new ArgumentException("Template key cannot be null or whitespace.", nameof(templateKey));

        var cloned = _sndWorld.ResolveTemplate(templateKey);
        if (!string.IsNullOrWhiteSpace(overrideName))
            cloned.Name = overrideName;

        return AddEntity(cloned);
    }

    /// <summary>
    ///     Adds multiple entities in batch.
    /// </summary>
    public LevelBuilder AddEntities(IEnumerable<SndMetaData> metaList)
    {
        ThrowIfBuilt();
        ArgumentNullException.ThrowIfNull(metaList);
        foreach (var meta in metaList)
            AddEntity(meta);
        return this;
    }

    /// <summary>
    ///     Sets a key-value pair on the session blackboard.
    /// </summary>
    public LevelBuilder SetSessionData<T>(string key, T value)
    {
        ThrowIfBuilt();
        _sessionBlackboard.SetValue(key, value);
        return this;
    }

    /// <summary>
    ///     Produces a <see cref="LevelPayload" /> and marks the builder as built.
    ///     No further modifications (adding entities or setting blackboard) are allowed after building.
    /// </summary>
    public LevelPayload Build()
    {
        ThrowIfBuilt();
        _built = true;

        var sceneSerializer = new SndSceneSerializer(_sndWorld);
        var blackboardSerializer = new BlackboardSerializer(_sndWorld.ConverterRegistry);
        var emptyStateMachinesNode = _sndWorld.ConverterRegistry.Write(new StateMachineContainerPayload());

        return new LevelPayload
        {
            LevelId = LevelId,
            SndSceneNode = sceneSerializer.Build(_sceneHost),
            SessionNode = blackboardSerializer.Serialize(_sessionBlackboard),
            SessionStateMachinesNode = emptyStateMachinesNode
        };
    }

    /// <summary>
    ///     Produces a <see cref="LevelPayload" /> and writes it to the current/ directory.
    ///     Equivalent to <c>Build()</c> + <see cref="ISaveStorageService.WriteLevelPayloadOnly" />.
    /// </summary>
    public LevelPayload Commit()
    {
        var payload = Build();

        _storageService.WriteLevelPayloadOnlyToCurrent(payload);

        return payload;
    }

    private void ThrowIfBuilt()
    {
        if (_built)
            throw new InvalidOperationException(
                "LevelBuilder has already been built. Create a new builder instance for further modifications.");
    }
}
