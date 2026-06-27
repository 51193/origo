using System;
using Origo.Core.Abstractions.Lifecycle;

namespace Origo.Core.Snd.Scene;

/// <summary>
///     归属于某个 <see cref="ISessionRun" /> 的场景宿主。
///     宿主创建的实体属于该会话；当框架在宿主上直接触发实体生命周期钩子
///     （如 <see cref="SndRuntime.SpawnCore" /> 的 AfterSpawn）而未经
///     <see cref="SessionManager" /> 编排路径时，宿主据此把归属会话压入 ambient 栈，
///     确保策略钩子经 <see cref="ISndContext.CurrentSession" /> 解析到正确的会话，
///     而非回退到前台会话。
/// </summary>
internal interface ISessionScopedSceneHost
{
    /// <summary>由 <see cref="SessionRun" /> 在构造时绑定其归属会话。</summary>
    void SetOwningSession(ISessionRun session);

    /// <summary>
    ///     进入归属会话的 ambient 作用域；释放返回值即退出。
    ///     若尚未绑定归属会话或上下文不支持 ambient，返回 null（调用方按无作用域处理）。
    /// </summary>
    IDisposable? EnterOwningSessionAmbient();
}
