using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Origo.ConsoleBridge.Tests;

public class ConsoleBridgeServerCommunicationTests
{
    [Fact]
    public void ClientSendCommand_ArrivesInInputQueue()
    {
        var (server, (input, _)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        writer.WriteLine("help");

        string? cmd = null;
        var ok = ConsoleBridgeTestInfrastructure.SpinUntil(() => input.TryDequeueCommand(out cmd), ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
        Assert.True(ok, "Command should arrive in input queue");
        Assert.Equal("help", cmd);

        server.Dispose();
    }

    [Fact]
    public void ClientSendMultipleCommands_ArriveInFifoOrder()
    {
        var (server, (input, _)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        writer.WriteLine("first");
        writer.WriteLine("second");
        writer.WriteLine("third");

        var lines = new List<string>();
        ConsoleBridgeTestInfrastructure.SpinUntil(() =>
        {
            while (input.TryDequeueCommand(out var l))
                lines.Add(l);
            return lines.Count >= 3;
        }, ConsoleBridgeTestInfrastructure.OutputTimeoutMs);

        Assert.Equal(3, lines.Count);
        Assert.Equal("first", lines[0]);
        Assert.Equal("second", lines[1]);
        Assert.Equal("third", lines[2]);

        server.Dispose();
    }

    [Fact]
    public void ClientSendCommand_ManyCommands_StressTest()
    {
        var (server, (input, _)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        for (var i = 0; i < ConsoleBridgeTestInfrastructure.StressCommandCount; i++)
            writer.WriteLine($"cmd_{i}");

        var count = 0;
        ConsoleBridgeTestInfrastructure.SpinUntil(() =>
        {
            while (input.TryDequeueCommand(out _))
                count++;
            return count >= ConsoleBridgeTestInfrastructure.StressCommandCount;
        }, ConsoleBridgeTestInfrastructure.OutputTimeoutMs);

        Assert.True(count >= ConsoleBridgeTestInfrastructure.StressCommandCount, $"Expected >= {ConsoleBridgeTestInfrastructure.StressCommandCount}, got {count}");

        server.Dispose();
    }

    [Fact]
    public void ClientSendCommand_LongLine_Arrives()
    {
        var (server, (input, _)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        var longCmd = new string('x', 4096);
        writer.WriteLine(longCmd);

        string? cmd = null;
        ConsoleBridgeTestInfrastructure.SpinUntil(() => input.TryDequeueCommand(out cmd) && cmd == longCmd, ConsoleBridgeTestInfrastructure.OutputTimeoutMs);
        Assert.Equal(longCmd, cmd);

        server.Dispose();
    }

    [Fact]
    public void ClientSendCommand_Unicode_Arrives()
    {
        var (server, (input, _)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        writer.WriteLine("héllo 世界 🌍");

        string? cmd = null;
        ConsoleBridgeTestInfrastructure.SpinUntil(() => input.TryDequeueCommand(out cmd) && cmd == "héllo 世界 🌍", ConsoleBridgeTestInfrastructure.OutputTimeoutMs);
        Assert.Equal("héllo 世界 🌍", cmd);

        server.Dispose();
    }

    [Fact]
    public void ClientSendCommand_LeadingAndTrailingWhitespace_Trimmed()
    {
        var (server, (input, _)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        writer.WriteLine("  \t  hello  \t  ");

        string? cmd = null;
        ConsoleBridgeTestInfrastructure.SpinUntil(() => input.TryDequeueCommand(out cmd) && cmd == "hello", ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
        Assert.Equal("hello", cmd);

        server.Dispose();
    }

    [Fact]
    public void BlankLines_AreNotEnqueued()
    {
        var (server, (input, _)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        writer.WriteLine("   ");
        writer.WriteLine("");
        writer.WriteLine("valid");

        var ok = ConsoleBridgeTestInfrastructure.SpinUntil(() =>
        {
            while (input.TryDequeueCommand(out var l))
                if (l == "valid")
                    return true;
            return false;
        }, ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
        Assert.True(ok, "Only 'valid' should appear in input queue (blank/whitespace lines filtered)");

        server.Dispose();
    }

    [Fact]
    public void ClientSendCommand_OnlyWhitespace_NothingEnqueued()
    {
        var (server, (input, _)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        writer.WriteLine("\t  ");
        writer.WriteLine("    ");
        writer.WriteLine("__SENTINEL__");

        ConsoleBridgeTestInfrastructure.SpinUntil(() => input.TryDequeueCommand(out var l) && l == "__SENTINEL__", ConsoleBridgeTestInfrastructure.CommandTimeoutMs);

        Assert.False(input.TryDequeueCommand(out _));

        server.Dispose();
    }

    [Fact]
    public void OutputChannel_Publish_ArrivesAtClient()
    {
        var (server, (_, output)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        output.Publish("hello from console");

        var line = ConsoleBridgeTestInfrastructure.ReadLineWithTimeout(reader, ConsoleBridgeTestInfrastructure.OutputTimeoutMs);
        Assert.NotNull(line);
        Assert.Equal("hello from console", line);

        server.Dispose();
    }

    [Fact]
    public void OutputChannel_MultiplePublishes_AllDelivered()
    {
        var (server, (_, output)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        output.Publish("line1");
        output.Publish("line2");
        output.Publish("line3");

        var received = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var line = ConsoleBridgeTestInfrastructure.ReadLineWithTimeout(reader, ConsoleBridgeTestInfrastructure.OutputTimeoutMs);
            Assert.NotNull(line);
            received.Add(line!);
        }

        Assert.Equal(3, received.Count);
        Assert.Equal("line1", received[0]);
        Assert.Equal("line2", received[1]);
        Assert.Equal("line3", received[2]);

        server.Dispose();
    }

    [Fact]
    public void OutputChannel_PublishNullString_Throws()
    {
        var (server, (_, output)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();

        Assert.Throws<ArgumentNullException>(() => output.Publish(null!));

        server.Dispose();
    }

    [Fact]
    public void OutputChannel_LargeVolume_ManyLines_AllDelivered()
    {
        var (server, (_, output)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        for (var i = 0; i < ConsoleBridgeTestInfrastructure.StressCommandCount; i++)
            output.Publish($"out_{i}");

        var count = 0;
        for (var i = 0; i < ConsoleBridgeTestInfrastructure.StressCommandCount; i++)
        {
            var line = ConsoleBridgeTestInfrastructure.ReadLineWithTimeout(reader, ConsoleBridgeTestInfrastructure.OutputTimeoutMs);
            if (line is null)
                break;
            count++;
        }

        Assert.True(count >= ConsoleBridgeTestInfrastructure.StressCommandCount, $"Expected >= {ConsoleBridgeTestInfrastructure.StressCommandCount}, got {count}");

        server.Dispose();
    }

    [Fact]
    public async Task OutputChannel_ConcurrentPublish_AllDelivered()
    {
        var (server, (_, output)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        const int concurrentCount = 10;
        var tasks = new Task[concurrentCount];
        for (var t = 0; t < concurrentCount; t++)
        {
            var prefix = t;
            tasks[t] = Task.Run(() =>
            {
                for (var i = 0; i < 10; i++)
                    output.Publish($"t{prefix}_{i}");
            }, TestContext.Current.CancellationToken);
        }

        await Task.WhenAll(tasks);

        var received = new List<string>();
        string? line;
        while ((line = ConsoleBridgeTestInfrastructure.ReadLineWithTimeout(reader, ConsoleBridgeTestInfrastructure.OutputTimeoutMs)) is not null)
            received.Add(line);

        Assert.True(received.Count >= concurrentCount * 10,
            $"Expected >= {concurrentCount * 10}, got {received.Count}");

        server.Dispose();
    }

    [Fact]
    public void PendingOutput_BufferOverflow_DropsOldestLines()
    {
        var (server, (_, output)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        var bufferLimit = 1000;
        var totalLines = bufferLimit + 1;
        for (var i = 0; i < totalLines; i++)
            output.Publish($"pending_{i}");

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        var received = new List<string>();
        for (var i = 0; i < totalLines; i++)
        {
            var line = ConsoleBridgeTestInfrastructure.ReadLineWithTimeout(reader, ConsoleBridgeTestInfrastructure.OutputTimeoutMs);
            if (line is null)
                break;
            received.Add(line);
        }

        Assert.Equal(bufferLimit, received.Count);
        Assert.Equal("pending_1", received[0]);
        Assert.Equal($"pending_{bufferLimit}", received[^1]);
        Assert.DoesNotContain("pending_0", received);

        server.Dispose();
    }

    [Fact]
    public void PendingOutput_WithinLimit_AllDeliveredOnConnect()
    {
        var (server, (_, output)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        var lineCount = 500;
        for (var i = 0; i < lineCount; i++)
            output.Publish($"pending_{i}");

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());

        var received = new List<string>();
        for (var i = 0; i < lineCount; i++)
        {
            var line = ConsoleBridgeTestInfrastructure.ReadLineWithTimeout(reader, ConsoleBridgeTestInfrastructure.OutputTimeoutMs);
            if (line is null)
                break;
            received.Add(line);
        }

        Assert.Equal(lineCount, received.Count);
        Assert.Equal("pending_0", received[0]);
        Assert.Equal($"pending_{lineCount - 1}", received[^1]);

        server.Dispose();
    }
}
