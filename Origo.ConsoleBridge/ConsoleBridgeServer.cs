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

    private readonly object _writerLock = new();
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
                _acceptTask.Wait(_disposeJoinTimeoutMs);
            }
            catch (AggregateException)
            {
            }
        }

        _cts.Dispose();
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_cts.IsCancellationRequested, this);
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
            return;

        if (_cts.IsCancellationRequested)
        {
            _started = 0;
            throw new ObjectDisposedException(nameof(ConsoleBridgeServer));
        }

        _listener = new TcpListener(IPAddress.Loopback, _options.Port);
        _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listener.Start();
        ActualPort = ((IPEndPoint)_listener.LocalEndpoint).Port;

        _outputSubId = _output.Subscribe(OnConsoleOutput);

        _acceptTask = Task.Run(() => AcceptLoopAsync(_cts.Token), _cts.Token);
    }

    private void OnConsoleOutput(string line)
    {
        lock (_writerLock)
        {
            if (_writer is not null)
            {
                _writer.WriteLine(line);
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
            catch (SocketException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (ct.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await HandleConnectionAsync(client, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.Log(LogLevel.Warning, nameof(ConsoleBridgeServer),
                    $"Connection handler failed: {ex.Message}");
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
                    break;
                }
                catch (ObjectDisposedException)
                {
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
