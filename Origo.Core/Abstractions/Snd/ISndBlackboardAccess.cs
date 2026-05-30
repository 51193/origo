using Origo.Core.Abstractions.Blackboard;

namespace Origo.Core.Abstractions.Snd;

/// <summary>
///     提供系统级和流程级黑板的只读访问。
///     由 <see cref="Origo.Core.Snd.ISndContext" /> 和
///     <see cref="Origo.Core.Abstractions.StateMachine.IStateMachineContext" /> 共同消费。
/// </summary>
public interface ISndBlackboardAccess
{
    /// <summary>系统级黑板，生命周期与进程一致。</summary>
    IBlackboard SystemBlackboard { get; }

    /// <summary>当前流程级黑板（存档槽级）；无活动流程时为 null。</summary>
    IBlackboard? ProgressBlackboard { get; }
}