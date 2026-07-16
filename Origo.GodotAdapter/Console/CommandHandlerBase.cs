using System;
using Origo.Core.Abstractions.Console;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Console;

namespace Origo.GodotAdapter.Console;

/// <summary>
///     Base class for Godot-specific console command handlers.
///     Holds a reference to <see cref="OrigoRuntime" /> and provides
///     entity lookup helpers for Godot adapter-layer commands.
/// </summary>
public abstract class CommandHandlerBase : IConsoleCommandHandler
{
    protected CommandHandlerBase(OrigoRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        Runtime = runtime;
    }

    protected OrigoRuntime Runtime { get; }

    public abstract string Name { get; }
    public abstract string HelpText { get; }
    public abstract int MinPositionalArgs { get; }
    public abstract int MaxPositionalArgs { get; }

    public bool TryExecute(CommandInvocation invocation, IConsoleOutputChannel outputChannel,
        out string? errorMessage)
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

    protected abstract bool ExecuteCore(CommandInvocation invocation, IConsoleOutputChannel outputChannel,
        out string? errorMessage);
}
