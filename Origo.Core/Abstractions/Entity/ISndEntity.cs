namespace Origo.Core.Abstractions.Entity;

/// <summary>
///     SND 实体的完整聚合接口，组合了数据存取、节点管理、策略控制、生命周期订阅与跨实体观察能力。
///     外部代码仅依赖此接口即可操作实体的全部公共功能。
/// </summary>
public interface ISndEntity : ISndDataAccess, ISndNodeAccess, ISndStrategyAccess, ISndActiveStrategyAccess,
    ISndEntityLifecycleAccess, ISndObservation
{
    /// <summary>
    ///     稳定的实体名（对应 <see cref="Snd.Metadata.SndMetaData.Name" />），可用于场景内查找与跨系统引用。
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     标记为待销毁状态。框架在帧末统一执行销毁（业务延迟队列之后、系统延迟队列之前）。
    ///     策略应在操作实体前通过此标志位判断实体是否仍然存活。
    /// </summary>
    bool IsPendingKill { get; }
}
