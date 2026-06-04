using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Abstractions.Scene;

/// <summary>
///     面向 Core 的 SND 场景宿主抽象。
///     负责实体的容器管理（创建/查找/移除），不负责策略生命周期钩子触发。
///     钩子触发由 <see cref="Origo.Core.Snd.Scene.SndRuntime" /> 和会话生命周期统一编排。
/// </summary>
public interface ISndSceneHost : ISndSceneAccess
{
    /// <summary>
    ///     从一份元数据在场景中创建并恢复一个实体，返回该实体的抽象接口。
    ///     仅执行实体的创建和数据/策略恢复，不触发 AfterSpawn 钩子。
    ///     钩子应由调用方（SndRuntime.Spawn）在返回后触发。
    ///     <para>
    ///         注意：此方法不执行重名校验。需要重名保护时应通过
    ///         <see cref="Origo.Core.Snd.Scene.SndRuntime.Spawn" /> 调用。
    ///     </para>
    /// </summary>
    ISndEntity SpawnEntity(SndMetaData metaData);

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
    ///     按名称摧毁单个实体（仅执行拆卸，不触发钩子）。
    ///     BeforeDead 钩子由框架在调用此方法前统一批量触发。
    ///     仅由框架 KillPendingEntities 步骤调用，业务代码应使用 <see cref="RequestKillEntity" />。
    /// </summary>
    void TeardownEntity(string name);

    /// <summary>
    ///     清空场景中所有实体的集合引用。
    ///     BeforeQuit 钩子和策略释放应由调用方在调用此方法前统一批量执行。
    ///     仅由框架在生命周期切换时内部调用。
    /// </summary>
    void RemoveAllEntities();
}
