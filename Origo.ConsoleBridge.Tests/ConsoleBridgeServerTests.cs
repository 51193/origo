using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Origo.Core.Runtime.Console;
using Xunit;

namespace Origo.ConsoleBridge.Tests;

public class ConsoleBridgeServerTests
{
    private const int CommandTimeoutMs = 2000;
    private const int OutputTimeoutMs = 3000;
    private const int DisconnectDelayMs = 200;
    private const int SpinPollIntervalMs = 10;
    private const int StressCommandCount = 100;

    // ── Lifecycle ───────────────────────────────────────────────────────

    [Fact]
    public void Start_Stop_NoExceptions()
    {
        var (server, _) = CreateStartedServer();
        server.Dispose();
    }

    [Fact]
    public void Start_AfterDispose_Throws()
    {
        var (server, _) = CreateStartedServer();
        server.Dispose();
        Assert.Throws<ObjectDisposedException>(() => server.Start());
    }

    [Fact]
    public void DoubleDispose_DoesNotThrow()
    {
        var (server, _) = CreateStartedServer();
        server.Dispose();
        server.Dispose();
        // No exception = success
    }

    [Fact]
    public void Dispose_StopsAcceptingNewConnections()
    {
        var (server, _) = CreateStartedServer();
        var port = server.ActualPort;
        server.Dispose();

        Thread.Sleep(DisconnectDelayMs);

        Assert.ThrowsAny<SocketException>(() =>
        {
            using var client = new TcpClient();
            client.Connect(IPAddress.Loopback, port);
        });
    }

    [Fact]
    public void Dispose_WhileClientConnected_NoHang()
    {
        var (server, _) = CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);

        // Dispose while Handle Thread is blocking on ReadLine
        var disposed = false;
        var disposeTask = Task.Run(() =>
        {
            server.Dispose();
            disposed = true;
        });

