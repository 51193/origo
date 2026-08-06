using System;
using System.Collections.Generic;

namespace Origo.Core.Runtime.Console;

/// <summary>
///     Parses a single line of text into a <see cref="CommandInvocation" />.
///     A command name with no subsequent tokens is also valid (argument constraints are validated
///     independently by each <see cref="IConsoleCommandHandler" />).
///     Supports positional arguments: <c>spawn myName myTemplate</c>;
///     or named arguments: <c>spawn name=myName template=myTemplate</c> (cannot be mixed with positional arguments).
/// </summary>
public static class ConsoleCommandParser
{
    private static readonly char[] _tokenSeparators = [' ', '\t'];

    /// <summary>
    ///     Parses a console line into a <see cref="CommandInvocation" />.
    ///     Returns false with an error message for empty/blank input.
    /// </summary>
    public static bool TryParse(string line, out CommandInvocation? invocation, out string? error)
    {
        invocation = null;
        error = null;

        if (string.IsNullOrWhiteSpace(line))
        {
            error = "Empty command.";
            return false;
        }

        var tokens = Tokenize(line);
        if (tokens.Count == 0)
        {
            error = "Empty command.";
            return false;
        }

        var command = tokens[0];
        var rest = tokens.Count > 1 ? tokens.GetRange(1, tokens.Count - 1) : [];

        if (HasNamedArgument(rest))
            return TryParseNamed(command, rest, out invocation, out error);

        invocation = new CommandInvocation
        {
            Command = command,
            PositionalArgs = rest,
            NamedArgs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
        return true;
    }

    private static bool HasNamedArgument(List<string> tokens)
    {
        foreach (var t in tokens)
            if (t.Contains('=', StringComparison.Ordinal))
                return true;
        return false;
    }

    private static bool TryParseNamed(string command, List<string> rest,
        out CommandInvocation? invocation, out string? error)
    {
        var named = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in rest)
        {
            var eq = t.IndexOf('=');
            if (eq <= 0 || eq == t.Length - 1)
            {
                invocation = null;
                error = $"Invalid named argument '{t}'. Expected key=value.";
                return false;
            }

            var key = t[..eq].Trim();
            var value = t[(eq + 1)..].Trim();
            if (key.Length == 0)
            {
                invocation = null;
                error = $"Invalid named argument '{t}'. Key cannot be empty.";
                return false;
            }

            named[key] = value;
        }

        invocation = new CommandInvocation
        {
            Command = command,
            PositionalArgs = [],
            NamedArgs = named
        };
        error = null;
        return true;
    }

    private static List<string> Tokenize(string line)
    {
        var parts = line.Trim().Split(_tokenSeparators, StringSplitOptions.RemoveEmptyEntries);
        return [.. parts];
    }
}
