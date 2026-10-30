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
///             via <c>SndStrategyPool</c>. Concrete strategy implementations must
///             remain stateless — mutable instance fields and writable instance
///             properties are rejected when the strategy is registered, so runtime state
///             cannot be stored on the strategy instance. Mutable state on the entity side
///             must be stored in the entity's Data
///             (via <see cref="ISndDataAccess.SetData{T}" /> / <see cref="ISndDataAccess.GetData{T}" />).
///             A readonly instance field is allowed only when the value it holds is deeply
///             immutable for the strategy's lifetime (for example an injected logger); a
///             readonly field that exposes mutable state is still shared state and is a bug.
///             Registration rejects the type with an <see cref="System.InvalidOperationException" />;
///             automatic discovery also logs the offending members before throwing.
///         </b>
///     </para>
/// </summary>
public abstract class BaseStrategy
{
}
