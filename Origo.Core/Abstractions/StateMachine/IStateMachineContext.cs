using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Scene;

namespace Origo.Core.Abstractions.StateMachine;

/// <summary>
///     状态机策略钩子所需的最小运行时上下文接口。
///     不包含任何"前台/后台"语义，所有成员在前后台会话中具有对等含义。
///     <para>
///         <strong>实现说明：</strong>
///         <list type="bullet">
///             <item>
///                 <description>
///                     <see cref="Snd.SndContext" /> 为全局/流程级默认实现，
///                     <see cref="SessionBlackboard" /> 和 <see cref="SceneAccess" /> 指向前台会话。
///                     该实现仅作为流程级状态机的上下文入口；会话级状态机不直接使用此实现。
///                 </description>
///             </item>
///             <item>
///                 <description>
///                     <see cref="SessionStateMachineContext" /> 为会话级适配器，
///                     将 <see cref="SessionBlackboard" /> 和 <see cref="SceneAccess" /> 绑定到当前会话，
///                     确保前台和后台会话的状态机钩子均指向各自的会话数据——前后台无语义分差。
///                 </description>
///             </item>
///         </list>
///     </para>
///     后台或测试场景可自行提供替代实现，从而与具体前台上下文彻底解耦。
/// </summary>
public interface IStateMachineContext
{
    /// <summary>系统级黑板。</summary>
    IBlackboard SystemBlackboard { get; }

    /// <summary>当前流程级黑板；无活动流程时为 null。</summary>
    IBlackboard? ProgressBlackboard { get; }

    /// <summary>当前会话黑板；无活动会话时为 null。</summary>
    IBlackboard? SessionBlackboard { get; }

    /// <summary>当前会话的 SND 场景访问；前后台会话各自返回自身的场景宿主。</summary>
    ISndSceneAccess SceneAccess { get; }

    /// <summary>将业务逻辑延迟动作加入队列。</summary>
    void EnqueueBusinessDeferred(Action action);
}
