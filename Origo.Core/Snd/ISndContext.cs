using Origo.Core.Abstractions.Snd;
using Origo.Core.Abstractions.StateMachine;

namespace Origo.Core.Snd;

/// <summary>
///     面向策略与游戏层的统一业务门面接口。不继承任何角色接口，
///     所有能力通过类型化 companion 属性访问。
///     <para>
///         companion 属性按职责划分：
///         <see cref="Blackboard" />（黑板访问）、
///         <see cref="Deferred" />（延迟动作队列）、
///         <see cref="Template" />（模板克隆）、
///         <see cref="ConsoleAccess" />（控制台）、
///         <see cref="StateMachines" />（状态机）、
///         <see cref="Save" />（存档/关卡操作）、
///         <see cref="Lifecycle" />（生命周期入口）、
///         <see cref="FileAccess" />（静态资源文件访问）、
///         <see cref="ArchiveFileAccess" />（存档内文件访问）、
///         <see cref="StateMachineContext" />（状态机上下文）。
///         策略钩子保持全量 <c>ISndContext ctx</c> 参数，
///         通过 <c>ctx.Blackboard.SystemBlackboard</c> 等二级访问使用各项能力。
///     </para>
/// </summary>
public interface ISndContext
{
    /// <summary>启动流程：策略发现 → 别名/模板加载 → 入口存档加载。</summary>
    void Bootstrap();

    /// <summary>当前存档根路径。</summary>
    string SaveRootPath { get; }

    /// <summary>初始存档根路径。</summary>
    string InitialSaveRootPath { get; }

    /// <summary>入口配置路径。</summary>
    string EntryConfigPath { get; }

    /// <summary>系统级和流程级黑板访问。</summary>
    ISndBlackboardAccess Blackboard { get; }

    /// <summary>延迟动作队列。</summary>
    ISndDeferredActions Deferred { get; }

    /// <summary>模板克隆。</summary>
    ISndTemplateAccess Template { get; }

    /// <summary>控制台命令提交、处理与输出订阅。</summary>
    ISndConsoleAccess ConsoleAccess { get; }

    /// <summary>流程级状态机容器访问。</summary>
    ISndStateMachineAccess StateMachines { get; }

    /// <summary>存档读写与关卡切换。</summary>
    ISndSaveOperations Save { get; }

    /// <summary>存档生命周期入口：继续游戏、初始存档、主菜单入口。</summary>
    ISndLifecycleOperations Lifecycle { get; }

    /// <summary>静态资源文件访问（策略范围，路径相对于项目配置目录）。</summary>
    ISndFileAccess FileAccess { get; }

    /// <summary>存档内文件访问（路径相对于存档的 extra/ 子目录）。</summary>
    ISndArchiveFileAccess ArchiveFileAccess { get; }

    /// <summary>状态机上下文（黑板访问 + 延迟动作 + 会话/场景访问）。</summary>
    IStateMachineContext StateMachineContext { get; }
}
