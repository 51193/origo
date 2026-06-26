using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Origo.Core.Abstractions.Console;
using Origo.Core.Runtime.Console;

namespace Origo.ConsoleBridge;

/// <summary>
///     TCP 控制台桥接服务器。
///     单连接模式：Accept 线程接受连接，Handle 线程读入命令，
///     控制台输出通过 Subscribe 回调直接写入 TCP 连接。
/// </summary>
public sealed class ConsoleBridgeServer : IDisposable
{
    private const int MaxPendingOutputLines = 1000;
    private const int ReadTimeoutMs = 30000;
    private const int DisposeJoinTimeoutMs = 3000;

    private readonly object _acceptLock = new();
    private readonly IConsoleInputSource _input;
    private readonly ConsoleBridgeOptions _options;
    private readonly IConsoleOutputChannel _output;
    private readonly List<string> _pendingOutput = new();

    private readonly object _writerLock = new();
    private readonly CancellationTokenSource _cts = new();

    private Thread? _acceptThread;
    private Thread? _handleThread;
    private volatile bool _hasActiveClient;
    private TcpListener? _listener;
    private long _outputSubId;
    private Socket? _serverSocket;
    private int _started;
    private StreamWriter? _writer;

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

        _serverSocket?.Close();
        _listener?.Stop();
        _output.Unsubscribe(_outputSubId);

        _acceptThread?.Join(DisposeJoinTimeoutMs);
        _handleThread?.Join(DisposeJoinTimeoutMs);

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
        _serverSocket = _listener.Server;
        ActualPort = ((IPEndPoint)_listener.LocalEndpoint).Port;

        _outputSubId = _output.Subscribe(OnConsoleOutput);

        _acceptThread = new Thread(AcceptLoop)
        {
            Name = "ConsoleBridge-Accept",
            IsBackground = true
        };
        _acceptThread.Start();
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
                if (_pendingOutput.Count < MaxPendingOutputLines)
                    _pendingOutput.Add(line);
            }
        }
    }

    private void AcceptLoop()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                TcpClient? client = null;
                try
                {
                    client = _listener!.AcceptTcpClient();
                }
                catch (SocketException) when (_cts.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException) when (_cts.IsCancellationRequested)
                {
                    break;
                }

                if (client is null)
                    continue;

                lock (_acceptLock)
                {
                    if (_hasActiveClient)
                    {
                        client.Close();
                        continue;
                    }

                    _hasActiveClient = true;
                }

                _handleThread = new Thread(() => HandleConnection(client))
                {
                    Name = "ConsoleBridge-Handle",
                    IsBackground = true
                };
                _handleThread.Start();
            }
        }
        finally
        {
            _listener?.Stop();
        }
    }

    private void HandleConnection(TcpClient client)
    {
        try
        {
            client.ReceiveTimeout = ReadTimeoutMs;
            using var stream = client.GetStream();
            stream.ReadTimeout = ReadTimeoutMs;
            using var reader = new StreamReader(stream);
            using var writer = new StreamWriter(stream) { AutoFlush = true };

            lock (_writerLock)
            {
                _writer = writer;
                foreach (var line in _pendingOutput)
                    writer.WriteLine(line);
                _pendingOutput.Clear();
            }

            while (!_cts.IsCancellationRequested)
            {
                string? line;
                try
                {
                    line = reader.ReadLine();
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

            lock (_acceptLock)
            {
                _hasActiveClient = false;
            }
        }
    }
}
