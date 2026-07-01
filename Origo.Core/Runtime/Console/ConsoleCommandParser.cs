using System;
using System.Collections.Generic;

namespace Origo.Core.Runtime.Console;

/// <summary>
///     将一行文本解析为 <see cref="CommandInvocation" />。
///     仅含命令名、无后续 token 时也合法（参数约束由各 <see cref="IConsoleCommandHandler" /> 自行校验）。
///     支持位置参数：<c>spawn myName myTemplate</c>；
///     或命名参数：<c>spawn name=myName template=myTemplate</c>（不可与位置参数混用）。
/// </summary>
public static class ConsoleCommandParser
{
    private static readonly char[] TokenSeparators = [' ', '\t'];

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
        var parts = line.Trim().Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries);
        return [.. parts];
    }
}
