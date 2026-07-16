using Origo.Core.Abstractions.Entity;

namespace Origo.Core.Snd.Strategy;

/// <summary>
///     Unified root base class for all strategy types, carrying infrastructure identity
///     such as pool registration, index annotations, and stateless constraints.
///     Specific lifecycle hooks are defined by derived base classes such as
///     <see cref="LifecycleStrategyBase" /> and <see cref="StateMachine.StateMachineStrategyBase" />.
///     <para>
///         <b>
///             Important: Strategy instances are shared and reused across multiple callers
///             via <see cref="SndStrategyPool" />. Concrete strategy implementations must
///             remain stateless — declaring instance fields or properties to store runtime
///             data is forbidden. Mutable state on the entity side must be stored in the
///             entity's Data (via <see cref="ISndEntity.SetData{T}" /> / <see cref="ISndEntity.GetData{T}" />).
///             During strategy registration, the strategy type is validated; if instance fields
///             or writable instance properties are present, registration is rejected and an error is logged.
///         </b>
///     </para>
/// </summary>
public abstract class BaseStrategy
{
}
