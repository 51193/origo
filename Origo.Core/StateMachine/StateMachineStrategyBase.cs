using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Snd.Strategy;

namespace Origo.Core.StateMachine;

/// <summary>
///     Base class for strategies bound to a string-stack state machine. Hook names encode "operation + timing"
///     and correspond one-to-one with <see cref="StackStateMachine" /> dispatch points. Hook parameters
///     use the <see cref="IStateMachineContext" /> interface, allowing frontend and backend to share the same
///     abstraction; branching on implementation type is forbidden.
/// </summary>
public abstract class StateMachineStrategyBase : BaseStrategy
{
    /// <summary>Invoked after a runtime <see cref="StackStateMachine.Push" /> succeeds.</summary>
    public virtual void OnPushRuntime(StateMachineStrategyContext context, IStateMachineContext ctx)
    {
    }

    /// <summary>Invoked for each layer in bottom-to-top order after loading the stack (see <see cref="StackStateMachine.FlushAfterLoad" />).</summary>
    public virtual void OnPushAfterLoad(StateMachineStrategyContext context, IStateMachineContext ctx)
    {
    }

    /// <summary>Invoked before a runtime <see cref="StackStateMachine.TryPopRuntime" /> pops the stack.</summary>
    public virtual void OnPopRuntime(StateMachineStrategyContext context, IStateMachineContext ctx)
    {
    }

    /// <summary>Invoked before a quit-time <see cref="StackStateMachine.TryPopOnQuit" /> pops the stack.</summary>
    public virtual void OnPopBeforeQuit(StateMachineStrategyContext context, IStateMachineContext ctx)
    {
    }
}
