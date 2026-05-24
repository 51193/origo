namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     ProgressRun 启动所需的配置参数。
///     <para>
///         仅传递标识符（SaveId），不注入任何已构建的运行时对象（如 IBlackboard）。
///         ProgressRun 在内部自行创建 ProgressBlackboard 并通过 <see cref="ProgressRun.LoadFromPayload" />
///         从持久化数据中恢复全部状态（包括会话拓扑）。
///     </para>
/// </summary>
internal readonly record struct ProgressParameters(string SaveId);
