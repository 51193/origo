using System.Collections.Generic;

namespace Origo.Core.Runtime.Console;

/// <summary>
///     解析后的一条控制台调用：命令名 + 位置参数 + 命名参数。
/// </summary>
public sealed class CommandInvocation
{
    public required string Command { get; init; }

    public required IReadOnlyList<string> PositionalArgs { get; init; }

    public required IReadOnlyDictionary<string, string> NamedArgs { get; init; }
}
