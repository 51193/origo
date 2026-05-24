using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Runtime.StateMachine;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     关卡会话级运行时的只读门面接口。
///     外部代码（策略层）仅通过此接口访问会话内部状态；
///     生命周期（创建 / 销毁）与序列化 / 反序列化均由 <see cref="ISessionManager" /> 统一管理，
///     不经由此接口暴露。
///     前台和后台关卡均为同一接口，区别仅在于注入的 <see cref="ISndSceneHost" /> 实现
///     以及 <see cref="IsFrontSession" /> 标志。
///     <para>
///         设计原则：会话对自己内部拥有完全支配权（黑板读写、场景操作、状态机），
///         但生命周期由 <see cref="ISessionManager" /> 全权管理。
///     </para>
/// </summary>
public interface ISessionRun : IDisposable
{
    IBlackboard SessionBlackboard { get; }

    /// <summary>
    ///     当前会话的 SND 场景宿主。前台和后台均返回 <see cref="ISndSceneHost" />，
    ///     支持 Spawn / FindByName / GetEntities 等完整实体操作，无需强转具体类型。
    /// </summary>
    ISndSceneHost SceneHost { get; }

    string LevelId { get; }

    /// <summary>
    ///     指示当前会话是否为前台会话。
    ///     该值在构造时由 <see cref="SessionManager" /> 根据挂载 Key 决定，构造后不可变。
    /// </summary>
    bool IsFrontSession { get; }

    /// <summary>
    ///     会话级字符串栈状态机容器。策略层可通过此方法创建/获取会话级状态机。
    /// </summary>
    StateMachineContainer GetSessionStateMachines();
}
