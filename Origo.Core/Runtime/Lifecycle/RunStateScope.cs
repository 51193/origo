using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Runtime.StateMachine;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     将黑板与字符串栈状态机容器绑定在同一生命周期内。
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
