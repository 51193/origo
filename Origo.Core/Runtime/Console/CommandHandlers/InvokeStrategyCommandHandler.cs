using System;
using Origo.Core.Abstractions.Console;

namespace Origo.Core.Runtime.Console.CommandHandlers;

/// <summary>
///     <c>invoke_strategy</c> command: invokes an entity's active strategy by name and outputs the return value.
///     Usage: <c>invoke_strategy &lt;entity&gt; &lt;strategy_index&gt; [input]</c>
/// </summary>
internal sealed class InvokeStrategyCommandHandler : ConsoleCommandHandlerBase
{
    private readonly OrigoRuntime _runtime;

    public InvokeStrategyCommandHandler(OrigoRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
    }

    public override string Name => "invoke_strategy";

    public override string HelpText =>
        "invoke_strategy <entity> <strategy_index> [input] — invoke an entity's active strategy. input is optional.";

    public override int MinPositionalArgs => 2;
    public override int MaxPositionalArgs => 3;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var entityName = invocation.PositionalArgs[0].Trim();
        var strategyIndex = invocation.PositionalArgs[1].Trim();
        var input = invocation.PositionalArgs.Count >= 3
            ? invocation.PositionalArgs[2].Trim()
            : null;

        if (!ConsoleCommandHelper.TryFindEntity(_runtime, entityName, out var entity, out errorMessage))
            return false;

        object? result;
        try
        {
            result = entity!.InvokeStrategy(strategyIndex, input);
        }
        catch (InvalidOperationException ex)
        {
            errorMessage = $"InvokeStrategy '{strategyIndex}' on '{entityName}' failed: {ex.Message}";
            return false;
        }

        outputChannel.Publish($"InvokeStrategy('{strategyIndex}') on '{entityName}' => {result ?? "null"}");
        errorMessage = null;
        return true;
    }
}
