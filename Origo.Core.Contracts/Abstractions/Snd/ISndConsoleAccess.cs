using System;

namespace Origo.Core.Abstractions.Snd;

/// <summary>
///     Console command submission and output subscription for business code.
///     Processing is owned by the Core frame driver and is not exposed here.
/// </summary>
public interface ISndConsoleAccess
{
    /// <summary>Submit a console command line. Returns false if no input queue is injected.</summary>
    bool TrySubmitConsoleCommand(string commandLine);

    /// <summary>Subscribe to console output. Returns a subscription ID.</summary>
    long SubscribeConsoleOutput(Action<string> onLine);

    /// <summary>Unsubscribe from console output.</summary>
    void UnsubscribeConsoleOutput(long subscriptionId);
}
