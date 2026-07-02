using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Origo.Core.Runtime.Console;
using Xunit;

namespace Origo.ConsoleBridge.Tests;

public static class ConsoleBridgeTestInfrastructure
{
    public const int CommandTimeoutMs = 2000;
    public const int OutputTimeoutMs = 3000;
    public const int ConnectRetryIntervalMs = 5;
    public const int SpinPollIntervalMs = 10;
    public const int StressCommandCount = 100;

    public static (ConsoleBridgeServer server, (ConsoleInputBuffer input, ConsoleOutputChannel output) queues)
        CreateStartedServer(int port = 0)
    {
        var input = new ConsoleInputBuffer();
        var output = new ConsoleOutputChannel();
        var options = new ConsoleBridgeOptions { Port = port };
        var server = new ConsoleBridgeServer(input, output, options);
        server.Start();
        return (server, (input, output));
    }

    public static bool SpinUntil(Func<bool> condition, int timeoutMs)
    {
        ArgumentNullException.ThrowIfNull(condition);

        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
                return true;
            Thread.Sleep(SpinPollIntervalMs);
        }

        return condition();
    }

    public static string? ReadLineWithTimeout(StreamReader reader, int timeoutMs)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var stream = reader.BaseStream;
        var oldTimeout = stream.ReadTimeout;
        try
        {
            stream.ReadTimeout = timeoutMs;
            return reader.ReadLine();
        }
        catch (IOException)
        {
            return null;
        }
        finally
        {
            stream.ReadTimeout = oldTimeout;
        }
    }

    public static TcpClient ConnectWithRetry(int port, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        SocketException? lastEx = null;
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                var client = new TcpClient();
                client.Connect(IPAddress.Loopback, port);
                return client;
            }
            catch (SocketException ex)
            {
                lastEx = ex;
                Thread.Sleep(ConnectRetryIntervalMs);
            }
        }
        throw new TimeoutException(
            $"Could not connect to port {port} within {timeoutMs}ms", lastEx);
    }

    public static void AssertConnectionRefused(int port, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                using var client = new TcpClient();
                client.Connect(IPAddress.Loopback, port);
                Assert.Fail("Connection should have been refused after server dispose");
            }
            catch (SocketException)
            {
            }
            Thread.Sleep(ConnectRetryIntervalMs);
        }
    }
}
