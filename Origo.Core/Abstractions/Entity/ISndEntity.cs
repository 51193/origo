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
    ///     销毁实体，触发 BeforeDead 钩子并释放数据、节点与策略。
    ///     需由场景宿主在从集合移除前调用此方法。
    /// </summary>
    void Kill();
}
