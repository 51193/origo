using System;
using Origo.Core.Abstractions.Console;

namespace Origo.Core.Runtime.Console.CommandHandlers;

/// <summary>
///     <c>clear_entities</c> 命令：销毁所有已生成的 SND 实体。
/// </summary>
internal sealed class ClearEntitiesCommandHandler : ConsoleCommandHandlerBase
{
    private readonly OrigoRuntime _runtime;

    public ClearEntitiesCommandHandler(OrigoRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
    }

    public override string Name => "clear_entities";
    public override string HelpText => "clear_entities — 销毁所有已生成的 SND 实体。";
    public override int MinPositionalArgs => 0;
    public override int MaxPositionalArgs => 0;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var count = _runtime.Snd.GetEntities().Count;
        _runtime.Snd.ClearAll();
        outputChannel.Publish($"Cleared {count} entities.");
        errorMessage = null;
        return true;
    }
}
