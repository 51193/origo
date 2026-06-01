using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Abstractions.Scene;

/// <summary>
///     面向 Core 的 SND 场景宿主抽象。
///     在 ISndSceneAccess 的基础上，补充对实体集合与按元数据生成实体的操作。
/// </summary>
public interface ISndSceneHost : ISndSceneAccess
{
    /// <summary>
    ///     从一份元数据生成并加入一个实体，返回该实体的抽象接口。
    ///     具体的节点创建与挂载由实现负责。
    ///     <para>
    ///         注意：此方法不执行重名校验。需要重名保护时应通过
    ///         <see cref="Origo.Core.Snd.Scene.SndRuntime.Spawn" /> 调用，
    ///         该方法会在委托到此方法前先检查名称是否已被占用。
    ///     </para>
    /// </summary>
    ISndEntity Spawn(SndMetaData metaData);

    /// <summary>
    ///     获取当前场景中所有仍然存活的实体视图。
    /// </summary>
    IReadOnlyCollection<ISndEntity> GetEntities();

    /// <summary>
    ///     根据实体名称查找对应实体。
    /// </summary>
    ISndEntity? FindByName(string name);

    /// <summary>
    ///     对所有存活实体执行 Process 帧更新。
    ///     不支持帧更新的宿主实现应为空操作。
    /// </summary>
    /// <param name="delta">帧间隔时间（秒）。</param>
    void ProcessAll(double delta);

    /// <summary>
    ///     立即将指定实体标记为待销毁状态。
    ///     实体在帧末统一销毁（业务延迟队列之后、系统延迟队列之前）。
    /// </summary>
    /// <exception cref="InvalidOperationException">若实体不存在或已标记为待销毁。</exception>
    void RequestKillEntity(string name);

    /// <summary>
    ///     按名称销毁单个实体（立即执行，从集合移除并触发清理逻辑）。
    ///     仅由框架统一 Kill 步骤调用，业务代码应使用 <see cref="RequestKillEntity" />。
    /// </summary>
    void DeadByName(string name);

    /// <summary>
    ///     清除场景中所有实体（触发 Quit 流程）。
    ///     仅由框架在生命周期切换时内部调用。
    /// </summary>
    void ClearAll();
}
