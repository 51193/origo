using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Origo.Core.Abstractions.Console;
using Origo.ConsoleBridge;

var input = new InputQueue();
var output = new OutputChannel();
using var server = new ConsoleBridgeServer(input, output, new ConsoleBridgeOptions { Port = 0 });
server.Start();

using var client = new TcpClient();
client.Connect(IPAddress.Loopback, server.ActualPort);
using var reader = new StreamReader(client.GetStream());
using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

writer.WriteLine("package_consumer_probe");
if (!input.WaitFor("package_consumer_probe", TimeSpan.FromSeconds(5)))
    throw new InvalidOperationException("console bridge did not enqueue the client command.");

output.Publish("PACKAGE_CONSUMER_RESPONSE");
var response = reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
if (response != "PACKAGE_CONSUMER_RESPONSE")
    throw new InvalidOperationException($"unexpected console response: '{response}'.");

Console.WriteLine("CONSOLE_BRIDGE_PACKAGE_CONSUMER_OK");
return 0;

internal sealed class InputQueue : IConsoleInputSource
{
    private readonly ConcurrentQueue<string> _lines = new();

    public bool TryDequeueCommand([NotNullWhen(true)] out string? line) => _lines.TryDequeue(out line);

    public void Enqueue(string line) => _lines.Enqueue(line);

    public void Clear()
    {
        while (_lines.TryDequeue(out _))
        {
        }
    }

    public bool WaitFor(string expected, TimeSpan timeout) =>
        SpinWait.SpinUntil(() => TryDequeueCommand(out var line) && line == expected, timeout);
}

internal sealed class OutputChannel : IConsoleOutputChannel
{
    private readonly Dictionary<long, Action<string>> _subscribers = [];
    private long _nextId;

    public long Subscribe(Action<string> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        lock (_subscribers)
        {
            var id = ++_nextId;
            _subscribers[id] = listener;
            return id;
        }
    }

    public bool Unsubscribe(long subscriptionId)
    {
        lock (_subscribers)
        {
            return _subscribers.Remove(subscriptionId);
        }
    }

    public void Publish(string line)
    {
        Action<string>[] handlers;
        lock (_subscribers)
        {
            handlers = [.. _subscribers.Values];
        }

        foreach (var handler in handlers)
            handler(line);
    }
}
