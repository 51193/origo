using System;
using Origo.Core.Abstractions.Console;

namespace Origo.Core.Runtime.Console.CommandHandlers;

/// <summary>
///     <c>snd_count</c> 命令：显示当前 SND 实体数量。
/// </summary>
internal sealed class SndCountCommandHandler : ConsoleCommandHandlerBase
{
    private readonly OrigoRuntime _runtime;

    public SndCountCommandHandler(OrigoRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
    }

    public override string Name => "snd_count";
    public override string HelpText => "snd_count — 显示当前 SND 实体数量。";
    public override int MinPositionalArgs => 0;
    public override int MaxPositionalArgs => 0;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var count = _runtime.Snd.GetEntities().Count;
        var msg = $"Snd count: {count}.";

        outputChannel.Publish(msg);
        errorMessage = null;
        return true;
    }
}
