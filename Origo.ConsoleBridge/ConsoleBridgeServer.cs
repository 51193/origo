using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Logging;
using Origo.Core.Runtime.Console;

namespace Origo.ConsoleBridge;

/// <summary>
///     TCP console bridge server. Single-connection mode: handles one
///     client connection at a time. The accept loop waits for the current
///     handler to finish before accepting the next connection;
///     new connections naturally wait in the OS backlog queue.
/// </summary>
public sealed class ConsoleBridgeServer : IDisposable
{
    private const int _maxPendingOutputLines = 1000;
    private const int _disposeJoinTimeoutMs = 3000;

    private readonly IConsoleInputSource _input;
    private readonly ILogger _logger;
    private readonly ConsoleBridgeOptions _options;
    private readonly IConsoleOutputChannel _output;
    private readonly Queue<string> _pendingOutput = new();

    private readonly Lock _writerLock = new();
    private readonly CancellationTokenSource _cts = new();

    private TcpListener _listener = null!;
    private long _outputSubId;
    private int _started;
    private int _droppedLineCount;
    private StreamWriter? _writer;
    private Task? _acceptTask;

    public ConsoleBridgeServer(
        IConsoleInputSource input,
        IConsoleOutputChannel output,
        ConsoleBridgeOptions? options = null,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        _input = input;
        _output = output;
        _options = options ?? new ConsoleBridgeOptions();
        _logger = logger ?? NullLogger.Instance;
    }

    public int ActualPort { get; private set; }

    public void Dispose()
    {
        if (_cts.IsCancellationRequested)
            return;

        _cts.Cancel();

        // Do not dispose the writer here: it wraps the same NetworkStream the
        // connection handler owns. _cts.Cancel() already unblocks the handler's
        // ReadLineAsync, and its finally block closes the stream/client once, in
        // order (a graceful FIN). Disposing the shared stream from this thread as
        // well would race that teardown and can reset the connection (RST).
        lock (_writerLock)
        {
            _writer = null;
        }

        _listener?.Stop();
        _output.Unsubscribe(_outputSubId);

        if (_acceptTask is not null)
        {
            try
            {
                var completed = _acceptTask.Wait(_disposeJoinTimeoutMs);
                if (!completed)
                    _logger.Log(LogLevel.Warning, nameof(ConsoleBridgeServer),
                        new LogMessageBuilder().Build("Accept loop did not stop within the join timeout."));
            }
            catch (AggregateException ex)
            {
                // Task.Wait only ever throws AggregateException; unexpected
                // exceptions propagate to the caller (fail-fast).
                foreach (var inner in ex.InnerExceptions)
                    _logger.Log(LogLevel.Error, nameof(ConsoleBridgeServer),
                        new LogMessageBuilder().Build($"Accept loop faulted: {inner.Message}"));
            }
        }

        _cts.Dispose();
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_cts.IsCancellationRequested, this);
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
            return;

        try
        {
            _listener = new TcpListener(IPAddress.Loopback, _options.Port);
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.Start();
            ActualPort = ((IPEndPoint)_listener.LocalEndpoint).Port;

            if (_outputSubId == 0)
                _outputSubId = _output.Subscribe(OnConsoleOutput);

            _acceptTask = Task.Run(() => AcceptLoopAsync(_cts.Token), _cts.Token);
        }
        catch
        {
            // Roll the started flag back so a failed Start (e.g. port in use)
            // can be retried after the cause is resolved.
            _started = 0;
            throw;
        }
    }

    private void OnConsoleOutput(string line)
    {
        lock (_writerLock)
        {
            if (_writer is not null)
            {
                try
                {
                    _writer.WriteLine(line);
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException)
                {
                    // A hard client disconnect (RST) breaks the socket write
                    // path while the handler thread is still inside its read
                    // loop. This is a connection-level failure: detach the
                    // dead writer so subsequent lines buffer again, and never
                    // let it propagate into the game frame loop.
                    _logger.Log(LogLevel.Warning, nameof(ConsoleBridgeServer),
                        new LogMessageBuilder()
                            .Build($"Output write failed; connection considered dead and detached: {ex.Message}"));
                    _writer = null;
                }
            }
            else
            {
                if (_pendingOutput.Count >= _maxPendingOutputLines)
                {
                    _pendingOutput.Dequeue();
                    _droppedLineCount++;
                }
                _pendingOutput.Enqueue(line);
            }
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // A cancellation makes AcceptTcpClientAsync throw
                // OperationCanceledException (handled above); anything else is a
                // genuine system-level socket error — stop the listener so the
                // host can restart the server (Start rolls the started flag
                // back and is retryable).
                _logger.Log(LogLevel.Error, nameof(ConsoleBridgeServer),
                    new LogMessageBuilder().Build(
                        $"Accept loop stopped after a non-cancellation error: {ex.Message}"));
                try
                {
                    _listener.Stop();
                }
                catch (Exception stopEx)
                {
                    _logger.Log(LogLevel.Warning, nameof(ConsoleBridgeServer),
                        new LogMessageBuilder().Build($"Failed to stop listener: {stopEx.Message}"));
                }

                Interlocked.Exchange(ref _started, 0);
                break;
            }

            try
            {
                await HandleConnectionAsync(client, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.Log(LogLevel.Warning, nameof(ConsoleBridgeServer),
                    new LogMessageBuilder().Build($"Connection handler failed: {ex}"));
            }
        }
    }

    private async Task HandleConnectionAsync(TcpClient client, CancellationToken ct)
    {
        try
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream);
            using var writer = new StreamWriter(stream) { AutoFlush = true };

            lock (_writerLock)
            {
                _writer = writer;
                if (_droppedLineCount > 0)
                {
                    writer.WriteLine(
                        $"[ConsoleBridge] Warning: {_droppedLineCount} output line(s) were dropped due to buffer overflow.");
                    _droppedLineCount = 0;
                }
                foreach (var line in _pendingOutput)
                    writer.WriteLine(line);
                _pendingOutput.Clear();
            }

            while (!ct.IsCancellationRequested)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException)
                {
                    // Client disconnect or stream reset: end this connection.
                    break;
                }

                if (line is null)
                    break;

                if (!string.IsNullOrWhiteSpace(line))
                    _input.Enqueue(line.Trim());
            }
        }
        finally
        {
            lock (_writerLock)
            {
                _writer = null;
            }

            client.Close();
        }
    }
}
