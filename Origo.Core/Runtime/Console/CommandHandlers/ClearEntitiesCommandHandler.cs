using System;
using Origo.Core.Abstractions.Console;

namespace Origo.Core.Runtime.Console.CommandHandlers;

/// <summary>
///     <c>request_clear_entities</c> 命令：请求清理当前场景中所有已生成的 SND 实体（帧末延迟执行）。
/// </summary>
internal sealed class ClearEntitiesCommandHandler : ConsoleCommandHandlerBase
{
    private readonly OrigoRuntime _runtime;

    public ClearEntitiesCommandHandler(OrigoRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
    }

    public override string Name => "request_clear_entities";
    public override string HelpText => "request_clear_entities — 请求清理当前场景中所有已生成的 SND 实体（帧末延迟执行）。";
    public override int MinPositionalArgs => 0;
    public override int MaxPositionalArgs => 0;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var count = _runtime.Snd.GetEntities().Count;
        _runtime.EnqueueBusinessDeferred(() => _runtime.Snd.ClearAll());
        outputChannel.Publish($"Requested clear of {count} entities (deferred to end of frame).");
        errorMessage = null;
        return true;
    }
}
