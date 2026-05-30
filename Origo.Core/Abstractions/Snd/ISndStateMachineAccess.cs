using Origo.Core.Runtime.StateMachine;

namespace Origo.Core.Abstractions.Snd;

/// <summary>
///     提供流程级字符串栈状态机的访问。
/// </summary>
public interface ISndStateMachineAccess
{
    /// <summary>流程级字符串栈状态机容器；无活动流程时为 null。</summary>
    StateMachineContainer? GetProgressStateMachines();
}