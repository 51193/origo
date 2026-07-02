using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Console;

namespace Origo.Core.Runtime.Console;

/// <summary>
///     控制台输出发布通道（生产者-消费者）。
///     Core 不保存历史，仅将输出广播给当前订阅者。
/// </summary>
public sealed class ConsoleOutputChannel : IConsoleOutputChannel
{
    private readonly Dictionary<long, Action<string>> _listeners = [];
    private readonly object _lock = new();
    private long _nextId = 1;

    public long Subscribe(Action<string> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        lock (_lock)
        {
            var id = _nextId++;
            _listeners[id] = listener;
            return id;
        }
    }

    public bool Unsubscribe(long subscriptionId)
    {
        lock (_lock)
        {
            return _listeners.Remove(subscriptionId);
        }
    }

    public void Publish(string line)
    {
        Action<string>[] targets;
        lock (_lock)
        {
            targets = new Action<string>[_listeners.Count];
            _listeners.Values.CopyTo(targets, 0);
        }

        var payload = line ?? throw new ArgumentNullException(nameof(line));
        Exception? firstError = null;
        var errorCount = 0;
        foreach (var listener in targets)
        {
            try
            {
                listener(payload);
            }
            catch (Exception ex)
            {
                firstError ??= ex;
                errorCount++;
            }
        }

        if (errorCount > 1)
            throw new AggregateException(
                $"Multiple listeners ({errorCount}) threw exceptions during Publish.", firstError!);
        if (firstError is not null)
            throw firstError;
    }
}
