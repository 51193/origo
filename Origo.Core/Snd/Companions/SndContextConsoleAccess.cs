using System;
using Origo.Core.Abstractions.Snd;

namespace Origo.Core.Snd.Companions;

/// <summary>Console command submission and output subscription for <see cref="SndContext" />.</summary>
internal sealed class SndContextConsoleAccess(SndContext owner) : ISndConsoleAccess
{
    public bool TrySubmitConsoleCommand(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return false;

        owner.Runtime.ConsoleInput?.Enqueue(commandLine.Trim());
        return owner.Runtime.ConsoleInput is not null;
    }

    public void ProcessConsolePending() => owner.Runtime.Console?.ProcessPending();

    public long SubscribeConsoleOutput(Action<string> onLine)
    {
        ArgumentNullException.ThrowIfNull(onLine);
        var channel = owner.Runtime.ConsoleOutputChannel
                      ?? throw new InvalidOperationException("Console output channel is not available.");
        return channel.Subscribe(line => onLine(line ?? string.Empty));
    }

    public void UnsubscribeConsoleOutput(long subscriptionId)
    {
        if (subscriptionId <= 0)
            return;
        owner.Runtime.ConsoleOutputChannel?.Unsubscribe(subscriptionId);
    }
}
