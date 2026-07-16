using System;
using Origo.Core.Abstractions.Console;

namespace Origo.Core.Runtime.Console;

/// <summary>
///     Base class for console command handlers. Derived classes only need to declare
///     Name, HelpText, MinPositionalArgs, and MaxPositionalArgs, and implement the ExecuteCore method.
///     The base class automatically handles argument count validation and invalid input messaging.
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
            errorMessage = $"{ConsoleMessages.InvalidArgumentCount} {HelpText}";
            return false;
        }

        return ExecuteCore(invocation, outputChannel, out errorMessage);
    }

    /// <summary>
    ///     Implemented by subclasses for command-specific logic; argument counts have already been validated
    ///     by this point.
    /// </summary>
    protected abstract bool ExecuteCore(CommandInvocation invocation, IConsoleOutputChannel outputChannel,
        out string? errorMessage);
}
