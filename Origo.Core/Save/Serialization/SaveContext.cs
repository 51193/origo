using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Scene;
using Origo.Core.DataSource;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;

namespace Origo.Core.Save.Serialization;

/// <summary>
///     存档上下文，封装 Progress（流程级）与 Session（会话级）黑板以及 SND 世界的序列化/反序列化能力。
///     不执行任何文件 I/O，所有输入输出均为序列化文本字符串或内存模型。由 ProgressRun / SessionRun 在生命周期内持有。
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

    /// <summary>
    ///     流程级黑板，保存跨关卡的全局进度数据。
    /// </summary>
    public IBlackboard Progress { get; }

    /// <summary>
    ///     会话级黑板，保存当前关卡内的临时状态数据。
    /// </summary>
    public IBlackboard Session { get; }

    /// <summary>
    ///     当前 SND 世界实例，提供场景数据的注册与序列化支持。
    /// </summary>
    public SndWorld SndWorld { get; }

    /// <summary>
    ///     将 Progress 黑板序列化为文本（使用 TypedData 保留类型信息）。
    /// </summary>
    public DataSourceNode SerializeProgress() => _blackboardSerializer.Serialize(Progress);

    /// <summary>
    ///     将序列化文本恢复到 Progress 黑板。
    /// </summary>
    public void DeserializeProgress(DataSourceNode serializedNode)
    {
        ArgumentNullException.ThrowIfNull(serializedNode);
        _blackboardSerializer.DeserializeInto(Progress, serializedNode);
    }

    /// <summary>
    ///     将 Session 黑板序列化为文本（使用 TypedData 保留类型信息）。
    /// </summary>
    public DataSourceNode SerializeSession() => _blackboardSerializer.Serialize(Session);

    /// <summary>
    ///     将序列化文本恢复到 Session 黑板。
    /// </summary>
    public void DeserializeSession(DataSourceNode serializedNode)
    {
        ArgumentNullException.ThrowIfNull(serializedNode);
        _blackboardSerializer.DeserializeInto(Session, serializedNode);
    }

    /// <summary>
    ///     将指定 SND 场景序列化为文本字符串。
    /// </summary>
    public DataSourceNode SerializeSndScene(ISndSceneAccess sceneAccess) => _sceneSerializer.Serialize(sceneAccess);

    /// <summary>
    ///     将序列化文本恢复到 SND 场景。
    ///     支持两种形式：完整的 SndMetaData 对象，或 { "sndName": "...", "templateKey": "..." } 简写，
    ///     与入口配置保持一致，统一通过 SndMappings.ResolveMetaListFromJsonArray 解析。
    /// </summary>
    public void DeserializeSndScene(ISndSceneAccess sceneAccess, DataSourceNode serializedNode,
        bool clearBeforeLoad = true) =>
        _sceneSerializer.DeserializeInto(sceneAccess, serializedNode, clearBeforeLoad);

    /// <summary>
    ///     收集当前存档所需的全部数据，生成完整的 <see cref="SaveGamePayload" />。
    /// </summary>
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
