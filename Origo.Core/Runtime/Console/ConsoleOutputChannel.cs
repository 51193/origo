using System.Threading;
using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Logging;

namespace Origo.Core.Runtime.Console;

/// <summary>
///     Console output publishing channel (producer-consumer).
///     The Core does not retain history; it only broadcasts output to current subscribers.
/// </summary>
/// <param name="logger">Optional logger; defaults to <see cref="NullLogger.Instance" />.</param>
public sealed class ConsoleOutputChannel(ILogger? logger = null) : IConsoleOutputChannel
{
    private readonly ILogger _logger = logger ?? NullLogger.Instance;
    private readonly Dictionary<long, Action<string>> _listeners = [];
    private readonly Lock _lock = new();
    private long _nextId = 1;

    /// <summary>Subscribes a listener to published output lines.</summary>
    /// <param name="listener">The callback invoked for each published line.</param>
    /// <returns>A subscription id used with <see cref="Unsubscribe" />.</returns>
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

    /// <summary>Removes a subscription by id.</summary>
    /// <param name="subscriptionId">The id returned by <see cref="Subscribe" />.</param>
    /// <returns>True when the subscription existed and was removed.</returns>
    public bool Unsubscribe(long subscriptionId)
    {
        lock (_lock)
        {
            return _listeners.Remove(subscriptionId);
        }
    }

    /// <summary>
    ///     Publishes a line to all current subscribers. Subscriber exceptions
    ///     are collected and rethrown (aggregated when multiple failed) after
    ///     every subscriber had its chance to run.
    /// </summary>
    /// <param name="line">The line to publish.</param>
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
                _logger.Log(LogLevel.Warning, nameof(ConsoleOutputChannel),
                    new LogMessageBuilder()
                        .AddContext("subscriberErrorCount", errorCount)
                        .Build($"Subscriber threw during Publish: {ex.Message}"));
            }
        }

        if (errorCount > 1)
            throw new AggregateException(
                $"Multiple listeners ({errorCount}) threw exceptions during Publish.", firstError!);
        if (firstError is not null)
            throw firstError;
    }
}
