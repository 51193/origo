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
    public override string HelpText => "kill_all — mark all SND entities in the current scene for kill (executed at end of frame).";
    public override int MinPositionalArgs => 0;
    public override int MaxPositionalArgs => 0;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var session = _runtime.SessionManager.ForegroundSession;
        if (session is null)
        {
            errorMessage = "No foreground session — no entities to kill.";
            return false;
        }

        var entities = session.GetEntities();
        var count = entities.Count;
        var marked = 0;
        foreach (var entity in entities)
        {
            if (entity.IsPendingKill)
                continue;
            session.RequestKillEntity(entity.Name);
            marked++;
        }

        outputChannel.Publish($"Marked {marked} of {count} entities for kill (deferred to end of frame).");
        errorMessage = null;
        return true;
    }
}
