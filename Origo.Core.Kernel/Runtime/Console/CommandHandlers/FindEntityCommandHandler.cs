using System;
using Origo.Core.Abstractions.Console;

namespace Origo.Core.Runtime.Console.CommandHandlers;

/// <summary>
///     <c>find_entity</c> command: finds an SND entity by name and displays its node info.
///     Usage: <c>find_entity &lt;name&gt;</c>
/// </summary>
internal sealed class FindEntityCommandHandler : ConsoleCommandHandlerBase
{
    private readonly OrigoRuntime _runtime;

    public FindEntityCommandHandler(OrigoRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
    }

    public override string Name => "find_entity";
    public override string HelpText => "find_entity <name> — find an SND entity by name and show its node info.";
    public override int MinPositionalArgs => 1;
    public override int MaxPositionalArgs => 1;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var name = invocation.PositionalArgs[0].Trim();
        if (!ConsoleCommandHelper.TryFindEntity(_runtime, name, out var entity, out _))
        {
            outputChannel.Publish($"Entity '{name}' not found.");
        }
        else
        {
            var nodeNames = entity!.GetNodeNames();
            outputChannel.Publish($"Entity '{name}' found. Nodes: [{string.Join(", ", nodeNames)}]");
        }

        errorMessage = null;
        return true;
    }
}
