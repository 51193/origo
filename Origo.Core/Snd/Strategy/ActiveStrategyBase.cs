using Origo.Core.Abstractions.Entity;

namespace Origo.Core.Snd.Strategy;

/// <summary>
///     主动策略基类。与 <see cref="LifecycleStrategyBase" /> 并列继承 <see cref="BaseStrategy" />，
///     但仅支持外部主动调用（<see cref="Invoke" />），不参与帧更新或生命周期钩子。
///     <para>
///         策略实例无状态、共享、池化，由 <see cref="SndStrategyPool" /> 统一管理。
///         通过 <see cref="ISndActiveStrategyAccess.InvokeStrategy" /> 在实体上调用。
///     </para>
/// </summary>
public abstract class ActiveStrategyBase : BaseStrategy
{
    /// <summary>
    ///     主动调用策略并返回结果。
    ///     entity 为调用此策略的实体实例；ctx 为当前游戏上下文；input 为可选的输入参数。
    /// </summary>
    public abstract object? Invoke(ISndEntity entity, ISndContext ctx, object? input);
}
