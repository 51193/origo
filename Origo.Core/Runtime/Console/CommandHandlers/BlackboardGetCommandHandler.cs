using System;
using Origo.Core.Abstractions.Console;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Runtime.Console.CommandHandlers;

/// <summary>
///     <c>bb_get</c> 命令：读取黑板中指定键的值。
///     用法：<c>bb_get &lt;layer&gt; &lt;key&gt;</c>
///     layer: system | progress | session
/// </summary>
internal sealed class BlackboardGetCommandHandler : ConsoleCommandHandlerBase
{
    private readonly OrigoRuntime _runtime;

    public BlackboardGetCommandHandler(OrigoRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
    }

    public override string Name => "bb_get";
    public override string HelpText => "bb_get <layer> <key> — 读取指定黑板层的键值。layer: system";
    public override int MinPositionalArgs => 2;
    public override int MaxPositionalArgs => 2;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var layer = invocation.PositionalArgs[0].Trim().ToLowerInvariant();
        var key = invocation.PositionalArgs[1].Trim();

        var bb = ConsoleCommandHelper.ResolveBlackboardLayer(_runtime, layer);

        if (bb is null)
        {
            errorMessage = $"Unknown or unavailable blackboard layer '{layer}'. Use: system";
            return false;
        }

        var all = bb.SerializeAll();
        if (all.TryGetValue(key, out var td))
            outputChannel.Publish($"[{layer}] {key} = {TypedDataObjectConverter.ToObject(td)} (type: {td.DataType.Name})");
        else
            outputChannel.Publish($"[{layer}] Key '{key}' not found.");

        errorMessage = null;
        return true;
    }
}
