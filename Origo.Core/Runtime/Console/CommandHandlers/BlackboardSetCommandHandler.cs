using System;
using System.Globalization;
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

        // Type inference
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv))
            bb.Set(key, iv);
        else if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var fv))
            bb.Set(key, fv);
        else if (bool.TryParse(raw, out var bv))
            bb.Set(key, bv);
        else
            bb.Set(key, raw);

        outputChannel.Publish($"[{layer}] {key} = {raw}");
        errorMessage = null;
        return true;
    }
}
