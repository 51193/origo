using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Origo.Core.Abstractions.Console;

namespace Origo.Core.Runtime.Console;

/// <summary>
///     Thread-safe in-memory command queue; the adapter layer enqueues commands via <see cref="Enqueue" />,
///     while the Core consumes them via <see cref="TryDequeueCommand" />.
/// </summary>
public sealed class ConsoleInputBuffer : IConsoleInputSource
{
    private readonly object _lock = new();
    private readonly Queue<string> _queue = new();

    public bool TryDequeueCommand([NotNullWhen(true)] out string? line)
    {
        lock (_lock)
        {
            if (_queue.Count == 0)
            {
                line = null;
                return false;
            }

            line = _queue.Dequeue();
            return true;
        }
    }

    public void Enqueue(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        lock (_lock)
        {
            _queue.Enqueue(line.Trim());
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _queue.Clear();
        }
    }
}
