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
    private const int ConnectRetryIntervalMs = 5;
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

        AssertConnectionRefused(port, 200);
    }

    [Fact]
    public async Task Dispose_WhileClientConnected_NoHang()
    {
        var (server, _) = CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);

        var disposed = false;
        var disposeTask = Task.Run(() =>
        {
            server.Dispose();
            disposed = true;
        }, TestContext.Current.CancellationToken);

        await disposeTask.WaitAsync(TimeSpan.FromMilliseconds(3000), TestContext.Current.CancellationToken);
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

        var ok = SpinUntil(() =>
        {
            while (input.TryDequeueCommand(out var l))
                if (l == "valid")
                    return true;
            return false;
        }, CommandTimeoutMs);
        Assert.True(ok, "Only 'valid' should appear in input queue (blank/whitespace lines filtered)");

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
        writer.WriteLine("__SENTINEL__");

        SpinUntil(() => input.TryDequeueCommand(out var l) && l == "__SENTINEL__", CommandTimeoutMs);

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
    public async Task OutputChannel_ConcurrentPublish_AllDelivered()
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
            }, TestContext.Current.CancellationToken);
        }

        await Task.WhenAll(tasks);

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
        var ok1 = SpinUntil(() => input.TryDequeueCommand(out var l) && l == "from_first", CommandTimeoutMs);
        Assert.True(ok1, "'from_first' should arrive from first client");

        // Try to connect second client — should be rejected
        try
        {
            using var client2 = new TcpClient();
            client2.Connect(IPAddress.Loopback, port);
            using var writer2 = new StreamWriter(client2.GetStream()) { AutoFlush = true };
            writer2.WriteLine("from_second");
        }
        catch (IOException)
        {
            // Expected: server closed the connection
        }

        // client1 should still work
        writer1.WriteLine("still_works");
        var ok2 = SpinUntil(() => input.TryDequeueCommand(out var l) && l == "still_works", CommandTimeoutMs);
        Assert.True(ok2, "'still_works' should arrive - client1 still functional after second rejected");

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

        using var client2 = ConnectWithRetry(port, CommandTimeoutMs);
        using var writer = new StreamWriter(client2.GetStream()) { AutoFlush = true };

        writer.WriteLine("after_reconnect");

        var ok = SpinUntil(() => input.TryDequeueCommand(out var l) && l == "after_reconnect", CommandTimeoutMs);
        Assert.True(ok, "'after_reconnect' should arrive after disconnect/reconnect");

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

        // Second client (should be accepted)
        using (var client2 = ConnectWithRetry(port, CommandTimeoutMs))
        {
        }

        // Third client (should also be accepted)
        using var client3 = ConnectWithRetry(port, CommandTimeoutMs);
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

        // New connection should work
        using var client2 = ConnectWithRetry(port, CommandTimeoutMs);
        using var writer = new StreamWriter(client2.GetStream()) { AutoFlush = true };
        writer.WriteLine("after_immediate");

        SpinUntil(() => input.TryDequeueCommand(out var l) && l == "after_immediate", CommandTimeoutMs);

        server.Dispose();
    }

    [Fact]
    public void MidSession_ClientHardDisconnect_ServerRecovers()
    {
        var (server, (input, _)) = CreateStartedServer();
        var port = server.ActualPort;

        var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        writer.WriteLine("in_session");
        var ok1 = SpinUntil(() => input.TryDequeueCommand(out var l) && l == "in_session", CommandTimeoutMs);
        Assert.True(ok1, "'in_session' should arrive from initial client");

        client.Client.Dispose();

        using var client2 = ConnectWithRetry(port, CommandTimeoutMs);
        using var writer2 = new StreamWriter(client2.GetStream()) { AutoFlush = true };
        writer2.WriteLine("after_hard_disconnect");

        var ok2 = SpinUntil(() => input.TryDequeueCommand(out var l) && l == "after_hard_disconnect", CommandTimeoutMs);
        Assert.True(ok2, "'after_hard_disconnect' should arrive after hard disconnect recovery");

        server.Dispose();
    }

    [Fact]
    public void MidSession_ClientAbort_NextConnectionAccepted()
    {
        var (server, (input, _)) = CreateStartedServer();
        var port = server.ActualPort;

        using (var client = new TcpClient())
        {
            client.Connect(IPAddress.Loopback, port);
            using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };
            writer.WriteLine("before_abort");
            var ok1 = SpinUntil(() => input.TryDequeueCommand(out var l) && l == "before_abort", CommandTimeoutMs);
            Assert.True(ok1, "'before_abort' should arrive from initial client");
        }

        using var client2 = ConnectWithRetry(port, CommandTimeoutMs);
        using var writer2 = new StreamWriter(client2.GetStream()) { AutoFlush = true };
        writer2.WriteLine("after_abort");

        var ok2 = SpinUntil(() => input.TryDequeueCommand(out var l) && l == "after_abort", CommandTimeoutMs);
        Assert.True(ok2, "'after_abort' should arrive after abort recovery");

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
                Thread.Yield();
            }

            pubDone.Set();
        }, TestContext.Current.CancellationToken);

        // Meanwhile, send commands from client
        for (var i = 0; i < 20; i++)
            writer.WriteLine($"cmd_{i}");

        // Wait for publisher to finish
        Assert.True(pubDone.Wait(CommandTimeoutMs, TestContext.Current.CancellationToken), "Publisher should complete");

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

    // ── M-1 regression: connect-time flush vs concurrent publish ───────

    [Fact]
    public async Task PendingFlushDuringConcurrentPublish_DeliversIntactLines()
    {
        var (server, (_, output)) = CreateStartedServer();
        var port = server.ActualPort;

        // Backlog produced before any client connects is buffered and flushed
        // when the connection is established.
        const int backlog = 50;
        const int burst = 200;
        for (var i = 0; i < backlog; i++)
            output.Publish($"pre_{i}");

        // A bounded burst from another thread races the connect-time flush. The
        // flush and the publish path must both hold the writer lock; otherwise
        // lines from the two threads can interleave and corrupt the stream.
        var publisher = Task.Run(() =>
        {
            for (var i = 0; i < burst; i++)
                output.Publish($"live_{i}");
        }, TestContext.Current.CancellationToken);

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());

        var pre = new HashSet<string>();
        var live = new HashSet<string>();
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < OutputTimeoutMs && pre.Count + live.Count < backlog + burst)
        {
            var line = ReadLineWithTimeout(reader, OutputTimeoutMs);
            if (line is null)
                break;

            // Every delivered line must be an intact, uncorrupted token.
            Assert.Matches(@"^(pre|live)_\d+$", line);
            if (line[0] == 'p')
                pre.Add(line);
            else
                live.Add(line);
        }

        await publisher.WaitAsync(TimeSpan.FromMilliseconds(CommandTimeoutMs), TestContext.Current.CancellationToken);

        Assert.Equal(backlog, pre.Count);
        Assert.Equal(burst, live.Count);

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

        // Send command, publish response, read it immediately
        writer.WriteLine("cmd1");
        SpinUntil(() => input.TryDequeueCommand(out var l) && l == "cmd1", CommandTimeoutMs);
        output.Publish("response1");

        var response1 = ReadLineWithTimeout(reader, OutputTimeoutMs);
        Assert.NotNull(response1);
        Assert.Equal("response1", response1);

        writer.WriteLine("cmd2");
        SpinUntil(() => input.TryDequeueCommand(out var l) && l == "cmd2", CommandTimeoutMs);
        output.Publish("response2");

        var response2 = ReadLineWithTimeout(reader, OutputTimeoutMs);
        Assert.NotNull(response2);
        Assert.Equal("response2", response2);

        server.Dispose();
    }

    // ── Agent workflow integration ───────────────────────────────────────

    [Fact]
    public async Task AgentLoop_OutputArrivesDuringReadWait()
    {
        var (server, (input, output)) = CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        writer.WriteLine("help");
        SpinUntil(() => input.TryDequeueCommand(out var l) && l == "help", CommandTimeoutMs);

        var readerBlocked = new ManualResetEventSlim(false);
        var readTask = Task.Run(() =>
        {
            readerBlocked.Set();
            return ReadLineWithTimeout(reader, OutputTimeoutMs);
        }, TestContext.Current.CancellationToken);

        Assert.True(readerBlocked.Wait(CommandTimeoutMs, TestContext.Current.CancellationToken));
        Thread.Yield();
        Thread.Sleep(0);

        output.Publish("command result here");

        var response = await readTask;
        Assert.NotNull(response);
        Assert.Equal("command result here", response);

        server.Dispose();
    }

    [Fact]
    public void AgentLoop_SendRead_SendRead_NoTriggerNeeded()
    {
        var (server, (input, output)) = CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        for (var round = 1; round <= 5; round++)
        {
            writer.WriteLine($"cmd_{round}");
            SpinUntil(() => input.TryDequeueCommand(out var l) && l == $"cmd_{round}", CommandTimeoutMs);

            output.Publish($"result_{round}");

            var response = ReadLineWithTimeout(reader, OutputTimeoutMs);
            Assert.NotNull(response);
            Assert.Equal($"result_{round}", response);
        }

        server.Dispose();
    }

    [Fact]
    public void AgentLoop_MultipleOutputLines_PerCommand()
    {
        var (server, (input, output)) = CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        writer.WriteLine("list");
        SpinUntil(() => input.TryDequeueCommand(out var l) && l == "list", CommandTimeoutMs);

        output.Publish("entity_1");
        output.Publish("entity_2");
        output.Publish("entity_3");

        var r1 = ReadLineWithTimeout(reader, OutputTimeoutMs);
        var r2 = ReadLineWithTimeout(reader, OutputTimeoutMs);
        var r3 = ReadLineWithTimeout(reader, OutputTimeoutMs);
        Assert.Equal("entity_1", r1);
        Assert.Equal("entity_2", r2);
        Assert.Equal("entity_3", r3);

        server.Dispose();
    }

    [Fact]
    public void AgentLoop_OutputBeforeConnect_DeliveredOnConnect()
    {
        var (server, (_, output)) = CreateStartedServer();
        var port = server.ActualPort;

        // Game produces output before any agent connects
        output.Publish("startup log 1");
        output.Publish("startup log 2");

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());

        var r1 = ReadLineWithTimeout(reader, OutputTimeoutMs);
        var r2 = ReadLineWithTimeout(reader, OutputTimeoutMs);
        Assert.Equal("startup log 1", r1);
        Assert.Equal("startup log 2", r2);

        server.Dispose();
    }

    [Fact]
    public void AgentLoop_Disconnect_Reconnect_FullFlow()
    {
        var (server, (input, output)) = CreateStartedServer();
        var port = server.ActualPort;

        // Session 1: connect, send command, read output, disconnect
        using (var client = new TcpClient())
        {
            client.Connect(IPAddress.Loopback, port);
            using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };
            using var reader = new StreamReader(client.GetStream());

            writer.WriteLine("session1_cmd");
            SpinUntil(() => input.TryDequeueCommand(out var l) && l == "session1_cmd", CommandTimeoutMs);
            output.Publish("session1_result");

            var response = ReadLineWithTimeout(reader, OutputTimeoutMs);
            Assert.Equal("session1_result", response);
        }

        // Session 2: reconnect, send new command, read output
        using (var client = ConnectWithRetry(port, CommandTimeoutMs))
        {
            using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };
            using var reader = new StreamReader(client.GetStream());

            writer.WriteLine("session2_cmd");
            SpinUntil(() => input.TryDequeueCommand(out var l) && l == "session2_cmd", CommandTimeoutMs);
            output.Publish("session2_result");

            var response = ReadLineWithTimeout(reader, OutputTimeoutMs);
            Assert.Equal("session2_result", response);
        }

        server.Dispose();
    }

    [Fact]
    public async Task AgentLoop_ConcurrentPublish_DuringReadWait()
    {
        var (server, (_, output)) = CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());

        var readerAboutToBlock = new ManualResetEventSlim(false);
        var readTask = Task.Run(() =>
        {
            var lines = new List<string>();
            string? line;
            readerAboutToBlock.Set();
            while ((line = ReadLineWithTimeout(reader, OutputTimeoutMs)) is not null)
                lines.Add(line);
            return lines;
        }, TestContext.Current.CancellationToken);

        Assert.True(readerAboutToBlock.Wait(CommandTimeoutMs, TestContext.Current.CancellationToken));
        Thread.Yield();
        Thread.Sleep(0);
        output.Publish("log_a");
        output.Publish("log_b");
        output.Publish("log_c");

        var result = await readTask;
        Assert.Contains("log_a", result);
        Assert.Contains("log_b", result);
        Assert.Contains("log_c", result);

        server.Dispose();
    }

    [Fact]
    public void AgentLoop_Stress_50Rounds_NoDeadlock()
    {
        var (server, (input, output)) = CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        for (var round = 0; round < 50; round++)
        {
            writer.WriteLine($"stress_{round}");
            SpinUntil(() => input.TryDequeueCommand(out var l) && l == $"stress_{round}", CommandTimeoutMs);
            output.Publish($"pong_{round}");

            var response = ReadLineWithTimeout(reader, OutputTimeoutMs);
            Assert.NotNull(response);
            Assert.Equal($"pong_{round}", response);
        }

        server.Dispose();
    }

    [Fact]
    public async Task AgentLoop_Dispose_WhileAgentWaitingForOutput()
    {
        var (server, _) = CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);

        var readerAboutToBlock = new ManualResetEventSlim(false);
        var readTask = Task.Run(() =>
        {
            using var reader = new StreamReader(client.GetStream());
            readerAboutToBlock.Set();
            reader.ReadLine();
        }, TestContext.Current.CancellationToken);

        Assert.True(readerAboutToBlock.Wait(CommandTimeoutMs, TestContext.Current.CancellationToken));
        Thread.Yield();
        Thread.Sleep(0);
        server.Dispose();

        await readTask.WaitAsync(TimeSpan.FromMilliseconds(3000), TestContext.Current.CancellationToken);
    }

    // ── Constructor guards ─────────────────────────────────────────────

    [Fact]
    public void Constructor_NullInput_Throws()
    {
        var output = new ConsoleOutputChannel();

        Assert.Throws<ArgumentNullException>(() =>
            new ConsoleBridgeServer(null!, output));
    }

    [Fact]
    public void Constructor_NullOutput_Throws()
    {
        var input = new ConsoleInputBuffer();

        Assert.Throws<ArgumentNullException>(() =>
            new ConsoleBridgeServer(input, null!));
    }

    [Fact]
    public void Constructor_DefaultOptions_HasExpectedPort()
    {
        var input = new ConsoleInputBuffer();
        var output = new ConsoleOutputChannel();

        var server = new ConsoleBridgeServer(input, output);
        server.Start();
        Assert.True(server.ActualPort > 0);
        server.Dispose();
    }

    [Fact]
    public void Constructor_CustomPort_StoredInOptions()
    {
        var input = new ConsoleInputBuffer();
        var output = new ConsoleOutputChannel();
        var options = new ConsoleBridgeOptions { Port = 9876 };

        var server = new ConsoleBridgeServer(input, output, options);
        server.Start();
        Assert.Equal(9876, server.ActualPort);
        server.Dispose();
    }

    // ── Start idempotency ─────────────────────────────────────────────

    [Fact]
    public void Start_CalledTwice_DoesNotThrow()
    {
        var input = new ConsoleInputBuffer();
        var output = new ConsoleOutputChannel();
        var server = new ConsoleBridgeServer(input, output);
        server.Start();
        server.Start(); // second call should be a no-op
        server.Dispose();
    }

    [Fact]
    public void Start_CalledTwice_PortRemainsSame()
    {
        var input = new ConsoleInputBuffer();
        var output = new ConsoleOutputChannel();
        var server = new ConsoleBridgeServer(input, output);
        server.Start();
        var port1 = server.ActualPort;
        server.Start(); // second call should be a no-op
        var port2 = server.ActualPort;
        Assert.Equal(port1, port2);
        server.Dispose();
    }

    [Fact]
    public void Dispose_BeforeStart_DoesNotThrow()
    {
        var input = new ConsoleInputBuffer();
        var output = new ConsoleOutputChannel();
        var server = new ConsoleBridgeServer(input, output);

        var ex = Record.Exception(() => server.Dispose());

        Assert.Null(ex);
    }

    // ── Helpers ──

    private static (ConsoleBridgeServer server, (ConsoleInputBuffer input, ConsoleOutputChannel output) queues)
        CreateStartedServer(int port = 0)
    {
        var input = new ConsoleInputBuffer();
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

    private static TcpClient ConnectWithRetry(int port, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        SocketException? lastEx = null;
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                var client = new TcpClient();
                client.Connect(IPAddress.Loopback, port);
                return client;
            }
            catch (SocketException ex)
            {
                lastEx = ex;
                Thread.Sleep(ConnectRetryIntervalMs);
            }
        }
        throw new TimeoutException(
            $"Could not connect to port {port} within {timeoutMs}ms", lastEx);
    }

    private static void AssertConnectionRefused(int port, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                using var client = new TcpClient();
                client.Connect(IPAddress.Loopback, port);
                Assert.Fail("Connection should have been refused after server dispose");
            }
            catch (SocketException)
            {
                // Expected — server is not accepting connections
            }
            Thread.Sleep(ConnectRetryIntervalMs);
        }
    }
}
