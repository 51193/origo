using Origo.Core.Runtime.Lifecycle;

namespace Origo.Core.Abstractions.Snd;

/// <summary>
///     提供会话管理器与当前会话的访问。
///     策略可通过此接口获取任何已挂载的会话引用。
/// </summary>
public interface ISndSessionAccess
{
    /// <summary>
    ///     会话管理器，以 KVP 形式统一管理所有 <see cref="ISessionRun" />。
    ///     前台会话以 <see cref="ISessionManager.ForegroundKey" /> 为键挂载。
    /// </summary>
    ISessionManager SessionManager { get; }

    /// <summary>
    ///     当前上下文绑定的会话。
    ///     对于全局上下文通常返回前台会话；对于会话上下文返回该会话自身。
    /// </summary>
    ISessionRun? CurrentSession { get; }

    /// <summary>
    ///     当前上下文绑定的会话是否为前台会话。
    ///     便捷属性，等价于 <c>CurrentSession?.IsFrontSession ?? false</c>。
    /// </summary>
    bool IsFrontSession { get; }
}
