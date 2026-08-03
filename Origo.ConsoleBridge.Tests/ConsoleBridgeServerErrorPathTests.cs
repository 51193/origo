using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Origo.Core.Abstractions.Console;
using Origo.Core.Runtime.Console;
using Origo.TestSupport;
using Xunit;

namespace Origo.ConsoleBridge.Tests;

public class ConsoleBridgeServerErrorPathTests
{
    /// <summary>
    ///     Test double: throws from Enqueue after a configured number of
    ///     successful calls, allowing tests to trigger the AcceptLoopAsync
    ///     error-handling path.
    /// </summary>
    private sealed class ThrowingInputSource : IConsoleInputSource
    {
        private readonly ConsoleInputBuffer _inner = new();

        public int ThrowAfterCount { get; set; } = int.MaxValue;

        public int EnqueueCallCount { get; private set; }

        public bool TryDequeueCommand([NotNullWhen(true)] out string? line) =>
            _inner.TryDequeueCommand(out line);

        public void Enqueue(string line)
        {
            EnqueueCallCount++;
            if (EnqueueCallCount > ThrowAfterCount)
                throw new InvalidOperationException("Simulated Enqueue failure");
            _inner.Enqueue(line);
        }

        public void Clear() =>
            _inner.Clear();
    }
    [Fact]
    public void HardClientRst_TriggersIOException_AndRecovers()
    {
        var (server, (input, _)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        writer.WriteLine("before_rst");
        Assert.True(ConsoleBridgeTestInfrastructure.SpinUntil(
            () => input.TryDequeueCommand(out var l) && l == "before_rst",
            ConsoleBridgeTestInfrastructure.CommandTimeoutMs));

        // Abruptly reset the connection: LingerState (true, 0) sends RST
        // immediately on close, which triggers IOException on the server's
        // pending ReadLineAsync.
        client.LingerState = new LingerOption(true, 0);
        client.Close();

        using var client2 = ConsoleBridgeTestInfrastructure.Connect(port,
            ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
        using var writer2 = new StreamWriter(client2.GetStream()) { AutoFlush = true };
        writer2.WriteLine("after_rst");

        Assert.True(ConsoleBridgeTestInfrastructure.SpinUntil(
            () => input.TryDequeueCommand(out var l) && l == "after_rst",
            ConsoleBridgeTestInfrastructure.CommandTimeoutMs));

        server.Dispose();
    }

    [Fact]
    public void HardSocketClose_TriggersIOException_AndRecovers()
    {
        var (server, (input, _)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        writer.WriteLine("before_socket_close");
        Assert.True(ConsoleBridgeTestInfrastructure.SpinUntil(
            () => input.TryDequeueCommand(out var l) && l == "before_socket_close",
            ConsoleBridgeTestInfrastructure.CommandTimeoutMs));

        client.Client.Close(0);

        using var client2 = ConsoleBridgeTestInfrastructure.Connect(port,
            ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
        using var writer2 = new StreamWriter(client2.GetStream()) { AutoFlush = true };
        writer2.WriteLine("after_socket_close");

        Assert.True(ConsoleBridgeTestInfrastructure.SpinUntil(
            () => input.TryDequeueCommand(out var l) && l == "after_socket_close",
            ConsoleBridgeTestInfrastructure.CommandTimeoutMs));

        server.Dispose();
    }

    [Fact]
    public void StreamShutdown_TriggersSocketException_AndRecovers()
    {
        var (server, (input, _)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        writer.WriteLine("before_shutdown");
        Assert.True(ConsoleBridgeTestInfrastructure.SpinUntil(
            () => input.TryDequeueCommand(out var l) && l == "before_shutdown",
            ConsoleBridgeTestInfrastructure.CommandTimeoutMs));

        client.Client.Shutdown(SocketShutdown.Both);
        client.Client.Close();

        using var client2 = ConsoleBridgeTestInfrastructure.Connect(port,
            ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
        using var writer2 = new StreamWriter(client2.GetStream()) { AutoFlush = true };
        writer2.WriteLine("after_shutdown");

        Assert.True(ConsoleBridgeTestInfrastructure.SpinUntil(
            () => input.TryDequeueCommand(out var l) && l == "after_shutdown",
            ConsoleBridgeTestInfrastructure.CommandTimeoutMs));

        server.Dispose();
    }

    [Fact]
    public void PendingFlush_BrokenClient_ServerRecovers()
    {
        var input = new ConsoleInputBuffer();
        var output = new ConsoleOutputChannel();
        var server = new ConsoleBridgeServer(input, output);
        server.Start();
        var port = server.ActualPort;

        // Publish pending output while no client is connected so that
        // when a client connects and immediately disconnects, the
        // HandleConnectionAsync write flush hits a dead stream and
        // exercises the general Exception handler.
        output.Publish("pending_before_connect");

        // Connect and immediately disconnect — the server accepts,
        // enters HandleConnectionAsync, attempts to flush pending
        // output onto a now-closed stream, catches the exception,
        // and continues the accept loop.
        using (var client = new TcpClient())
        {
            client.Connect(IPAddress.Loopback, port);
        }

        using var client2 = ConsoleBridgeTestInfrastructure.Connect(port,
            ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
        using var writer2 = new StreamWriter(client2.GetStream()) { AutoFlush = true };

        writer2.WriteLine("recovered_cmd");
        Assert.True(ConsoleBridgeTestInfrastructure.SpinUntil(
            () => input.TryDequeueCommand(out var l) && l == "recovered_cmd",
            ConsoleBridgeTestInfrastructure.CommandTimeoutMs));

        server.Dispose();
    }

    [Fact]
    public void WriteFailure_LogsWarning_AndRecovers()
    {
        var logger = new TestLogger();
        var input = new ThrowingInputSource { ThrowAfterCount = 0 };
        var output = new ConsoleOutputChannel();
        var server = new ConsoleBridgeServer(input, output, logger: logger);
        server.Start();
        var port = server.ActualPort;

        // Connect a client and send a command. The server reads it and
        // calls input.Enqueue, which throws. The exception propagates
        // out of HandleConnectionAsync and is caught by AcceptLoopAsync,
        // which logs a warning.
        using (var client = new TcpClient())
        {
            client.Connect(IPAddress.Loopback, port);
            using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };
            writer.WriteLine("trigger_exception");
        }

        // Wait for the server to handle the connection and log.
        ConsoleBridgeTestInfrastructure.SpinUntil(
            () => logger.Warnings.Count > 0,
            ConsoleBridgeTestInfrastructure.CommandTimeoutMs);

        var foundWarning = false;
        foreach (var w in logger.Warnings)
        {
            if (w.Contains(nameof(ConsoleBridgeServer)) &&
                w.Contains("Connection handler failed"))
            {
                foundWarning = true;
                break;
            }
        }

        Assert.True(foundWarning,
            "Expected a warning log from ConsoleBridgeServer about connection handler failure.");

        // Verify server still accepts new connections (recovery).
        input.ThrowAfterCount = int.MaxValue;
        using var client2 = ConsoleBridgeTestInfrastructure.Connect(port,
            ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
        using var writer2 = new StreamWriter(client2.GetStream()) { AutoFlush = true };

        writer2.WriteLine("recovered_cmd");
        Assert.True(ConsoleBridgeTestInfrastructure.SpinUntil(
            () => input.TryDequeueCommand(out var l) && l == "recovered_cmd",
            ConsoleBridgeTestInfrastructure.CommandTimeoutMs));

        server.Dispose();
    }

    // ── Accept-loop fault observability (regression: faults used to be swallowed) ──

    [Fact]
    public void Dispose_FaultedAcceptTask_LogsErrorInsteadOfSwallowing()
    {
        var logger = new TestLogger();
        var server = new ConsoleBridgeServer(new ConsoleInputBuffer(), new ConsoleOutputChannel(), logger: logger);

        var acceptTaskField = typeof(ConsoleBridgeServer)
            .GetField("_acceptTask", BindingFlags.NonPublic | BindingFlags.Instance)!;
        acceptTaskField.SetValue(server, Task.FromException(new SocketException(1234)));

        server.Dispose();

        Assert.Contains(logger.Errors, e => e.Contains("Accept loop faulted"));
    }

    [Fact]
    public void Dispose_AcceptTaskStillRunning_LogsTimeoutWarning()
    {
        var logger = new TestLogger();
        var server = new ConsoleBridgeServer(new ConsoleInputBuffer(), new ConsoleOutputChannel(), logger: logger);

        var acceptTaskField = typeof(ConsoleBridgeServer)
            .GetField("_acceptTask", BindingFlags.NonPublic | BindingFlags.Instance)!;
        acceptTaskField.SetValue(server,
            Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        server.Dispose();
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(6), "Dispose should not wait for the full accept-task lifetime.");
        Assert.Contains(logger.Warnings, e => e.Contains("Accept loop did not stop within the join timeout"));
    }

    [Fact]
    public void Start_AfterDispose_ThrowsObjectDisposed()
    {
        var server = new ConsoleBridgeServer(new ConsoleInputBuffer(), new ConsoleOutputChannel());
        server.Dispose();

        Assert.Throws<ObjectDisposedException>(() => server.Start());
    }

    [Fact]
    public void Start_Failure_RollsBackAndAllowsRetry()
    {
        // An invalid port fails during listener construction inside Start;
        // the rolled-back started flag must allow a later Start to succeed.
        var options = new ConsoleBridgeOptions { Port = -1 };
        var server = new ConsoleBridgeServer(new ConsoleInputBuffer(), new ConsoleOutputChannel(), options);

        Assert.ThrowsAny<Exception>(() => server.Start());

        options.Port = 0;
        server.Start();
        Assert.True(server.ActualPort > 0);

        server.Dispose();
    }

    [Fact]
    public async Task AcceptLoop_NonCancellationListenerError_LogsErrorAndStops()
    {
        var logger = new TestLogger();
        var server = new ConsoleBridgeServer(new ConsoleInputBuffer(), new ConsoleOutputChannel(), logger: logger);
        server.Start();

        // Stop the listener out from under the accept loop: the pending
        // AcceptTcpClientAsync fails with a non-cancellation socket error.
        var listenerField = typeof(ConsoleBridgeServer)
            .GetField("_listener", BindingFlags.NonPublic | BindingFlags.Instance)!;
        ((TcpListener)listenerField.GetValue(server)!).Stop();

        Assert.True(ConsoleBridgeTestInfrastructure.SpinUntil(
            () => logger.Errors.Any(e => e.Contains("Accept loop stopped")),
            ConsoleBridgeTestInfrastructure.CommandTimeoutMs));

        server.Dispose();
        Assert.DoesNotContain(logger.Errors, e => e.Contains("Accept loop faulted"));
    }
}
