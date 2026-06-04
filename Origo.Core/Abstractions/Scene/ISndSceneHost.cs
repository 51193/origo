using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Abstractions.Scene;

/// <summary>
///     面向 Core 的 SND 场景宿主抽象。
///     负责实体的容器管理（创建/查找/移除）及单实体/批量的 Spawn 钩子编排。
///     Load/Save/Quit/Dead 的钩子编排由 <see cref="Origo.Core.Snd.Scene.SndRuntime" />
///     和会话生命周期统一处理。
/// </summary>
public interface ISndSceneHost : ISndSceneAccess
{
    /// <summary>
    ///     从一份元数据在场景中创建实体，恢复数据/策略/节点，并触发 AfterSpawn 钩子。
    ///     单实体 spawn：等效于 Recover + AfterSpawn。
    ///     <para>
    ///         注意：此方法不执行重名校验。需要重名保护时应通过
    ///         <see cref="Origo.Core.Snd.Scene.SndRuntime.Spawn" /> 调用。
    ///     </para>
    /// </summary>
    ISndEntity Spawn(SndMetaData metaData);

    /// <summary>
    ///     批量生成多个 SND 实体：先全部恢复数据/策略/节点，再统一触发 AfterSpawn 钩子。
    ///     所有实体在钩子触发前已全部注册到查找集合，实现加载顺序无关的跨实体互操作。
    ///     <para>
    ///         注意：此方法不执行重名校验。需要重名保护时应通过
    ///         <see cref="Origo.Core.Snd.Scene.SndRuntime.SpawnMany" /> 调用。
    ///     </para>
    /// </summary>
    void SpawnMany(IEnumerable<SndMetaData> metaList);

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
