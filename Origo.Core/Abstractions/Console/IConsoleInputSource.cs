using System.Diagnostics.CodeAnalysis;

namespace Origo.Core.Abstractions.Console;

/// <summary>
///     Console input abstraction between the Core and adapter layers.
///     The adapter layer submits commands via <see cref="Enqueue" />;
///     the Core consumes them per-frame via <see cref="TryDequeueCommand" />.
/// </summary>
public interface IConsoleInputSource
{
    /// <summary>
    ///     Attempt to dequeue a line of command text for parsing; returns
    ///     false when no input is available.
    /// </summary>
    bool TryDequeueCommand([NotNullWhen(true)] out string? line);

    /// <summary>
    ///     Append a command line to the queue. Blank lines are ignored.
    /// </summary>
    void Enqueue(string line);

    /// <summary>
    ///     Clear all pending commands from the queue.
    /// </summary>
    void Clear();
}
