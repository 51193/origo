using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.StateMachine;

namespace Origo.Core.Abstractions.Lifecycle;

/// <summary>
///     关卡会话级运行时的只读门面接口。
///     外部代码（策略层）仅通过此接口访问会话内部状态；
///     生命周期（创建 / 销毁）与序列化 / 反序列化均由 <see cref="ISessionManager" /> 统一管理。
///     前台和后台关卡均为同一接口，区别仅在于注入的 <see cref="ISndSceneHost" /> 实现
///     以及 <see cref="IsFrontSession" /> 标志。
///     <see cref="IDisposable.Dispose" /> 仅供框架内部通过 <see cref="ISessionManager" /> 调用，
///     策略代码应通过 <see cref="ISessionManager.DestroySession" /> 销毁会话。
/// </summary>
public interface ISessionRun : IDisposable
{
    IBlackboard SessionBlackboard { get; }

    ISndSceneHost SceneHost { get; }

    string LevelId { get; }

    bool IsFrontSession { get; }

    /// <summary>
    ///     会话级状态机容器。策略层可通过此接口创建/获取会话级状态机。
    /// </summary>
    IStateMachineContainer GetSessionStateMachines();
}
