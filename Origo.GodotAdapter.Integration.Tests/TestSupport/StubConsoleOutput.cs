using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Console;

namespace Origo.GodotAdapter.Integration.Tests.TestSupport;

public sealed class StubConsoleOutput : IConsoleOutputChannel
{
    private long _nextId;
    private readonly Dictionary<long, Action<string>> _listeners = [];

    public List<string> PublishedLines { get; } = [];

    public long Subscribe(Action<string> listener)
    {
        var id = ++_nextId;
        _listeners[id] = listener;
        return id;
    }

    public bool Unsubscribe(long subscriptionId) => _listeners.Remove(subscriptionId);

    public void Publish(string line)
    {
        PublishedLines.Add(line);
        foreach (var listener in _listeners.Values)
            listener(line);
    }
}
