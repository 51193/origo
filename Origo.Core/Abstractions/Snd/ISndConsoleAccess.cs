using System;

namespace Origo.Core.Abstractions.Snd;

/// <summary>
///     Console command submission, processing, and output subscription.
///     Strategies can interact with external consoles through this interface.
/// </summary>
public interface ISndConsoleAccess
{
    /// <summary>Submit a console command line. Returns false if no input queue is injected.</summary>
    bool TrySubmitConsoleCommand(string commandLine);

    /// <summary>Process pending console commands.</summary>
    void ProcessConsolePending();

    /// <summary>Subscribe to console output. Returns a subscription ID.</summary>
    long SubscribeConsoleOutput(Action<string> onLine);

    /// <summary>Unsubscribe from console output.</summary>
    void UnsubscribeConsoleOutput(long subscriptionId);
}
