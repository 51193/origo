using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Abstractions.Scene;

/// <summary>
///     面向 Core 的 SND 场景宿主抽象。
///     仅负责实体的容器管理（创建/查找/移除），不涉及任何策略生命周期钩子。
///     所有钩子编排由会话生命周期统一处理
///     （<see cref="Origo.Core.Runtime.Lifecycle.SessionRun" /> 与 <see cref="Origo.Core.Runtime.Lifecycle.SessionManager" />）。
///     <para>
///         适配层实现此接口时不应触发任何策略钩子——钩子是 Core 层的专属职责。
///     </para>
/// </summary>
public interface ISndSceneHost : ISndSceneAccess
{
    /// <summary>
    ///     从元数据在场景中创建实体，恢复数据/策略/节点到内存。
    ///     不触发任何生命周期钩子（AfterSpawn / AfterLoad 等）。
///     钩子应由调用方（<see cref="Origo.Core.Snd.Scene.SndEntityFactory" /> / <see cref="Origo.Core.Runtime.Lifecycle.SessionRun" />）在适当阶段统一触发。
///     <para>
///         注意：此方法不执行重名校验，框架当前也不在上层强制重名唯一性。
///     </para>
    /// </summary>
    ISndEntity CreateEntity(SndMetaData metaData);

    /// <summary>
    ///     获取当前场景中所有仍然存活的实体视图。
    /// </summary>
    IReadOnlyCollection<ISndEntity> GetEntities();

    /// <summary>
    ///     根据实体名称查找对应实体。
    /// </summary>
    ISndEntity? FindByName(string name);

    /// <summary>
    ///     对所有存活实体执行每帧更新。
    ///     不支持帧更新的宿主实现应为空操作。
    ///     此方法仅负责逐实体派发帧更新，不负责 Deferred Actions 冲刷等全局管线的编排。
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
    ///     按名称移除单个实体（仅从集合中移除并释放引擎资源，不触发钩子、不释放策略）。
    ///     钩子与策略释放由框架在调用此方法前统一批量执行。
    ///     仅由框架在生命周期切换时内部调用。
    /// </summary>
    void RemoveEntity(string name);

    /// <summary>
    ///     清空场景中所有实体的集合引用。
    ///     钩子与策略释放应由调用方在调用此方法前统一批量执行。
    ///     仅由框架在生命周期切换时内部调用。
    /// </summary>
    void RemoveAllEntities();
}
