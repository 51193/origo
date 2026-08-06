using System.Collections.Generic;

namespace Origo.Core.Runtime.Console;

/// <summary>
///     A parsed console invocation: command name + positional arguments + named arguments.
/// </summary>
public sealed class CommandInvocation
{
    /// <summary>The command name.</summary>
    public required string Command { get; init; }

    /// <summary>Positional (space-separated) arguments in order.</summary>
    public required IReadOnlyList<string> PositionalArgs { get; init; }

    /// <summary>Named arguments (key=value pairs).</summary>
    public required IReadOnlyDictionary<string, string> NamedArgs { get; init; }
}
