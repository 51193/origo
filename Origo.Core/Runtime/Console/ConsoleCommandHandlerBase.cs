using System;
using Origo.Core.Abstractions.Console;

namespace Origo.Core.Runtime.Console;

/// <summary>
///     控制台命令处理器基类。派生类只需声明 Name、HelpText、MinPositionalArgs、MaxPositionalArgs，
///     并实现 ExecuteCore 方法。基类自动处理参数数量校验与非法输入提示。
/// </summary>
public abstract class ConsoleCommandHandlerBase : IConsoleCommandHandler
{
    public abstract string Name { get; }
    public abstract string HelpText { get; }
    public abstract int MinPositionalArgs { get; }
    public abstract int MaxPositionalArgs { get; }

    public bool TryExecute(CommandInvocation invocation, IConsoleOutputChannel outputChannel, out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(outputChannel);

        var count = invocation.PositionalArgs.Count;

        if (count < MinPositionalArgs || (MaxPositionalArgs >= 0 && count > MaxPositionalArgs))
        {
            errorMessage = $"参数数量不合法。{HelpText}";
            return false;
        }

        return ExecuteCore(invocation, outputChannel, out errorMessage);
    }

    /// <summary>
    ///     子类实现具体命令逻辑；到达此处时参数数量已通过校验。
    /// </summary>
    protected abstract bool ExecuteCore(CommandInvocation invocation, IConsoleOutputChannel outputChannel,
        out string? errorMessage);
}
