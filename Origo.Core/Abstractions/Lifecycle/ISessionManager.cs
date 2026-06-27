using System.Collections.Generic;

namespace Origo.Core.Abstractions.Lifecycle;

/// <summary>
///     会话管理器接口，全权管理所有 <see cref="ISessionRun" /> 的生命周期。
/// </summary>
public interface ISessionManager
{
    /// <summary>前台会话在管理器中的保留键。</summary>
    const string ForegroundKey = "__foreground__";

    /// <summary>当前是否可以创建会话。Empty Session Manager 返回 false。</summary>
    bool CanCreateSessions { get; }

    /// <summary>当前前台会话；无活动前台会话时为 null。</summary>
    ISessionRun? ForegroundSession { get; }

    /// <summary>获取所有已挂载会话的键列表。</summary>
    IReadOnlyCollection<string> Keys { get; }

    /// <summary>按键获取会话。</summary>
    ISessionRun? TryGet(string key);

    /// <summary>检查指定键的会话是否存在。</summary>
    bool Contains(string key);

    /// <summary>
    ///     创建后台关卡会话并自动挂载到管理器。
    ///     使用纯内存场景宿主实现，不依赖引擎适配层。
    /// </summary>
    ISessionRun CreateBackgroundSession(string key, string levelId, bool syncProcess = false);

    /// <summary>销毁指定键的会话。若键不存在则静默返回。</summary>
    void DestroySession(string key);

    /// <summary>
    ///     对所有配置为参与 Process 的会话执行帧更新。
    /// </summary>
    void ProcessAllSessions(double delta, bool includeForeground = false);

    /// <summary>
    ///     收割所有会话（含前台）中标记为待销毁的实体：触发观察者拆线与 BeforeDead 钩子后物理移除。
    ///     由帧末统一调用，使前台与后台会话的 kill-pending 语义完全一致——前台不再被特殊对待。
    /// </summary>
    void KillPendingAllSessions();
}
