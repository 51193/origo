using Origo.Core.Abstractions.Console;

namespace Origo.Core.Runtime.Console;

/// <summary>
///     Manually registered handler for a single console command (non-reflection).
/// </summary>
public interface IConsoleCommandHandler
{
    /// <summary>
    ///     Command name (first word). Case-insensitive comparison is handled by the router.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     Help text describing the purpose and usage of this command.
    ///     Collected and displayed automatically by the help command.
    /// </summary>
    string HelpText { get; }

    /// <summary>
    ///     Minimum number of positional arguments (inclusive).
    /// </summary>
    int MinPositionalArgs { get; }

    /// <summary>
    ///     Maximum number of positional arguments (inclusive). -1 means unlimited.
    /// </summary>
    int MaxPositionalArgs { get; }

    /// <summary>
    ///     Attempt to execute the command. Returns true on success.
    ///     On failure, returns false and sets errorMessage with a user-facing description.
    /// </summary>
    bool TryExecute(CommandInvocation invocation, IConsoleOutputChannel outputChannel, out string? errorMessage);
}
