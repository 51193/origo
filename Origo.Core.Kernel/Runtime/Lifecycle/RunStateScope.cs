using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Runtime.StateMachine;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     Binds a blackboard and a string-stack state machine container within the same lifetime.
/// </summary>
internal sealed class RunStateScope
{
    public RunStateScope(IBlackboard blackboard, StateMachineContainer stateMachines)
    {
        ArgumentNullException.ThrowIfNull(blackboard);
        ArgumentNullException.ThrowIfNull(stateMachines);
        Blackboard = blackboard;
        StateMachines = stateMachines;
    }

    public IBlackboard Blackboard { get; }

    public StateMachineContainer StateMachines { get; }
}
