using System.Collections.Generic;

namespace Origo.Core.Runtime.Console;

/// <summary>
///     A parsed console invocation: command name + positional arguments + named arguments.
/// </summary>
public sealed class CommandInvocation
{
    public required string Command { get; init; }

    public required IReadOnlyList<string> PositionalArgs { get; init; }

    public required IReadOnlyDictionary<string, string> NamedArgs { get; init; }
}
