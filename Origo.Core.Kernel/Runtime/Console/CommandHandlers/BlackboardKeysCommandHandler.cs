using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Console;

namespace Origo.Core.Runtime.Console.CommandHandlers;

/// <summary>
///     <c>bb_keys</c> command: lists all keys in the given blackboard layer.
///     Usage: <c>bb_keys &lt;layer&gt;</c>
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
    public override string HelpText => "bb_keys <layer> — list all keys in the given blackboard layer. layer: system";
    public override int MinPositionalArgs => 1;
    public override int MaxPositionalArgs => 1;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var layer = invocation.PositionalArgs[0].Trim().ToLowerInvariant();

        IBlackboard bb;
        try
        {
            bb = ConsoleCommandHelper.ResolveBlackboardLayer(_runtime, layer);
        }
        catch (ArgumentException ex)
        {
            errorMessage = ex.Message;
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
