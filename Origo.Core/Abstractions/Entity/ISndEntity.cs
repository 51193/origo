namespace Origo.Core.Abstractions.Entity;

/// <summary>
///     抽象 SND 实体的最小接口，使策略与数据层不依赖具体引擎节点类型。
///     继承 <see cref="ISndDataAccess" />、<see cref="ISndNodeAccess" />、
///     <see cref="ISndStrategyAccess" /> 和 <see cref="ISndActiveStrategyAccess" />
///     以保持向后兼容。
/// </summary>
public interface ISndEntity : ISndDataAccess, ISndNodeAccess, ISndStrategyAccess, ISndActiveStrategyAccess
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
