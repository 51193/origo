using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd;

/// <summary>
///     面向策略与游戏层的统一业务门面接口。
///     <para>
///         整个游戏由实体的策略模式驱动，包括 UI 按钮、场景控件等；因此此接口覆盖完整业务链路：
///         三层黑板、存档/读档、控制台、关卡切换、会话管理、状态机等。
///         不暴露框架内部实现细节（如 <see cref="SndRuntime" />、文件路径等），
///         仅暴露策略钩子与游戏逻辑可合理调用的能力。
///     </para>
///     <para>
///         <b>设计原则：</b>
///         <list type="bullet">
///             <item>
///                 <description>
///                     实体策略基类 <see cref="Strategy.EntityStrategyBase" /> 的所有钩子均以
///                     <c>ISndContext ctx</c> 作为上下文参数，策略可通过此接口读写黑板、请求存档、
///                     切换关卡、操作控制台等，而无需依赖具体实现。
///                 </description>
///             </item>
///             <item>
///                 <description>
///                     Session 内部能力（实体管理、状态机、黑板读写等）不经由 Context 泄露，
///                     策略应通过 <see cref="SessionManager" /> 获取 <see cref="ISessionRun" /> 引用，
///                     再调用 Session 自身的成员方法。
///                 </description>
///             </item>
///         </list>
///     </para>
/// </summary>
public interface ISndContext
{
    // ── Blackboards ────────────────────────────────────────────────────

    /// <summary>系统级黑板，生命周期与进程一致。持久化到 <c>saveRoot/system.json</c>。</summary>
    IBlackboard SystemBlackboard { get; }

    /// <summary>当前流程级黑板（存档槽级）；无活动流程时为 null。</summary>
    IBlackboard? ProgressBlackboard { get; }

    // ── Session management ─────────────────────────────────────────────

    /// <summary>
    ///     会话管理器，以 KVP 形式统一管理所有 <see cref="ISessionRun" />。
    ///     前台会话以 <see cref="ISessionManager.ForegroundKey" /> 为键挂载，与后台会话无架构区别。
    ///     策略可通过此管理器获取任何已挂载的会话引用。
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

    // ── Deferred actions ───────────────────────────────────────────────

    /// <summary>将业务逻辑延迟动作加入队列，在当前帧末尾统一执行。策略钩子中推荐使用此方法排队副作用。</summary>
    void EnqueueBusinessDeferred(Action action);

    /// <summary>执行当前帧的所有延迟动作（业务队列先于系统队列）。由引擎适配层每帧调用一次，策略不应直接调用。</summary>
    void FlushDeferredActionsForCurrentFrame();

    /// <summary>获取当前待执行的持久化请求计数，可用于等待异步存档完成。</summary>
    int GetPendingPersistenceRequestCount();

    // ── Template cloning ──────────────────────────────────────────────

    /// <summary>克隆指定模板并可选地覆盖名称，便于按模板批量创建实体。</summary>
    SndMetaData CloneTemplate(string templateKey, string? overrideName = null);

    // ── Console ────────────────────────────────────────────────────────

    /// <summary>提交一条控制台命令。若未注入输入队列则返回 false。</summary>
    bool TrySubmitConsoleCommand(string commandLine);

    /// <summary>处理控制台待执行命令。</summary>
    void ProcessConsolePending();

    /// <summary>订阅控制台输出，返回订阅 ID。</summary>
    long SubscribeConsoleOutput(Action<string> onLine);

    /// <summary>取消控制台输出订阅。</summary>
    void UnsubscribeConsoleOutput(long subscriptionId);

    // ── State machines ─────────────────────────────────────────────────

    /// <summary>流程级字符串栈状态机容器；无活动流程时为 null。</summary>
    StateMachineContainer? GetProgressStateMachines();

    // ── Save / load / level ────────────────────────────────────────────

    /// <summary>列出可用存档槽位。</summary>
    IReadOnlyList<string> ListSaves();

    /// <summary>请求加载指定存档。</summary>
    void RequestLoadGame(string saveId);

    /// <summary>请求保存到指定槽位。</summary>
    void RequestSaveGame(string newSaveId);

    /// <summary>自动保存，返回实际使用的 saveId。</summary>
    string RequestSaveGameAuto(string? newSaveId = null);

    /// <summary>设置 continue 目标存档。</summary>
    void SetContinueTarget(string saveId);

    /// <summary>请求切换前台关卡。</summary>
    void RequestSwitchForegroundLevel(string newLevelId);

    // ── Session lifecycle ──────────────────────────────────────────────

    /// <summary>是否存在可继续游戏的目标存档。</summary>
    bool HasContinueData();

    /// <summary>请求继续游戏（基于当前 continue 目标）。</summary>
    bool RequestContinueGame();

    /// <summary>请求加载初始存档模板。</summary>
    void RequestLoadInitialSave();

    /// <summary>按启动流程重新读取主菜单入口配置。</summary>
    void RequestLoadMainMenuEntrySave();
}
