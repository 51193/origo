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
///     单连接模式：Accept 线程接受连接，Handle 线程处理 I/O，
///     通过 Subscribe 回调接收控制台输出。
/// </summary>
public sealed class ConsoleBridgeServer : IDisposable
{
    private readonly object _acceptLock = new();
    private readonly ConsoleInputQueue _input;
    private readonly ConsoleBridgeOptions _options;
    private readonly IConsoleOutputChannel _output;

    private readonly Queue<string> _sendQueue = new();
    private readonly object _sendQueueLock = new();

    private Thread? _acceptThread;
    private volatile bool _disposed;
    private Thread? _handleThread;
    private volatile bool _hasActiveClient;
    private TcpListener? _listener;
    private long _outputSubId;
    private Socket? _serverSocket;

    public ConsoleBridgeServer(
        ConsoleInputQueue input,
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
        if (_disposed)
            return;

        _disposed = true;
        try
        {
            _serverSocket?.Close();
        }
        catch
        {
            /* ignore */
        }

        try
        {
            _listener?.Stop();
        }
        catch
        {
            /* ignore */
        }

        try
        {
            _output.Unsubscribe(_outputSubId);
        }
        catch
        {
            /* ignore */
        }
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_acceptThread is not null)
            return;

        _listener = new TcpListener(IPAddress.Loopback, _options.Port);
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
        lock (_sendQueueLock)
        {
            _sendQueue.Enqueue(line);
        }
    }

    private void AcceptLoop()
    {
        try
        {
            while (!_disposed)
            {
                TcpClient? client = null;
                try
                {
                    client = _listener!.AcceptTcpClient();
                }
                catch (SocketException) when (_disposed)
                {
                    break;
                }
                catch (ObjectDisposedException) when (_disposed)
                {
                    break;
                }
                catch (Exception)
                {
                    continue;
                }

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
            try
            {
                _listener?.Stop();
            }
            catch
            {
                /* ignore */
            }
        }
    }

    private void HandleConnection(TcpClient client)
    {
        try
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream);
            using var writer = new StreamWriter(stream) { AutoFlush = true };

            while (!_disposed)
            {
                DrainSendQueue(writer);

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
            try
            {
                client.Close();
            }
            catch
            {
                /* ignore */
            }

            _hasActiveClient = false;
        }
    }

    private void DrainSendQueue(StreamWriter writer)
    {
        List<string> pending;
        lock (_sendQueueLock)
        {
            if (_sendQueue.Count == 0)
                return;

            pending = new List<string>(_sendQueue);
            _sendQueue.Clear();
        }

        foreach (var line in pending)
            try
            {
                writer.WriteLine(line);
            }
            catch (IOException)
            {
                break;
            }
    }
}
