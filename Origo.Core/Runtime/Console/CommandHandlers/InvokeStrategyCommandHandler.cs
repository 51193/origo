using System;
using Origo.Core.Abstractions.Console;

namespace Origo.Core.Runtime.Console.CommandHandlers;

/// <summary>
///     <c>invoke_strategy</c> 命令：按实体名调用主动策略并输出返回值。
///     用法：<c>invoke_strategy &lt;entity&gt; &lt;strategy_index&gt; [input]</c>
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
        "invoke_strategy <entity> <strategy_index> [input] — 调用实体的主动策略。input 可选。";

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

        var entity = _runtime.Snd.FindByName(entityName);
        if (entity is null)
        {
            errorMessage = $"Entity '{entityName}' not found.";
            return false;
        }

        object? result;
        try
        {
            result = entity.InvokeStrategy(strategyIndex, input);
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