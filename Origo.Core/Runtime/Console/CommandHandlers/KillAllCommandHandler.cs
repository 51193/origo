using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.Entity;

namespace Origo.Core.Runtime.Console.CommandHandlers;

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
        var session = _runtime.SessionManager.ForegroundSession;
        IReadOnlyCollection<ISndEntity> entities;
        if (session is not null)
        {
            entities = session.GetEntities();
        }
        else
        {
            entities = _runtime.ForegroundSceneHost.GetEntities();
        }

        var count = entities.Count;
        var marked = 0;
        foreach (var entity in entities)
        {
            if (entity.IsPendingKill)
                continue;
            if (session is not null)
                session.RequestKillEntity(entity.Name);
            else
                _runtime.ForegroundSceneHost.RequestKillEntity(entity.Name);
            marked++;
        }

        outputChannel.Publish($"Marked {marked} of {count} entities for kill (deferred to end of frame).");
        errorMessage = null;
        return true;
    }
}
