using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Origo.ConsoleBridge.Tests;

public class ConsoleBridgeServerLifecycleTests
{
    [Fact]
    public void Start_Stop_NoExceptions()
    {
        var (server, _) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        server.Dispose();
    }

    [Fact]
    public void Start_AfterDispose_Throws()
    {
        var (server, _) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        server.Dispose();
        Assert.Throws<ObjectDisposedException>(() => server.Start());
    }

    [Fact]
    public void DoubleDispose_DoesNotThrow()
    {
        var (server, _) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        server.Dispose();
        var ex = Record.Exception(() => server.Dispose());
        Assert.Null(ex);
    }


    [Fact]
    public void Dispose_StopsAcceptingNewConnections()
    {
        var (server, _) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;
        server.Dispose();

        ConsoleBridgeTestInfrastructure.AssertConnectionRefused(port, 200);
    }

    [Fact]
    public async Task Dispose_WhileClientConnected_NoHang()
    {
        var (server, _) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);

        var disposed = false;
        var disposeTask = Task.Run(() =>
        {
            server.Dispose();
            disposed = true;
        }, TestContext.Current.CancellationToken);

        await disposeTask.WaitAsync(TimeSpan.FromMilliseconds(3000), TestContext.Current.CancellationToken);
        Assert.True(disposed);
    }

    [Fact]
    public void ActualPort_ReflectsAssignedPort()
    {
        var (server, _) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        Assert.True(server.ActualPort > 0);
        server.Dispose();
    }

    [Fact]
    public void SecondConnection_IsRejected()
    {
        var (server, (input, _)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using var client1 = new TcpClient();
        client1.Connect(IPAddress.Loopback, port);
        using var writer1 = new StreamWriter(client1.GetStream()) { AutoFlush = true };

        writer1.WriteLine("from_first");
        var ok1 = ConsoleBridgeTestInfrastructure.SpinUntil(() => input.TryDequeueCommand(out var l) && l == "from_first", ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
        Assert.True(ok1, "'from_first' should arrive from first client");

        try
        {
            using var client2 = new TcpClient();
            client2.Connect(IPAddress.Loopback, port);
            using var writer2 = new StreamWriter(client2.GetStream()) { AutoFlush = true };
            writer2.WriteLine("from_second");
        }
        catch (IOException)
        {
        }

        writer1.WriteLine("still_works");
        var ok2 = ConsoleBridgeTestInfrastructure.SpinUntil(() => input.TryDequeueCommand(out var l) && l == "still_works", ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
        Assert.True(ok2, "'still_works' should arrive - client1 still functional after second rejected");

        server.Dispose();
    }

    [Fact]
    public void SecondConnection_CommandDoesNotArrive()
    {
        var (server, (input, _)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using var client1 = new TcpClient();
        client1.Connect(IPAddress.Loopback, port);
        var writer1 = new StreamWriter(client1.GetStream()) { AutoFlush = true };

        writer1.WriteLine("from_first");
        ConsoleBridgeTestInfrastructure.SpinUntil(() => input.TryDequeueCommand(out var l) && l == "from_first", ConsoleBridgeTestInfrastructure.CommandTimeoutMs);

        try
        {
            using var client2 = new TcpClient();
            client2.Connect(IPAddress.Loopback, port);
            using var writer2 = new StreamWriter(client2.GetStream()) { AutoFlush = true };
            writer2.WriteLine("from_second");
        }
        catch (IOException)
        {
        }

        while (input.TryDequeueCommand(out _))
        {
        }

        Assert.False(input.TryDequeueCommand(out _));

        server.Dispose();
    }

    [Fact]
    public void ClientDisconnect_ServerAcceptsNewConnection()
    {
        var (server, (input, _)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using (var client = new TcpClient())
        {
            client.Connect(IPAddress.Loopback, port);
        }

        using var client2 = ConsoleBridgeTestInfrastructure.Connect(port, ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
        using var writer = new StreamWriter(client2.GetStream()) { AutoFlush = true };

        writer.WriteLine("after_reconnect");

        var ok = ConsoleBridgeTestInfrastructure.SpinUntil(() => input.TryDequeueCommand(out var l) && l == "after_reconnect", ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
        Assert.True(ok, "'after_reconnect' should arrive after disconnect/reconnect");

        server.Dispose();
    }

    [Fact]
    public void ClientDisconnect_ThenThirdAccepted()
    {
        var (server, (input, _)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using (var client = new TcpClient())
        {
            client.Connect(IPAddress.Loopback, port);
        }

        using (var client2 = ConsoleBridgeTestInfrastructure.Connect(port, ConsoleBridgeTestInfrastructure.CommandTimeoutMs))
        {
        }

        using var client3 = ConsoleBridgeTestInfrastructure.Connect(port, ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
        using var writer = new StreamWriter(client3.GetStream()) { AutoFlush = true };

        writer.WriteLine("third");

        ConsoleBridgeTestInfrastructure.SpinUntil(() => input.TryDequeueCommand(out var l) && l == "third", ConsoleBridgeTestInfrastructure.CommandTimeoutMs);

        server.Dispose();
    }

    [Fact]
    public void ClientImmediateDisconnect_ServerRecovers()
    {
        var (server, (input, _)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using (var client = new TcpClient())
        {
            client.Connect(IPAddress.Loopback, port);
        }

        using var client2 = ConsoleBridgeTestInfrastructure.Connect(port, ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
        using var writer = new StreamWriter(client2.GetStream()) { AutoFlush = true };
        writer.WriteLine("after_immediate");

        ConsoleBridgeTestInfrastructure.SpinUntil(() => input.TryDequeueCommand(out var l) && l == "after_immediate", ConsoleBridgeTestInfrastructure.CommandTimeoutMs);

        server.Dispose();
    }

    [Fact]
    public void MidSession_ClientHardDisconnect_ServerRecovers()
    {
        var (server, (input, _)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        writer.WriteLine("in_session");
        var ok1 = ConsoleBridgeTestInfrastructure.SpinUntil(() => input.TryDequeueCommand(out var l) && l == "in_session", ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
        Assert.True(ok1, "'in_session' should arrive from initial client");

        client.Client.Dispose();

        using var client2 = ConsoleBridgeTestInfrastructure.Connect(port, ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
        using var writer2 = new StreamWriter(client2.GetStream()) { AutoFlush = true };
        writer2.WriteLine("after_hard_disconnect");

        var ok2 = ConsoleBridgeTestInfrastructure.SpinUntil(() => input.TryDequeueCommand(out var l) && l == "after_hard_disconnect", ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
        Assert.True(ok2, "'after_hard_disconnect' should arrive after hard disconnect recovery");

        server.Dispose();
    }

    [Fact]
    public void MidSession_ClientAbort_NextConnectionAccepted()
    {
        var (server, (input, _)) = ConsoleBridgeTestInfrastructure.CreateStartedServer();
        var port = server.ActualPort;

        using (var client = new TcpClient())
        {
            client.Connect(IPAddress.Loopback, port);
            using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };
            writer.WriteLine("before_abort");
            var ok1 = ConsoleBridgeTestInfrastructure.SpinUntil(() => input.TryDequeueCommand(out var l) && l == "before_abort", ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
            Assert.True(ok1, "'before_abort' should arrive from initial client");
        }

        using var client2 = ConsoleBridgeTestInfrastructure.Connect(port, ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
        using var writer2 = new StreamWriter(client2.GetStream()) { AutoFlush = true };
        writer2.WriteLine("after_abort");

        var ok2 = ConsoleBridgeTestInfrastructure.SpinUntil(() => input.TryDequeueCommand(out var l) && l == "after_abort", ConsoleBridgeTestInfrastructure.CommandTimeoutMs);
        Assert.True(ok2, "'after_abort' should arrive after abort recovery");

        server.Dispose();
    }
}
