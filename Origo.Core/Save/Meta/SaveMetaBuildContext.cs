using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Scene;

namespace Origo.Core.Save.Meta;

/// <summary>
///     单次存档时传给 <see cref="ISaveMetaContributor" /> 的只读上下文（展示用 meta.map，与业务载荷序列化无关）。
/// </summary>
public readonly struct SaveMetaBuildContext
{
    public SaveMetaBuildContext(
        string saveId,
        string currentLevelId,
        IBlackboard progress,
        IBlackboard session,
        ISndSceneAccess sceneAccess)
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

    /// <summary>当前存档操作的目标槽位 ID。</summary>
    public string SaveId { get; }

    /// <summary>当前激活的关卡 ID。</summary>
    public string CurrentLevelId { get; }

    /// <summary>流程级黑板（只读快照）。</summary>
    public IBlackboard Progress { get; }

    /// <summary>当前会话级黑板（只读快照）。</summary>
    public IBlackboard Session { get; }

    /// <summary>当前场景的只读访问接口，可用于序列化实体元数据列表。</summary>
    public ISndSceneAccess SceneAccess { get; }
}