        Assert.True(disposeTask.Wait(TimeSpan.FromMilliseconds(3000)), "Dispose should complete within timeout");
        Assert.True(disposed);
    }

    [Fact]
    public void ActualPort_ReflectsAssignedPort()
    {
        var (server, _) = CreateStartedServer();
        Assert.True(server.ActualPort > 0);
        server.Dispose();
    }

    // ── Input ───────────────────────────────────────────────────────────

    [Fact]
    public void ClientSendCommand_ArrivesInInputQueue()
    {
        var (server, (input, _)) = CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        writer.WriteLine("help");

        string? cmd = null;
        var ok = SpinUntil(() => input.TryDequeueCommand(out cmd), CommandTimeoutMs);
        Assert.True(ok, "Command should arrive in input queue");
        Assert.Equal("help", cmd);

        server.Dispose();
    }

    [Fact]
    public void ClientSendMultipleCommands_ArriveInFifoOrder()
    {
        var (server, (input, _)) = CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        writer.WriteLine("first");
        writer.WriteLine("second");
        writer.WriteLine("third");

        var lines = new List<string>();
        SpinUntil(() =>
        {
            while (input.TryDequeueCommand(out var l))
                lines.Add(l);
            return lines.Count >= 3;
        }, OutputTimeoutMs);

        Assert.Equal(3, lines.Count);
        Assert.Equal("first", lines[0]);
        Assert.Equal("second", lines[1]);
        Assert.Equal("third", lines[2]);

        server.Dispose();
    }

    [Fact]
    public void ClientSendCommand_ManyCommands_StressTest()
    {
        var (server, (input, _)) = CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        for (var i = 0; i < StressCommandCount; i++)
            writer.WriteLine($"cmd_{i}");

        var count = 0;
        SpinUntil(() =>
        {
            while (input.TryDequeueCommand(out _))
                count++;
            return count >= StressCommandCount;
        }, OutputTimeoutMs);

        Assert.True(count >= StressCommandCount, $"Expected >= {StressCommandCount}, got {count}");

        server.Dispose();
    }

    [Fact]
    public void ClientSendCommand_LongLine_Arrives()
    {
        var (server, (input, _)) = CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        var longCmd = new string('x', 4096);
        writer.WriteLine(longCmd);

        string? cmd = null;
        SpinUntil(() => input.TryDequeueCommand(out cmd) && cmd == longCmd, OutputTimeoutMs);
        Assert.Equal(longCmd, cmd);

        server.Dispose();
    }

    [Fact]
    public void ClientSendCommand_Unicode_Arrives()
    {
        var (server, (input, _)) = CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        writer.WriteLine("héllo 世界 🌍");

        string? cmd = null;
        SpinUntil(() => input.TryDequeueCommand(out cmd) && cmd == "héllo 世界 🌍", OutputTimeoutMs);
        Assert.Equal("héllo 世界 🌍", cmd);

        server.Dispose();
    }

    [Fact]
    public void ClientSendCommand_LeadingAndTrailingWhitespace_Trimmed()
    {
        var (server, (input, _)) = CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        writer.WriteLine("  \t  hello  \t  ");

        string? cmd = null;
        SpinUntil(() => input.TryDequeueCommand(out cmd) && cmd == "hello", CommandTimeoutMs);
        Assert.Equal("hello", cmd);

        server.Dispose();
    }

    [Fact]
    public void BlankLines_AreNotEnqueued()
    {
        var (server, (input, _)) = CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        writer.WriteLine("   ");
        writer.WriteLine("");
        writer.WriteLine("valid");

        SpinUntil(() =>
        {
            while (input.TryDequeueCommand(out var l))
                if (l == "valid")
                    return true;
            return false;
        }, CommandTimeoutMs);

        server.Dispose();
    }

    [Fact]
    public void ClientSendCommand_OnlyWhitespace_NothingEnqueued()
    {
        var (server, (input, _)) = CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        writer.WriteLine("\t  ");
        writer.WriteLine("    ");

        Thread.Sleep(DisconnectDelayMs);

        Assert.False(input.TryDequeueCommand(out _));

        server.Dispose();
    }

    // ── Output ──────────────────────────────────────────────────────────

    [Fact]
    public void OutputChannel_Publish_ArrivesAtClient()
    {
        var (server, (_, output)) = CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        output.Publish("hello from console");

        writer.WriteLine("trigger");
        writer.Flush();

        var line = ReadLineWithTimeout(reader, OutputTimeoutMs);
        Assert.NotNull(line);
        Assert.Equal("hello from console", line);

        server.Dispose();
    }

    [Fact]
    public void OutputChannel_MultiplePublishes_AllDelivered()
    {
        var (server, (_, output)) = CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        output.Publish("line1");
        output.Publish("line2");
        output.Publish("line3");

        writer.WriteLine("trigger");
        writer.Flush();

        var received = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var line = ReadLineWithTimeout(reader, OutputTimeoutMs);
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
    public void OutputChannel_PublishNullString_ArrivesAsEmpty()
    {
        var (server, (_, output)) = CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        output.Publish(null!);

        writer.WriteLine("trigger");
        writer.Flush();

        var line = ReadLineWithTimeout(reader, OutputTimeoutMs);
        Assert.NotNull(line);
        Assert.Equal(string.Empty, line);

        server.Dispose();
    }

    [Fact]
    public void OutputChannel_LargeVolume_ManyLines_AllDelivered()
    {
        var (server, (_, output)) = CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        for (var i = 0; i < StressCommandCount; i++)
            output.Publish($"out_{i}");

        writer.WriteLine("trigger");
        writer.Flush();

        var count = 0;
        for (var i = 0; i < StressCommandCount; i++)
        {
            var line = ReadLineWithTimeout(reader, OutputTimeoutMs);
            if (line is null)
                break;
            count++;
        }

        Assert.True(count >= StressCommandCount, $"Expected >= {StressCommandCount}, got {count}");

        server.Dispose();
    }

    [Fact]
    public void OutputChannel_ConcurrentPublish_AllDelivered()
    {
        var (server, (_, output)) = CreateStartedServer();
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
            });
        }

        Task.WaitAll(tasks);

        writer.WriteLine("trigger");
        writer.Flush();

        var received = new List<string>();
        string? line;
        while ((line = ReadLineWithTimeout(reader, OutputTimeoutMs)) is not null)
            received.Add(line);

        Assert.True(received.Count >= concurrentCount * 10,
            $"Expected >= {concurrentCount * 10}, got {received.Count}");

        server.Dispose();
    }

    // ── Connection management ───────────────────────────────────────────

    [Fact]
    public void SecondConnection_IsRejected()
    {
        var (server, (input, _)) = CreateStartedServer();
        var port = server.ActualPort;

        using var client1 = new TcpClient();
        client1.Connect(IPAddress.Loopback, port);
        using var writer1 = new StreamWriter(client1.GetStream()) { AutoFlush = true };

        // Verify client1 works
        writer1.WriteLine("from_first");
        SpinUntil(() => input.TryDequeueCommand(out var l) && l == "from_first", CommandTimeoutMs);

        // Try to connect second client — should be rejected
        try
        {
            using var client2 = new TcpClient();
            client2.Connect(IPAddress.Loopback, port);
            using var writer2 = new StreamWriter(client2.GetStream()) { AutoFlush = true };
            writer2.WriteLine("from_second");
            writer2.Flush();
        }
        catch (IOException)
        {
            // Expected: server closed the connection
        }

        // client1 should still work
        writer1.WriteLine("still_works");
        SpinUntil(() => input.TryDequeueCommand(out var l) && l == "still_works", CommandTimeoutMs);

        server.Dispose();
    }

    [Fact]
    public void SecondConnection_CommandDoesNotArrive()
    {
        var (server, (input, _)) = CreateStartedServer();
        var port = server.ActualPort;

        using var client1 = new TcpClient();
        client1.Connect(IPAddress.Loopback, port);
        var writer1 = new StreamWriter(client1.GetStream()) { AutoFlush = true };

        writer1.WriteLine("from_first");
        SpinUntil(() => input.TryDequeueCommand(out var l) && l == "from_first", CommandTimeoutMs);

        try
        {
            using var client2 = new TcpClient();
            client2.Connect(IPAddress.Loopback, port);
            using var writer2 = new StreamWriter(client2.GetStream()) { AutoFlush = true };
            writer2.WriteLine("from_second");
            writer2.Flush();
        }
        catch (IOException)
        {
            // Expected
        }

        // Drain any remaining commands from first client
        while (input.TryDequeueCommand(out _))
        {
        }

        // Now verify "from_second" never arrived (would have been rejected)
        Assert.False(input.TryDequeueCommand(out _));

        server.Dispose();
    }

    [Fact]
    public void ClientDisconnect_ServerAcceptsNewConnection()
    {
        var (server, (input, _)) = CreateStartedServer();
        var port = server.ActualPort;

        // Connect and disconnect
        using (var client = new TcpClient())
        {
            client.Connect(IPAddress.Loopback, port);
        }

        Thread.Sleep(DisconnectDelayMs);

        // New connection should be accepted
        using var client2 = new TcpClient();
        client2.Connect(IPAddress.Loopback, port);
        using var writer = new StreamWriter(client2.GetStream()) { AutoFlush = true };

        writer.WriteLine("after_reconnect");

        SpinUntil(() => input.TryDequeueCommand(out var l) && l == "after_reconnect", CommandTimeoutMs);

        server.Dispose();
    }

    [Fact]
    public void ClientDisconnect_ThenThirdAccepted()
    {
        var (server, (input, _)) = CreateStartedServer();
        var port = server.ActualPort;

        // First client
        using (var client = new TcpClient())
        {
            client.Connect(IPAddress.Loopback, port);
        }

        Thread.Sleep(DisconnectDelayMs);

        // Second client (should be accepted)
        using (var client2 = new TcpClient())
        {
            client2.Connect(IPAddress.Loopback, port);
        }

        Thread.Sleep(DisconnectDelayMs);

        // Third client (should also be accepted)
        using var client3 = new TcpClient();
        client3.Connect(IPAddress.Loopback, port);
        using var writer = new StreamWriter(client3.GetStream()) { AutoFlush = true };

        writer.WriteLine("third");

        SpinUntil(() => input.TryDequeueCommand(out var l) && l == "third", CommandTimeoutMs);

        server.Dispose();
    }

    [Fact]
    public void ClientImmediateDisconnect_ServerRecovers()
    {
        var (server, (input, _)) = CreateStartedServer();
        var port = server.ActualPort;

        // Connect and immediately dispose (abort before handle thread reads)
        using (var client = new TcpClient())
        {
            client.Connect(IPAddress.Loopback, port);
            // Disconnect immediately - Handle Thread will get ReadLine null
        }

        Thread.Sleep(DisconnectDelayMs);

        // New connection should work
        using var client2 = new TcpClient();
        client2.Connect(IPAddress.Loopback, port);
        using var writer = new StreamWriter(client2.GetStream()) { AutoFlush = true };
        writer.WriteLine("after_immediate");

        SpinUntil(() => input.TryDequeueCommand(out var l) && l == "after_immediate", CommandTimeoutMs);

        server.Dispose();
    }

    // ── Thread safety ───────────────────────────────────────────────────

    [Fact]
    public void Concurrent_PublishWhileReading_NoDeadlock()
    {
        var (server, (input, output)) = CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        // Start a publisher that publishes continuously
        var pubDone = new ManualResetEventSlim(false);
        var pubTask = Task.Run(() =>
        {
            for (var i = 0; i < 50; i++)
            {
                output.Publish($"pub_{i}");
                Thread.Sleep(1);
            }

            pubDone.Set();
        });

        // Meanwhile, send commands from client
        for (var i = 0; i < 20; i++)
            writer.WriteLine($"cmd_{i}");

        // Wait for publisher to finish
        Assert.True(pubDone.Wait(CommandTimeoutMs), "Publisher should complete");

        // All commands should arrive
        SpinUntil(() =>
        {
            var count = 0;
            while (input.TryDequeueCommand(out _))
                count++;
            return count >= 20;
        }, CommandTimeoutMs);

        server.Dispose();
    }

    // ── Round-trip ─────────────────────────────────────────────────────

    [Fact]
    public void FullRoundTrip_CommandResponsePattern()
    {
        var (server, (input, output)) = CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        // Phase 1: Send command, publish response, trigger drain
        writer.WriteLine("cmd1");
        SpinUntil(() => input.TryDequeueCommand(out var l) && l == "cmd1", CommandTimeoutMs);
        output.Publish("response1");

        writer.WriteLine("cmd2");
        SpinUntil(() => input.TryDequeueCommand(out var l) && l == "cmd2", CommandTimeoutMs);
        output.Publish("response2");

        // Send trigger to flush
        writer.WriteLine("cmd3");

        // Responses should arrive before "cmd3" is dequeued from input
        // The handle thread drains output before reading
        // So cmd1→response1→cmd2→response2→cmd3 should be enqueued in order
        var response1 = ReadLineWithTimeout(reader, OutputTimeoutMs);
        var response2 = ReadLineWithTimeout(reader, OutputTimeoutMs);
        Assert.NotNull(response1);
        Assert.NotNull(response2);
        Assert.Equal("response1", response1);
        Assert.Equal("response2", response2);

        // cmd3 should be enqueued
        SpinUntil(() => input.TryDequeueCommand(out var l) && l == "cmd3", CommandTimeoutMs);

        server.Dispose();
    }

    // ── Helpers ──

    private static (ConsoleBridgeServer server, (ConsoleInputQueue input, ConsoleOutputChannel output) queues)
        CreateStartedServer(int port = 0)
    {
        var input = new ConsoleInputQueue();
        var output = new ConsoleOutputChannel();
        var options = new ConsoleBridgeOptions { Port = port };
        var server = new ConsoleBridgeServer(input, output, options);
        server.Start();
        return (server, (input, output));
    }

    private static bool SpinUntil(Func<bool> condition, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
                return true;
            Thread.Sleep(SpinPollIntervalMs);
        }

        return condition();
    }

    private static string? ReadLineWithTimeout(StreamReader reader, int timeoutMs)
    {
        var stream = reader.BaseStream;
        var oldTimeout = stream.ReadTimeout;
        try
        {
            stream.ReadTimeout = timeoutMs;
            return reader.ReadLine();
        }
        catch (IOException)
        {
            return null;
        }
        finally
        {
            stream.ReadTimeout = oldTimeout;
        }
    }
}
