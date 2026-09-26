using System;
using Origo.Core.Abstractions.Console;

namespace Origo.Core.Runtime.Console.CommandHandlers;

/// <summary><c>snd_count</c> — show the current SND entity count.</summary>
internal sealed class SndCountCommandHandler : ConsoleCommandHandlerBase
{
    private readonly OrigoRuntime _runtime;

    public SndCountCommandHandler(OrigoRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
    }

    public override string Name => "snd_count";
    public override string HelpText => "snd_count — show the current SND entity count.";
    public override int MinPositionalArgs => 0;
    public override int MaxPositionalArgs => 0;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var session = _runtime.SessionManager.ForegroundSession;
        var count = session?.GetEntities().Count ?? 0;
        var msg = $"Snd count: {count}.";

        outputChannel.Publish(msg);
        errorMessage = null;
        return true;
    }
}
