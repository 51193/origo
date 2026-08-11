using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    private TcpClient? _client;
    private bool _detachRequested;
    private Task? _acceptTask;

    /// <summary>
    ///     Creates a bridge server forwarding client console input to
    ///     <paramref name="input" /> and publishing server output on
    ///     <paramref name="output" />.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="input" /> or <paramref name="output" /> is null.</exception>
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

    /// <summary>
    ///     The port the server is actually listening on (may differ from the
    ///     configured port when the configured one is 0/auto-assigned).
    /// </summary>
    public int ActualPort { get; private set; }

    /// <summary>
    ///     Stops the listener, cancels the accept loop, and releases the
    ///     output subscription. Idempotent.
    /// </summary>
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
        _listener?.Dispose();
        _output.Unsubscribe(_outputSubId);

        if (_acceptTask is not null)
        {
            var acceptLoopCompleted = false;
            try
            {
                acceptLoopCompleted = _acceptTask.Wait(_disposeJoinTimeoutMs);
                if (!acceptLoopCompleted)
                    _logger.Log(LogLevel.Warning, nameof(ConsoleBridgeServer),
                        new LogMessageBuilder().Build("Accept loop did not stop within the join timeout."));
            }
            catch (AggregateException ex)
            {
                // Task.Wait only ever throws AggregateException; a faulted accept
                // loop is logged as an Error so the host can restart the server
                // (matching the documented exception-propagation strategy).
                acceptLoopCompleted = true;
                foreach (var inner in ex.InnerExceptions)
                    _logger.Log(LogLevel.Error, nameof(ConsoleBridgeServer),
                        new LogMessageBuilder().Build($"Accept loop faulted: {inner.Message}"));
            }

            // Only dispose the CTS once the accept loop has actually stopped.
            // Disposing it while the loop is still running makes the task
            // register callbacks on a disposed token and surface a misleading
            // "non-cancellation error" log for a normal shutdown race.
            if (acceptLoopCompleted)
                _cts.Dispose();
        }
        else
        {
            _cts.Dispose();
        }
    }

    /// <summary>
    ///     Starts listening for a single console connection. Idempotent; a
    ///     failed start rolls back its acquired resources so the same
    ///     instance can be retried.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    ///     Thrown when called after <see cref="Dispose" />.
    /// </exception>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_cts.IsCancellationRequested, this);
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
            return;

        try
        {
            // Socket.SendTimeout treats 0 as "infinite": a non-positive
            // configured timeout would silently reintroduce the frame-thread
            // stall the bounded send timeout exists to prevent.
            if (_options.OutputSendTimeoutMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(_options), _options.OutputSendTimeoutMs,
                    "OutputSendTimeoutMs must be positive; zero or negative values disable the " +
                    "bounded send timeout (Socket.SendTimeout semantics).");

            _listener = new TcpListener(IPAddress.Loopback, _options.Port);
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.Start();
            ActualPort = ((IPEndPoint)_listener.LocalEndpoint).Port;

            if (_outputSubId == 0)
                _outputSubId = _output.Subscribe(OnConsoleOutput);

            _acceptTask = Task.Run(() => AcceptLoopAsync(_cts.Token), _cts.Token);

            if (_cts.IsCancellationRequested)
            {
                // Start raced with Dispose: the accept loop was launched with an
                // already-cancelled token and exits immediately, but a successful
                // Start must never be observable after Dispose. Roll back so the
                // instance is left cleanly disposed and retry-safe.
                throw new ObjectDisposedException(nameof(ConsoleBridgeServer),
                    "Dispose was called while Start was in progress.");
            }
        }
        catch
        {
            // Roll back everything a failed Start acquired (listener, output
            // subscription) so a retry after the cause is resolved starts
            // from a clean state instead of leaking the socket or the
            // subscription.
            _listener?.Stop();
            _listener?.Dispose();
            if (_outputSubId != 0)
            {
                _output.Unsubscribe(_outputSubId);
                _outputSubId = 0;
            }

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
                    // dead writer, close the dead client, and buffer the
                    // undelivered line so the next connection replays it —
                    // never let the failure propagate into the game frame
                    // loop. Closing the client also ends the handler's read
                    // loop, freeing the single connection slot: a dead
                    // connection must not occupy it forever (otherwise no
                    // "next connection" ever arrives and the buffered lines
                    // are lost).
                    _logger.Log(LogLevel.Warning, nameof(ConsoleBridgeServer),
                        new LogMessageBuilder()
                            .Build($"Output write failed; connection considered dead and detached: {ex.Message}"));
                    _writer = null;
                    _detachRequested = true;
                    _client?.Close();
                    if (_pendingOutput.Count >= _maxPendingOutputLines)
                    {
                        _pendingOutput.Dequeue();
                        _droppedLineCount++;
                    }

                    _pendingOutput.Enqueue(line);
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
                if (ct.IsCancellationRequested)
                {
                    // Dispose cancelled the token before stopping the
                    // listener; the stop can surface as a plain socket error
                    // instead of OperationCanceledException. Treat it as a
                    // normal shutdown rather than a genuine failure.
                    break;
                }

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
                    _listener.Dispose();
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
            // A bounded send timeout keeps output writes (which run on the
            // game frame thread) from stalling forever on a client that stops
            // reading: once the TCP send buffer stays full past the timeout,
            // the write fails and the connection is detached.
            client.Client.SendTimeout = _options.OutputSendTimeoutMs;
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream);
            using var writer = new StreamWriter(stream) { AutoFlush = true };

            lock (_writerLock)
            {
                _writer = writer;
                _client = client;
                try
                {
                    if (_droppedLineCount > 0)
                        writer.WriteLine(
                            $"[ConsoleBridge] Warning: {_droppedLineCount} output line(s) were dropped due to buffer overflow.");

                    // The backlog replay runs on a bounded time budget: a
                    // slow-but-reading client drains each line below the send
                    // timeout, so an unbounded replay would hold the writer
                    // lock for the whole backlog and stall the game frame
                    // thread (the caller of OnConsoleOutput) for seconds.
                    // Aborting at the budget keeps the remaining lines
                    // buffered for the next connection. The budget scales
                    // with the send timeout but is capped so a large timeout
                    // cannot reintroduce a long frame-thread stall (the
                    // multiplication is widened to long so an extreme
                    // configured timeout cannot overflow).
                    var budget = TimeSpan.FromMilliseconds(
                        Math.Clamp(_options.OutputSendTimeoutMs * 4L, 200, 1000));
                    var flushTimer = Stopwatch.StartNew();
                    while (_pendingOutput.Count > 0)
                    {
                        if (flushTimer.Elapsed >= budget)
                        {
                            _logger.Log(LogLevel.Warning, nameof(ConsoleBridgeServer),
                                new LogMessageBuilder().Build(
                                    "Backlog replay aborted at its time budget; the remaining lines stay buffered for the next connection."));
                            break;
                        }

                        writer.WriteLine(_pendingOutput.Peek());
                        _pendingOutput.Dequeue();
                    }

                    // Only reset the drop counter after the flush completed;
                    // on a mid-flush write failure or a budget abort the
                    // warning and the undelivered lines are retried by the
                    // next connection.
                    if (_pendingOutput.Count == 0)
                        _droppedLineCount = 0;
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException)
                {
                    // A slow or dead client filled the send buffer past the
                    // send timeout: end this connection instead of blocking
                    // the caller. The backlog is still queued (it is cleared
                    // only after a successful flush) for the next connection.
                    _logger.Log(LogLevel.Warning, nameof(ConsoleBridgeServer),
                        new LogMessageBuilder().Build(
                            $"Initial output flush failed; connection detached: {ex.Message}"));
                    _detachRequested = true;
                    return;
                }
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
                catch (Exception ex) when (ex is IOException or ObjectDisposedException)
                {
                    // The connection was deliberately closed after an output
                    // write failure (the dead-client detach); end the handler
                    // silently instead of reporting a false read failure.
                    if (IsDetachRequested())
                        break;

                    // Client disconnect or stream reset: end this connection.
                    // Surfaces the I/O failure instead of swallowing it silently
                    // (documented exception-propagation strategy).
                    _logger.Log(LogLevel.Warning, nameof(ConsoleBridgeServer),
                        new LogMessageBuilder().Build(
                            $"Client read failed, ending connection: {ex}"));
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
                _client = null;
                _detachRequested = false;
            }

            client.Close();
        }
    }

    private bool IsDetachRequested()
    {
        lock (_writerLock)
        {
            return _detachRequested;
        }
    }
}
