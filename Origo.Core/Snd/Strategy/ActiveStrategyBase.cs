using Origo.Core.Abstractions.Entity;

namespace Origo.Core.Snd.Strategy;

/// <summary>
///     Active strategy base class. Inherits from <see cref="BaseStrategy" /> alongside
///     <see cref="LifecycleStrategyBase" />, but only supports externally-initiated calls
///     (<see cref="Invoke" />) and does not participate in frame updates or lifecycle hooks.
///     <para>
///         Strategy instances are stateless, shared, and pooled, managed centrally by
///         <see cref="SndStrategyPool" />. Invoked on entities via
///         <see cref="ISndActiveStrategyAccess.InvokeStrategy" />.
///     </para>
/// </summary>
public abstract class ActiveStrategyBase : BaseStrategy
{
    /// <summary>
    ///     Invokes the strategy actively and returns the result.
    ///     <c>entity</c> is the entity instance on which this strategy is invoked;
    ///     <c>ctx</c> is the current game context; <c>input</c> is an optional input parameter.
    /// </summary>
    public abstract object? Invoke(ISndEntity entity, ISndContext ctx, object? input);
}
