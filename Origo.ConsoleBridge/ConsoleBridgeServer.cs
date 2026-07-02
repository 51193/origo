using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Origo.Core.Abstractions.Console;
using Origo.Core.Runtime.Console;

namespace Origo.ConsoleBridge;

/// <summary>
///     TCP 控制台桥接服务器。
///     单连接模式：内部使用异步 I/O 接受连接和读取命令，
///     控制台输出通过 Subscribe 回调直接写入 TCP 连接。
/// </summary>
public sealed class ConsoleBridgeServer : IDisposable
{
    private const int _maxPendingOutputLines = 1000;
    private const int _disposeJoinTimeoutMs = 3000;

    private readonly IConsoleInputSource _input;
    private readonly ConsoleBridgeOptions _options;
    private readonly IConsoleOutputChannel _output;
    private readonly List<string> _pendingOutput = [];

    private readonly object _writerLock = new();
    private readonly CancellationTokenSource _cts = new();

    private int _activeClientCount;
    private TcpListener _listener = null!;
    private long _outputSubId;
    private int _started;
    private StreamWriter? _writer;
    private Task? _acceptTask;

    public ConsoleBridgeServer(
        IConsoleInputSource input,
        IConsoleOutputChannel output,
        ConsoleBridgeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        _input = input;
        _output = output;
        _options = options ?? new ConsoleBridgeOptions();
    }

    public int ActualPort { get; private set; }

    public void Dispose()
    {
        if (_cts.IsCancellationRequested)
            return;

        _cts.Cancel();

        lock (_writerLock)
        {
            _writer?.Dispose();
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
                if (_pendingOutput.Count < _maxPendingOutputLines)
                    _pendingOutput.Add(line);
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
                client = await _listener.AcceptTcpClientAsync(ct);
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

            if (Interlocked.CompareExchange(ref _activeClientCount, 1, 0) != 0)
            {
                client.Close();
                continue;
            }

            _ = HandleConnectionAsync(client, ct);
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
                foreach (var line in _pendingOutput)
                    writer.WriteLine(line);
                _pendingOutput.Clear();
            }

            while (!ct.IsCancellationRequested)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(ct);
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

            Interlocked.Exchange(ref _activeClientCount, 0);
            client.Close();
        }
    }
}
