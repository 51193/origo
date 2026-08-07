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
    public void Start_PortInUse_RollsBackListenerAndAllowsRetryAfterRelease()
    {
        // A failed Start must fully roll back the acquired listener and
        // output subscription; otherwise the retry leaks the old socket (or
        // fails again) and the subscription stays subscribed.
        using var blocker = new TcpListener(IPAddress.Loopback, 0);
        blocker.Start();
        var occupiedPort = ((IPEndPoint)blocker.LocalEndpoint).Port;

        var options = new ConsoleBridgeOptions { Port = occupiedPort };
        var server = new ConsoleBridgeServer(new ConsoleInputBuffer(), new ConsoleOutputChannel(), options);

        Assert.Throws<SocketException>(() => server.Start());

        blocker.Stop();

        server.Start();
        Assert.Equal(occupiedPort, server.ActualPort);
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

    [Fact]
    public void AcceptLoop_NonCancellationError_StopsListenerAndAllowsRestart()
    {
        var logger = new TestLogger();
        var input = new ConsoleInputBuffer();
        var output = new ConsoleOutputChannel();
        var options = new ConsoleBridgeOptions { Port = 0 };
        var server = new ConsoleBridgeServer(input, output, options, logger: logger);
        server.Start();

        // Stop the listener out from under the accept loop, forcing a
        // non-cancellation accept error.
        var listenerField = typeof(ConsoleBridgeServer)
            .GetField("_listener", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var originalListener = (TcpListener)listenerField.GetValue(server)!;
        var originalPort = ((IPEndPoint)originalListener.LocalEndpoint).Port;
        originalListener.Stop();

        Assert.True(ConsoleBridgeTestInfrastructure.SpinUntil(
            () => logger.Errors.Any(e => e.Contains("Accept loop stopped")),
            ConsoleBridgeTestInfrastructure.CommandTimeoutMs));

        // Documented contract: the fault path rolls the started flag back so
        // the same instance can be restarted by the host.
        var startedField = typeof(ConsoleBridgeServer)
            .GetField("_started", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.Equal(0, (int)startedField.GetValue(server)!);

        // The same instance is restartable: Start binds a fresh listener that
        // accepts connections.
        server.Start();
        Assert.True(server.ActualPort > 0);
        Assert.NotEqual(originalPort, server.ActualPort);

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, server.ActualPort);
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };
        writer.WriteLine("restart_cmd");
        Assert.True(ConsoleBridgeTestInfrastructure.SpinUntil(
            () => input.TryDequeueCommand(out var line) && line == "restart_cmd",
            ConsoleBridgeTestInfrastructure.CommandTimeoutMs));

        server.Dispose();
    }

    // ── Output-side isolation (regression: RST during Publish crashed the caller) ──

    [Fact]
    public void OnConsoleOutput_BrokenWriter_DoesNotThrowToCaller()
    {
        var logger = new TestLogger();
        var server = new ConsoleBridgeServer(new ConsoleInputBuffer(), new ConsoleOutputChannel(), logger: logger);

        // Simulate a connection whose client stream died (RST) while the
        // handler thread is still inside its read loop: the writer targets
        // a stream whose writes always throw.
        var brokenWriter = new StreamWriter(new ThrowingWriteStream()) { AutoFlush = true };

        var writerField = typeof(ConsoleBridgeServer)
            .GetField("_writer", BindingFlags.NonPublic | BindingFlags.Instance)!;
        writerField.SetValue(server, brokenWriter);

        var onOutputField = typeof(ConsoleBridgeServer)
            .GetMethod("OnConsoleOutput", BindingFlags.NonPublic | BindingFlags.Instance)!;

        for (var i = 0; i < 5; i++)
        {
            try
            {
                onOutputField.Invoke(server, ["burst_line_" + i]);
            }
            catch (TargetInvocationException ex)
            {
                Assert.Fail($"OnConsoleOutput threw for a broken client writer: {ex.InnerException}");
            }
        }

        Assert.Null(writerField.GetValue(server));
        Assert.Contains(logger.Warnings,
            w => w.Contains(nameof(ConsoleBridgeServer)) && w.Contains("write"));
    }

    [Fact]
    public void Publish_BrokenClientWriter_DoesNotThrowToCaller()
    {
        var logger = new TestLogger();
        var input = new ConsoleInputBuffer();
        var output = new ConsoleOutputChannel();
        var server = new ConsoleBridgeServer(input, output, logger: logger);
        server.Start();
        var port = server.ActualPort;

        // Connect but keep the client open: the handler accepts and blocks in
        // its read loop holding the writer. Wait until it is inside the
        // connection (writer non-null) — a fixed sleep could race the accept
        // and leave the publish buffering without ever touching the client.
        using (var client = new TcpClient())
        {
            client.Connect(IPAddress.Loopback, port);

            var writerField = typeof(ConsoleBridgeServer)
                .GetField("_writer", BindingFlags.NonPublic | BindingFlags.Instance)!;
            Assert.True(ConsoleBridgeTestInfrastructure.SpinUntil(
                () => writerField.GetValue(server) is not null,
                ConsoleBridgeTestInfrastructure.CommandTimeoutMs),
                "Server should have accepted the connection before publish.");

            // Now hard-disconnect (RST) so the handler's writer targets a dead
            // stream, then publish from the game side. The publish call itself
            // must not throw: a dead client writer is a connection-level
            // failure and must be isolated from the game frame loop.
            client.Client.Shutdown(SocketShutdown.Both);
            client.Client.Close();
        }

        var published = false;
        for (var i = 0; i < 5; i++)
        {
            try
            {
                output.Publish($"burst_line_{i}");
                published = true;
            }
            catch (Exception ex)
            {
                Assert.Fail($"Publish threw for a broken client writer: {ex}");
            }
        }

        Assert.True(published, "Publish should have completed for all lines");

        // The server must still accept new connections afterwards.
        using var client2 = ConsoleBridgeTestInfrastructure.Connect(port,
            ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
        using var writer2 = new StreamWriter(client2.GetStream()) { AutoFlush = true };
        writer2.WriteLine("recovered_after_write_failure");
        Assert.True(ConsoleBridgeTestInfrastructure.SpinUntil(
            () => input.TryDequeueCommand(out var l) && l == "recovered_after_write_failure",
            ConsoleBridgeTestInfrastructure.CommandTimeoutMs));

        server.Dispose();
    }

    /// <summary>
    ///     Test double: writes fail with an <see cref="IOException" />,
    ///     simulating a socket whose peer hard-disconnected (RST). The first
    ///     flushes succeed so the writer can be constructed with
    ///     <c>AutoFlush</c> (its setter flushes immediately).
    /// </summary>
    private sealed class ThrowingWriteStream : Stream
    {
        private int _flushCalls;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            if (Interlocked.Increment(ref _flushCalls) > 2)
                throw new IOException("Simulated socket write failure");
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new IOException("Simulated socket write failure");
    }
}
