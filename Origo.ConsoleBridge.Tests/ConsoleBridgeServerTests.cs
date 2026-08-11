using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Origo.Core.Runtime.Console;
using Origo.TestSupport;
using Xunit;

namespace Origo.ConsoleBridge.Tests;

public class ConsoleBridgeServerTests
{
    [Fact]
    public void Concurrent_PublishWhileReading_NoDeadlock()
    {
        var (server, (input, output)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        var pubDone = new ManualResetEventSlim(false);
        var pubTask = Task.Run(() =>
        {
            for (var i = 0; i < 50; i++)
                output.Publish($"pub_{i}");

            pubDone.Set();
        }, TestContext.Current.CancellationToken);

        for (var i = 0; i < 20; i++)
            writer.WriteLine($"cmd_{i}");

        Assert.True(pubDone.Wait(ConsoleBridgeTestInfrastructure.CommandTimeoutMs, TestContext.Current.CancellationToken), "Publisher should complete");

        ConsoleBridgeTestInfrastructure.SpinUntil(() =>
        {
            var count = 0;
            while (input.TryDequeueCommand(out _))
                count++;
            return count >= 20;
        }, ConsoleBridgeTestInfrastructure.CommandTimeoutMs);

        server.Dispose();
    }

    [Fact]
    public async Task PendingFlushDuringConcurrentPublish_DeliversIntactLines()
    {
        var (server, (_, output)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        const int backlog = 50;
        const int burst = 200;
        for (var i = 0; i < backlog; i++)
            output.Publish($"pre_{i}");

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
        while (watch.ElapsedMilliseconds < ConsoleBridgeTestInfrastructure.OutputTimeoutMs && pre.Count + live.Count < backlog + burst)
        {
            var line = ConsoleBridgeTestInfrastructure.ReadLineWithTimeout(reader, ConsoleBridgeTestInfrastructure.OutputTimeoutMs);
            if (line is null)
                break;

            Assert.Matches(@"^(pre|live)_\d+$", line);
            if (line[0] == 'p')
                pre.Add(line);
            else
                live.Add(line);
        }

        await publisher.WaitAsync(TimeSpan.FromMilliseconds(ConsoleBridgeTestInfrastructure.CommandTimeoutMs), TestContext.Current.CancellationToken);

        Assert.Equal(backlog, pre.Count);
        Assert.Equal(burst, live.Count);

        server.Dispose();
    }

    [Fact]
    public void FullRoundTrip_CommandResponsePattern()
    {
        var (server, (input, output)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        writer.WriteLine("cmd1");
        ConsoleBridgeTestInfrastructure.SpinUntil(() => input.TryDequeueCommand(out var l) && l == "cmd1", ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
        output.Publish("response1");

        var response1 = ConsoleBridgeTestInfrastructure.ReadLineWithTimeout(reader, ConsoleBridgeTestInfrastructure.OutputTimeoutMs);
        Assert.NotNull(response1);
        Assert.Equal("response1", response1);

        writer.WriteLine("cmd2");
        ConsoleBridgeTestInfrastructure.SpinUntil(() => input.TryDequeueCommand(out var l) && l == "cmd2", ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
        output.Publish("response2");

        var response2 = ConsoleBridgeTestInfrastructure.ReadLineWithTimeout(reader, ConsoleBridgeTestInfrastructure.OutputTimeoutMs);
        Assert.NotNull(response2);
        Assert.Equal("response2", response2);

        server.Dispose();
    }

    [Fact]
    public async Task AgentLoop_OutputArrivesDuringReadWait()
    {
        var (server, (input, output)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        writer.WriteLine("help");
        ConsoleBridgeTestInfrastructure.SpinUntil(() => input.TryDequeueCommand(out var l) && l == "help", ConsoleBridgeTestInfrastructure.CommandTimeoutMs);

        var readerBlocked = new ManualResetEventSlim(false);
        var readTask = Task.Run(() =>
        {
            readerBlocked.Set();
            return ConsoleBridgeTestInfrastructure.ReadLineWithTimeout(reader, ConsoleBridgeTestInfrastructure.OutputTimeoutMs);
        }, TestContext.Current.CancellationToken);

        Assert.True(readerBlocked.Wait(ConsoleBridgeTestInfrastructure.CommandTimeoutMs, TestContext.Current.CancellationToken));

        output.Publish("command result here");

        var response = await readTask;
        Assert.NotNull(response);
        Assert.Equal("command result here", response);

        server.Dispose();
    }

    [Fact]
    public void AgentLoop_SendRead_SendRead_NoTriggerNeeded()
    {
        var (server, (input, output)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        for (var round = 1; round <= 5; round++)
        {
            writer.WriteLine($"cmd_{round}");
            ConsoleBridgeTestInfrastructure.SpinUntil(() => input.TryDequeueCommand(out var l) && l == $"cmd_{round}", ConsoleBridgeTestInfrastructure.CommandTimeoutMs);

            output.Publish($"result_{round}");

            var response = ConsoleBridgeTestInfrastructure.ReadLineWithTimeout(reader, ConsoleBridgeTestInfrastructure.OutputTimeoutMs);
            Assert.NotNull(response);
            Assert.Equal($"result_{round}", response);
        }

        server.Dispose();
    }

    [Fact]
    public void AgentLoop_MultipleOutputLines_PerCommand()
    {
        var (server, (input, output)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        writer.WriteLine("list");
        ConsoleBridgeTestInfrastructure.SpinUntil(() => input.TryDequeueCommand(out var l) && l == "list", ConsoleBridgeTestInfrastructure.CommandTimeoutMs);

        output.Publish("entity_1");
        output.Publish("entity_2");
        output.Publish("entity_3");

        var r1 = ConsoleBridgeTestInfrastructure.ReadLineWithTimeout(reader, ConsoleBridgeTestInfrastructure.OutputTimeoutMs);
        var r2 = ConsoleBridgeTestInfrastructure.ReadLineWithTimeout(reader, ConsoleBridgeTestInfrastructure.OutputTimeoutMs);
        var r3 = ConsoleBridgeTestInfrastructure.ReadLineWithTimeout(reader, ConsoleBridgeTestInfrastructure.OutputTimeoutMs);
        Assert.Equal("entity_1", r1);
        Assert.Equal("entity_2", r2);
        Assert.Equal("entity_3", r3);

        server.Dispose();
    }

    [Fact]
    public void AgentLoop_OutputBeforeConnect_DeliveredOnConnect()
    {
        var (server, (_, output)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        output.Publish("startup log 1");
        output.Publish("startup log 2");

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());

        var r1 = ConsoleBridgeTestInfrastructure.ReadLineWithTimeout(reader, ConsoleBridgeTestInfrastructure.OutputTimeoutMs);
        var r2 = ConsoleBridgeTestInfrastructure.ReadLineWithTimeout(reader, ConsoleBridgeTestInfrastructure.OutputTimeoutMs);
        Assert.Equal("startup log 1", r1);
        Assert.Equal("startup log 2", r2);

        server.Dispose();
    }

    [Fact]
    public void AgentLoop_Disconnect_Reconnect_FullFlow()
    {
        var (server, (input, output)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using (var client = new TcpClient())
        {
            client.Connect(IPAddress.Loopback, port);
            using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };
            using var reader = new StreamReader(client.GetStream());

            writer.WriteLine("session1_cmd");
            ConsoleBridgeTestInfrastructure.SpinUntil(() => input.TryDequeueCommand(out var l) && l == "session1_cmd", ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
            output.Publish("session1_result");

            var response = ConsoleBridgeTestInfrastructure.ReadLineWithTimeout(reader, ConsoleBridgeTestInfrastructure.OutputTimeoutMs);
            Assert.Equal("session1_result", response);
        }

        using (var client = ConsoleBridgeTestInfrastructure.Connect(port, ConsoleBridgeTestInfrastructure.CommandTimeoutMs))
        {
            using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };
            using var reader = new StreamReader(client.GetStream());

            writer.WriteLine("session2_cmd");
            ConsoleBridgeTestInfrastructure.SpinUntil(() => input.TryDequeueCommand(out var l) && l == "session2_cmd", ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
            output.Publish("session2_result");

            var response = ConsoleBridgeTestInfrastructure.ReadLineWithTimeout(reader, ConsoleBridgeTestInfrastructure.OutputTimeoutMs);
            Assert.Equal("session2_result", response);
        }

        server.Dispose();
    }

    [Fact]
    public async Task AgentLoop_ConcurrentPublish_DuringReadWait()
    {
        var (server, (_, output)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
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
            while ((line = ConsoleBridgeTestInfrastructure.ReadLineWithTimeout(reader, ConsoleBridgeTestInfrastructure.OutputTimeoutMs)) is not null)
                lines.Add(line);
            return lines;
        }, TestContext.Current.CancellationToken);

        Assert.True(readerAboutToBlock.Wait(ConsoleBridgeTestInfrastructure.CommandTimeoutMs, TestContext.Current.CancellationToken));
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
        var (server, (input, output)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var reader = new StreamReader(client.GetStream());
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        for (var round = 0; round < 50; round++)
        {
            writer.WriteLine($"stress_{round}");
            ConsoleBridgeTestInfrastructure.SpinUntil(() => input.TryDequeueCommand(out var l) && l == $"stress_{round}", ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
            output.Publish($"pong_{round}");

            var response = ConsoleBridgeTestInfrastructure.ReadLineWithTimeout(reader, ConsoleBridgeTestInfrastructure.OutputTimeoutMs);
            Assert.NotNull(response);
            Assert.Equal($"pong_{round}", response);
        }

        server.Dispose();
    }

    [Fact]
    public async Task AgentLoop_Dispose_WhileAgentWaitingForOutput()
    {
        var (server, _) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);

        var readerAboutToBlock = new ManualResetEventSlim(false);
        var readTask = Task.Run(() =>
        {
            using var reader = new StreamReader(client.GetStream());
            readerAboutToBlock.Set();
            try
            {
                // Disposing the server unblocks the blocked read. Both a graceful
                // close (ReadLine returns null) and an abrupt reset
                // (IOException: connection reset by peer) satisfy the contract that
                // the agent's read does not hang. Only a hang — caught below by the
                // WaitAsync timeout — is a failure.
                reader.ReadLine();
            }
            catch (IOException)
            {
            }
        }, TestContext.Current.CancellationToken);

        Assert.True(readerAboutToBlock.Wait(ConsoleBridgeTestInfrastructure.CommandTimeoutMs, TestContext.Current.CancellationToken));
        server.Dispose();

        await readTask.WaitAsync(TimeSpan.FromMilliseconds(3000), TestContext.Current.CancellationToken);
    }

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
        // Port 0 selects an ephemeral port; the ActualPort reflects the
        // configured value while avoiding collisions with other processes.
        var options = new ConsoleBridgeOptions { Port = 0 };

        var server = new ConsoleBridgeServer(input, output, options);
        server.Start();
        Assert.True(server.ActualPort > 0);
        server.Dispose();
    }

    [Fact]
    public void Start_CalledTwice_DoesNotThrow()
    {
        var input = new ConsoleInputBuffer();
        var output = new ConsoleOutputChannel();
        var server = new ConsoleBridgeServer(input, output);
        server.Start();
        var ex = Record.Exception(() => server.Start());
        Assert.Null(ex);
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
        server.Start();
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

    [Fact]
    public async Task OutputToNonReadingClient_DoesNotBlockPublish()
    {
        var (server, (_, output)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        try
        {
            using var client = new TcpClient { ReceiveBufferSize = 1024 };
            client.Connect(IPAddress.Loopback, server.ActualPort);
            // Never read from the stream: a client that stops consuming data
            // fills the server's send buffer. Publishing must not block the
            // caller (the game frame thread) indefinitely — the dead
            // connection is detached after a send timeout and the remaining
            // lines are buffered for the next connection.
            var longLine = new string('x', 60);
            var publish = Task.Run(() =>
            {
                for (var i = 0; i < 200_000; i++)
                    output.Publish($"line {i}: {longLine}");
            }, TestContext.Current.CancellationToken);

            var completed = await Task.WhenAny(publish,
                Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.Same(publish, completed);
        }
        finally
        {
            server.Dispose();
        }
    }

    [Fact]
    public void Options_OutputSendTimeoutMs_DefaultsAndCustom()
    {
        Assert.Equal(ConsoleBridgeOptions.DefaultOutputSendTimeoutMs,
            new ConsoleBridgeOptions().OutputSendTimeoutMs);
        Assert.Equal(250, new ConsoleBridgeOptions { OutputSendTimeoutMs = 250 }.OutputSendTimeoutMs);
    }

    [Fact]
    public void Start_NonPositiveOutputSendTimeout_Throws()
    {
        var input = new ConsoleInputBuffer();
        var output = new ConsoleOutputChannel();
        var server = new ConsoleBridgeServer(input, output,
            new ConsoleBridgeOptions { OutputSendTimeoutMs = 0 });

        // Socket.SendTimeout treats 0 as "infinite": accepting it would
        // silently reintroduce the frame-thread stall the timeout exists to
        // prevent.
        Assert.Throws<ArgumentOutOfRangeException>(() => server.Start());
    }

    [Fact]
    public async Task BacklogReplayToNonReadingClient_DetachesConnectionAndKeepsBacklog()
    {
        var input = new ConsoleInputBuffer();
        var output = new ConsoleOutputChannel();
        var logger = new TestLogger();
        var server = new ConsoleBridgeServer(input, output, new ConsoleBridgeOptions { Port = 0 }, logger);
        server.Start();
        try
        {
            // Fill the pending-output backlog while no client is connected
            // (8MB total — well beyond the kernel socket buffers).
            var longLine = new string('x', 8000);
            for (var i = 0; i < 1000; i++)
                output.Publish($"line {i}: {longLine}");

            // Connect without reading: the backlog replay stalls until the
            // send timeout detaches the connection; the remaining backlog
            // stays queued for a later connection instead of blocking the
            // caller (and the connection is closed).
            using var client = new TcpClient { ReceiveBufferSize = 1024 };
            client.Connect(IPAddress.Loopback, server.ActualPort);

            await Task.Delay(500, TestContext.Current.CancellationToken);
            output.Publish("after-detach");

            Assert.True(ConsoleBridgeTestInfrastructure.SpinUntil(
                () => logger.Warnings.Any(w => w.Contains("flush failed")),
                ConsoleBridgeTestInfrastructure.OutputTimeoutMs),
                "the stalled replay must detach the connection");
        }
        finally
        {
            server.Dispose();
        }
    }

    [Fact]
    public async Task DeadNonReadingClient_IsClosed_NextClientConnectsAndReplaysBacklog()
    {
        // A client that stops reading must be actually closed once the send
        // timeout detaches it — otherwise it occupies the single connection
        // slot forever and the buffered lines can never reach a "next
        // connection" (the documented replay mechanism).
        var input = new ConsoleInputBuffer();
        var output = new ConsoleOutputChannel();
        var logger = new TestLogger();
        var server = new ConsoleBridgeServer(input, output,
            new ConsoleBridgeOptions { Port = 0, OutputSendTimeoutMs = 50 }, logger);
        server.Start();
        try
        {
            var longLine = new string('x', 8000);
            for (var i = 0; i < 1000; i++)
                output.Publish($"line {i}: {longLine}");

            using var deadClient = new TcpClient { ReceiveBufferSize = 1024 };
            deadClient.Connect(IPAddress.Loopback, server.ActualPort);

            Assert.True(ConsoleBridgeTestInfrastructure.SpinUntil(
                () => IsRemoteEndClosed(deadClient),
                ConsoleBridgeTestInfrastructure.OutputTimeoutMs),
                "the detached dead client must be closed by the server");

            // With the slot freed, a new client connects and receives the
            // buffered backlog: the documented replay-on-next-connection.
            using var nextClient = ConsoleBridgeTestInfrastructure.Connect(
                server.ActualPort, ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
            using var reader = new StreamReader(nextClient.GetStream());
            Assert.NotNull(ConsoleBridgeTestInfrastructure.ReadLineWithTimeout(
                reader, ConsoleBridgeTestInfrastructure.OutputTimeoutMs));
        }
        finally
        {
            server.Dispose();
        }
    }

    [Fact]
    public async Task BacklogReplayToSlowClient_AbortsAtBudget_RemainingLinesReplayOnNextConnection()
    {
        // A slow-but-reading client drains the backlog below the per-line
        // send timeout, so the replay would otherwise hold the writer lock
        // for the full backlog duration and stall the game frame thread for
        // seconds. The flush runs on a bounded time budget: the remaining
        // lines stay buffered and replay on the next connection.
        var input = new ConsoleInputBuffer();
        var output = new ConsoleOutputChannel();
        var logger = new TestLogger();
        // A very large send timeout keeps per-line writes from ever timing
        // out (a timed-out line may already be half-delivered and would
        // replay duplicated on the next connection); with the flush budget
        // capped at one second, the budget abort is then the only mechanism
        // that ends the flush.
        var server = new ConsoleBridgeServer(input, output,
            new ConsoleBridgeOptions { Port = 0, OutputSendTimeoutMs = 10_000 }, logger);
        server.Start();
        try
        {
            // 8MB of backlog: the socket send buffer absorbs the first chunk
            // instantly; the rest blocks on every line (the client drains
            // slowly but continuously), so the time budget must end the
            // replay far below the per-line send timeout.
            const int lineCount = 1000;
            var longLine = new string('x', 8000);
            for (var i = 0; i < lineCount; i++)
                output.Publish($"line {i}: {longLine}");

            using var slowClient = ConsoleBridgeTestInfrastructure.Connect(
                server.ActualPort, ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
            var firstClientLineCount = 0;
            var budgetAbortSeen = false;
            using (var reader = new StreamReader(slowClient.GetStream()))
            {
                // Drain slowly and continuously (5ms per line, polling the
                // socket with a short timeout when the receive buffer is
                // empty): every replay write then blocks a few milliseconds
                // instead of timing out, so the time budget is the only
                // mechanism that ends the flush. Reading stops once the
                // budget abort has been observed and the buffer is drained
                // (EOF means the server detached the connection, which fails
                // the budget-abort assertion below).
                while (true)
                {
                    string? line;
                    try
                    {
                        reader.BaseStream.ReadTimeout = 100;
                        line = reader.ReadLine();
                    }
                    catch (IOException)
                    {
                        // Receive buffer empty: if the server's budget abort
                        // has been observed (either on a previous line or
                        // just now — the abort log races the client's last
                        // read), the flush is over; otherwise keep polling.
                        if (budgetAbortSeen
                            || logger.SnapshotWarnings().Any(w => w.Contains("time budget", StringComparison.Ordinal)))
                            break;
                        continue;
                    }

                    if (line is null)
                        break;

                    firstClientLineCount++;
                    if (logger.SnapshotWarnings().Any(w => w.Contains("time budget", StringComparison.Ordinal)))
                        budgetAbortSeen = true;
                    await Task.Delay(5, TestContext.Current.CancellationToken);
                    if (firstClientLineCount >= lineCount)
                        break;
                }
            }

            // The replay aborted at its time budget: not everything was sent.
            Assert.True(firstClientLineCount < lineCount,
                "the backlog replay must abort at its time budget for a slow reader");
            Assert.True(logger.SnapshotWarnings().Any(w => w.Contains("time budget", StringComparison.Ordinal)),
                "the replay must end via the budget abort, not a write failure");

            // Reconnect: the remaining lines replay on the next connection.
            using var nextClient = ConsoleBridgeTestInfrastructure.Connect(
                server.ActualPort, ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
            var replayedLineCount = 0;
            using (var reader = new StreamReader(nextClient.GetStream()))
            {
                while (ConsoleBridgeTestInfrastructure.ReadLineWithTimeout(
                           reader, ConsoleBridgeTestInfrastructure.OutputTimeoutMs) is not null)
                {
                    replayedLineCount++;
                }
            }

            Assert.True(replayedLineCount > 100,
                "at least the tail of the backlog must survive for the next connection");
            Assert.Equal(lineCount, firstClientLineCount + replayedLineCount);
        }
        finally
        {
            server.Dispose();
        }
    }

    private static bool IsRemoteEndClosed(TcpClient client)
    {
        // Buffered payload bytes arrive before the close notification, so a
        // single ReadByte only proves "not EOF yet". Drain until EOF: the
        // server has closed the connection exactly when the stream ends.
        var stream = client.GetStream();
        var oldTimeout = stream.ReadTimeout;
        try
        {
            stream.ReadTimeout = 800;
            while (stream.ReadByte() != -1)
            {
            }

            return true;
        }
        catch (SocketException)
        {
            return true;
        }
        catch (ObjectDisposedException)
        {
            return true;
        }
        catch (IOException ex)
        {
            // A read timeout means the connection is still open; any other
            // I/O failure means the peer closed it.
            return ex.InnerException is not SocketException { SocketErrorCode: SocketError.TimedOut };
        }
        finally
        {
            stream.ReadTimeout = oldTimeout;
        }
    }
}
