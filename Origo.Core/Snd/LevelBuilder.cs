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
///     结构化关卡构建器，提供流式 API 在 Core 层离线构建关卡场景。
///     使用 <see cref="MemorySndSceneHost" /> 作为内存场景宿主，
///     支持添加实体、设置会话黑板键值对，最终通过 <see cref="Build" /> 生成
///     <see cref="LevelPayload" /> 或通过 <see cref="Commit" /> 直接持久化到磁盘。
///     <para>
///         通过 <see cref="ISaveStorageService" /> 与存储实现解耦，
///         与 SessionRun 共享同一套存储抽象，不直接依赖 SavePathLayout 或静态 Writer。
///     </para>
/// </summary>
internal sealed class LevelBuilder
{
    private readonly MemorySndSceneHost _sceneHost = new();
    private readonly Blackboard.Blackboard _sessionBlackboard = new();
    private readonly SndWorld _sndWorld;
    private readonly ISaveStorageService _storageService;
    private bool _built;

    /// <summary>
    ///     创建关卡构建器实例。
    /// </summary>
    /// <param name="levelId">关卡唯一标识符。</param>
    /// <param name="sndWorld">SND 世界实例，提供序列化支持。</param>
    /// <param name="storageService">存档读写服务，用于 <see cref="Commit" /> 持久化。</param>
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
    ///     关卡唯一标识符。
    /// </summary>
    public string LevelId { get; }

    /// <summary>
    ///     内存场景宿主，允许外部直接查询已添加的实体。
    /// </summary>
    public ISndSceneHost SceneHost => _sceneHost;

    /// <summary>
    ///     会话级黑板，允许外部直接读取已设置的键值对。
    /// </summary>
    public IBlackboard SessionBlackboard => _sessionBlackboard;

    /// <summary>
    ///     向关卡添加一个实体。
    /// </summary>
    public LevelBuilder AddEntity(SndMetaData metaData)
    {
        ThrowIfBuilt();
        ArgumentNullException.ThrowIfNull(metaData);
        if (string.IsNullOrWhiteSpace(metaData.Name))
            throw new ArgumentException("SndMetaData.Name cannot be null or whitespace.", nameof(metaData));
        if (_sceneHost.FindByName(metaData.Name) is not null)
            throw new InvalidOperationException($"Entity '{metaData.Name}' already exists in this level builder.");

        _sceneHost.Spawn(metaData);
        return this;
    }

    /// <summary>
    ///     按模板名称与可选名称覆盖添加实体。
    /// </summary>
    public LevelBuilder AddEntityFromTemplate(string templateKey, string? overrideName = null)
    {
        ThrowIfBuilt();
        if (string.IsNullOrWhiteSpace(templateKey))
            throw new ArgumentException("Template key cannot be null or whitespace.", nameof(templateKey));

        var template = _sndWorld.ResolveTemplate(templateKey);
        var cloned = SndWorld.CloneMetaData(template);
        if (!string.IsNullOrWhiteSpace(overrideName))
            cloned.Name = overrideName;

        return AddEntity(cloned);
    }

    /// <summary>
    ///     批量添加多个实体。
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
    ///     向会话黑板设置键值对。
    /// </summary>
    public LevelBuilder SetSessionData<T>(string key, T value)
    {
        ThrowIfBuilt();
        _sessionBlackboard.Set(key, value);
        return this;
    }

    /// <summary>
    ///     生成 <see cref="LevelPayload" /> 并标记为已构建。
    ///     构建后不可再修改（添加实体或设置黑板）。
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
    ///     生成 <see cref="LevelPayload" /> 并写入 current/ 目录。
    ///     等效于 <c>Build()</c> + <see cref="ISaveStorageService.WriteLevelPayloadOnly" />。
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
