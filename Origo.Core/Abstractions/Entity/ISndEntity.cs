namespace Origo.Core.Abstractions.Entity;

/// <summary>
///     抽象 SND 实体的最小接口，使策略与数据层不依赖具体引擎节点类型。
///     继承 <see cref="ISndDataAccess" />、<see cref="ISndNodeAccess" /> 和
///     <see cref="ISndStrategyAccess" /> 以保持向后兼容。
/// </summary>
public interface ISndEntity : ISndDataAccess, ISndNodeAccess, ISndStrategyAccess
{
    /// <summary>
    ///     稳定的实体名（对应 <see cref="Snd.Metadata.SndMetaData.Name" />），可用于场景内查找与跨系统引用。
    /// </summary>
    string Name { get; }
}
