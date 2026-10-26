using System;
using System.Threading;
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
    private readonly Lock _lock = new();
    private readonly Queue<string> _queue = new();
    private readonly int _maxPendingCommands;

    /// <summary>
    ///     Creates a command queue bounded to <paramref name="maxPendingCommands" />.
    ///     When the bound is reached, the oldest pending command is dropped and
    ///     <see cref="DroppedCommandCount" /> increments, so a flooding producer
    ///     cannot grow the queue without bound.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the bound is not positive.</exception>
    public ConsoleInputBuffer(int maxPendingCommands = 4096)
    {
        if (maxPendingCommands <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPendingCommands), maxPendingCommands,
                "The pending-command bound must be positive.");
        _maxPendingCommands = maxPendingCommands;
    }

    /// <summary>Number of commands currently waiting to be processed.</summary>
    public int PendingCount
    {
        get
        {
            lock (_lock)
                return _queue.Count;
        }
    }

    /// <summary>Number of commands dropped because the pending bound was reached.</summary>
    public long DroppedCommandCount { get; private set; }

    /// <summary>Dequeues the next command, if any.</summary>
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

    /// <summary>Enqueues a command line, trimming surrounding whitespace; blank lines are ignored.</summary>
    public void Enqueue(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        lock (_lock)
        {
            if (_queue.Count >= _maxPendingCommands)
            {
                _queue.Dequeue();
                DroppedCommandCount++;
            }

            _queue.Enqueue(line.Trim());
        }
    }

    /// <summary>Clears all pending commands.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _queue.Clear();
        }
    }
}
