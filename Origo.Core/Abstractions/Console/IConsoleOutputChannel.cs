using System;

namespace Origo.Core.Abstractions.Console;

/// <summary>
///     Core-side console output publish channel (no local history).
///     Core publishes string messages; adapter layers and strategies
///     subscribe and unsubscribe over their lifecycle.
/// </summary>
public interface IConsoleOutputChannel
{
    /// <summary>
    ///     Register an output listener, returning a subscription id.
    /// </summary>
    long Subscribe(Action<string> listener);

    /// <summary>
    ///     Cancel the specified subscription; returns false if the id
    ///     does not exist.
    /// </summary>
    bool Unsubscribe(long subscriptionId);

    /// <summary>
    ///     Publish a console output message.
    /// </summary>
    void Publish(string line);
}
