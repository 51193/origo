using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Snd;

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
///         遵循接口隔离原则（ISP），本接口按职责拆分为 9 个角色接口：
///         <see cref="ISndBlackboardAccess" />（黑板访问）、
///         <see cref="ISndDeferredActions" />（延迟动作队列）、
///         <see cref="ISndTemplateAccess" />（模板克隆）、
///         <see cref="ISndConsoleAccess" />（控制台）、
///         <see cref="ISndStateMachineAccess" />（状态机）、
///         <see cref="ISndSaveOperations" />（存档/关卡操作）、
///         <see cref="ISndLifecycleOperations" />（生命周期入口）、
///         <see cref="ISndFileAccess" />（静态资源文件访问）、
///         <see cref="ISndArchiveFileAccess" />（存档内文件访问）。
///         消费者可按需依赖窄接口，策略钩子保持全量 <c>ISndContext ctx</c> 参数。
///     </para>
/// </summary>
public interface ISndContext : ISndBlackboardAccess, ISndDeferredActions,
    ISndTemplateAccess, ISndConsoleAccess, ISndStateMachineAccess, ISndSaveOperations,
    ISndLifecycleOperations, ISndFileAccess, ISndArchiveFileAccess
{
    /// <summary>
    ///     会话管理器，以 KVP 形式统一管理所有 <see cref="ISessionRun" />。
    ///     前台会话以 <see cref="ISessionManager.ForegroundKey" /> 为键挂载。
    /// </summary>
    ISessionManager SessionManager { get; }
}
