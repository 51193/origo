using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Console;

namespace Origo.Core.Runtime.Console.CommandHandlers;

/// <summary>
///     <c>bb_set</c> 命令：向黑板写入一个字符串值。
///     用法：<c>bb_set &lt;layer&gt; &lt;key&gt; &lt;value&gt;</c>
///     layer: system
///     值将自动推断类型：整数 → Int32、浮点 → Single、"true"/"false" → Boolean、其余 → String。
/// </summary>
internal sealed class BlackboardSetCommandHandler : ConsoleCommandHandlerBase
{
    private readonly OrigoRuntime _runtime;

    public BlackboardSetCommandHandler(OrigoRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
    }

    public override string Name => "bb_set";
    public override string HelpText => "bb_set <layer> <key> <value> — 向黑板写入值（自动推断类型）。layer: system";
    public override int MinPositionalArgs => 3;
    public override int MaxPositionalArgs => 3;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var layer = invocation.PositionalArgs[0].Trim().ToLowerInvariant();
        var key = invocation.PositionalArgs[1].Trim();
        var raw = invocation.PositionalArgs[2].Trim();

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

        ConsoleCommandHelper.SetBlackboardWithTypeInference(bb, key, raw);

        outputChannel.Publish($"[{layer}] {key} = {raw}");
        errorMessage = null;
        return true;
    }
}
