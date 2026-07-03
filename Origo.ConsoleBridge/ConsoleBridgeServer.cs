using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Origo.Core.Abstractions.Console;
using Origo.Core.Runtime.Console;

namespace Origo.ConsoleBridge;

/// <summary>
///     TCP 控制台桥接服务器。
///     单连接模式：一次只处理一个客户端连接。
///     Accept 循环在 handler 完成后才继续接受下一个连接，
///     新连接在此期间进入 OS backlog 队列自然等待。
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
            catch (AggregateException ex) when (ex.InnerExceptions.Count > 0 && ex.InnerExceptions.All(e => e is OperationCanceledException))
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

            await HandleConnectionAsync(client, ct);
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

            client.Close();
        }
    }
}
