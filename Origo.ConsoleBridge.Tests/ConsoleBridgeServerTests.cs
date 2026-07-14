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
            {
                output.Publish($"pub_{i}");
                Thread.Yield();
            }

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
        var options = new ConsoleBridgeOptions { Port = 9876 };

        var server = new ConsoleBridgeServer(input, output, options);
        server.Start();
        Assert.Equal(9876, server.ActualPort);
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
}
