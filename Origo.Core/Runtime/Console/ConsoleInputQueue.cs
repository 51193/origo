using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Origo.Core.Abstractions.Console;

namespace Origo.Core.Runtime.Console;

/// <summary>
///     线程安全的内存命令队列；适配层通过 <see cref="Enqueue" /> 投递，Core 通过
///     <see cref="TryDequeueCommand" /> 消费。
/// </summary>
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix — this type is intentionally a queue
public sealed class ConsoleInputQueue : IConsoleInputSource
#pragma warning restore CA1711
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
