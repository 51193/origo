using System;
using Origo.Core.Abstractions.Console;

namespace Origo.Core.Runtime.Console.CommandHandlers;

/// <summary>
///     <c>bb_keys</c> 命令：列出指定黑板层的全部键。
///     用法：<c>bb_keys &lt;layer&gt;</c>
///     layer: system
/// </summary>
internal sealed class BlackboardKeysCommandHandler : ConsoleCommandHandlerBase
{
    private readonly OrigoRuntime _runtime;

    public BlackboardKeysCommandHandler(OrigoRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
    }

    public override string Name => "bb_keys";
    public override string HelpText => "bb_keys <layer> — 列出指定黑板层的全部键。layer: system";
    public override int MinPositionalArgs => 1;
    public override int MaxPositionalArgs => 1;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var layer = invocation.PositionalArgs[0].Trim().ToLowerInvariant();

        var bb = layer switch
        {
            "system" => _runtime.SystemBlackboard,
            _ => null
        };

        if (bb is null)
        {
            errorMessage = $"Unknown or unavailable blackboard layer '{layer}'. Use: system";
            return false;
        }

        var keys = bb.GetKeys();
        if (keys.Count == 0)
            outputChannel.Publish($"[{layer}] (empty)");
        else
            outputChannel.Publish($"[{layer}] Keys ({keys.Count}): {string.Join(", ", keys)}");

        errorMessage = null;
        return true;
    }
}
