using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Snd.Strategy;

namespace Origo.Core.StateMachine;

/// <summary>
///     Base class for strategies bound to a string-stack state machine. Hook names encode "operation + timing"
///     and correspond one-to-one with <c>StackStateMachine</c> dispatch points. Hook parameters
///     use the <see cref="IStateMachineContext" /> interface, allowing frontend and backend to share the same
///     abstraction; branching on implementation type is forbidden.
/// </summary>
public abstract class StateMachineStrategyBase : BaseStrategy
{
    /// <summary>Invoked after a runtime <c>StackStateMachine.Push</c> succeeds.</summary>
    public virtual void OnPushRuntime(StateMachineStrategyContext context, IStateMachineContext ctx)
    {
    }

    /// <summary>Invoked for each layer in bottom-to-top order after loading the stack (see <c>StackStateMachine.FlushAfterLoad</c>).</summary>
    public virtual void OnPushAfterLoad(StateMachineStrategyContext context, IStateMachineContext ctx)
    {
    }

    /// <summary>Invoked before a runtime <c>StackStateMachine.TryPopRuntime</c> pops the stack.</summary>
    public virtual void OnPopRuntime(StateMachineStrategyContext context, IStateMachineContext ctx)
    {
    }

    /// <summary>Invoked before a quit-time <c>StackStateMachine.TryPopOnQuit</c> pops the stack.</summary>
    public virtual void OnPopBeforeQuit(StateMachineStrategyContext context, IStateMachineContext ctx)
    {
    }
}
