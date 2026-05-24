using Origo.Core.Abstractions.Console;

namespace Origo.Core.Runtime.Console;

/// <summary>
///     单条控制台命令的手动注册处理器（非反射）。
/// </summary>
public interface IConsoleCommandHandler
{
    /// <summary>
    ///     命令名（首词），不区分大小写比较由路由器负责。
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     帮助信息，描述该命令的用途与用法，由 help 命令自动收集展示。
    /// </summary>
    string HelpText { get; }

    /// <summary>
    ///     允许的位置参数最小数量（含）。
    /// </summary>
    int MinPositionalArgs { get; }

    /// <summary>
    ///     允许的位置参数最大数量（含）。-1 表示无上限。
    /// </summary>
    int MaxPositionalArgs { get; }

    bool TryExecute(CommandInvocation invocation, IConsoleOutputChannel outputChannel, out string? errorMessage);
}
