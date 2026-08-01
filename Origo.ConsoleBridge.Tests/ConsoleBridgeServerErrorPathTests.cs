using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Origo.Core.Runtime.Console;
using Xunit;

namespace Origo.ConsoleBridge.Tests;

public class ConsoleBridgeServerErrorPathTests
{
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

        Thread.Sleep(100);

        using var client2 = ConsoleBridgeTestInfrastructure.Connect(port,
            ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
        using var writer2 = new StreamWriter(client2.GetStream()) { AutoFlush = true };

        writer2.WriteLine("recovered_cmd");
        Assert.True(ConsoleBridgeTestInfrastructure.SpinUntil(
            () => input.TryDequeueCommand(out var l) && l == "recovered_cmd",
            ConsoleBridgeTestInfrastructure.CommandTimeoutMs));

        server.Dispose();
    }
}
