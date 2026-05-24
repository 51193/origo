using Origo.Core.Abstractions.Entity;

namespace Origo.Core.Snd.Strategy;

/// <summary>
///     所有策略类型的统一根基类，承载对象池注册、索引注解与无状态约束等基础设施身份。
///     具体生命周期钩子由 <see cref="EntityStrategyBase" />、<see cref="StateMachine.StateMachineStrategyBase" /> 等分支基类定义。
///     <para>
///         <b>
///             重要：策略实例通过 <see cref="SndStrategyPool" /> 在多个调用方之间共享复用。
///             具体策略实现必须保持无状态——禁止声明实例字段或属性来存储运行时数据。
///             实体侧可变状态必须存放在实体的 Data 中（通过 <see cref="ISndEntity.SetData{T}" /> / <see cref="ISndEntity.GetData{T}" />）。
///             策略注册阶段会校验策略类型，若存在实例字段或可写实例属性将拒绝注册并记录错误日志。
///         </b>
///     </para>
/// </summary>
public abstract class BaseStrategy
{
}
