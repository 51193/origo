using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Scene;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     SessionRun 启动所需的配置参数。
///     <para>
///         <see cref="IsFrontSession" /> 由 <see cref="SessionManager" /> 在创建时赋值，
///         标识该 Session 是否为前台会话。该标志在 SessionRun 构造后固化到运行时中，
///         策略钩子通过 <see cref="ISndContext.IsFrontSession" /> 获取。
///     </para>
/// </summary>
internal readonly record struct SessionParameters(
    string LevelId,
    IBlackboard SessionBlackboard,
    ISndSceneHost SceneHost,
    bool IsFrontSession = false);
