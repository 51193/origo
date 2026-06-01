using System;
using Origo.Core.Abstractions.Console;

namespace Origo.Core.Runtime.Console.CommandHandlers;

/// <summary>
///     <c>kill_all</c> 命令：立即标记当前场景中所有 SND 实体为待销毁，
///     物理销毁在帧末统一执行。
/// </summary>
internal sealed class KillAllCommandHandler : ConsoleCommandHandlerBase
{
    private readonly OrigoRuntime _runtime;

    public KillAllCommandHandler(OrigoRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
    }

    public override string Name => "kill_all";
    public override string HelpText => "kill_all — 立即标记当前场景中所有 SND 实体为待销毁（帧末统一执行）。";
    public override int MinPositionalArgs => 0;
    public override int MaxPositionalArgs => 0;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var entities = _runtime.Snd.GetEntities();
        var count = entities.Count;
        var marked = 0;
        foreach (var entity in entities)
        {
            if (entity.IsPendingKill)
                continue;
            _runtime.Snd.SceneHost.RequestKillEntity(entity.Name);
            marked++;
        }

        outputChannel.Publish($"Marked {marked} of {count} entities for kill (deferred to end of frame).");
        errorMessage = null;
        return true;
    }
}
