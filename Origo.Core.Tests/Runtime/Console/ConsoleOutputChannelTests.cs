using System;
using System.Collections.Generic;
using Origo.Core.Runtime.Console;
using Xunit;

namespace Origo.Core.Tests;

public class ConsoleOutputChannelTests
{
    [Fact]
    public void ConsoleOutputChannel_Subscribe_And_Publish()
    {
        var channel = new ConsoleOutputChannel();
        var received = new List<string>();
        channel.Subscribe(msg => received.Add(msg));

        channel.Publish("hello");
        Assert.Single(received);
        Assert.Equal("hello", received[0]);
    }

    [Fact]
    public void ConsoleOutputChannel_Unsubscribe_StopsReceiving()
    {
        var channel = new ConsoleOutputChannel();
        var received = new List<string>();
        var id = channel.Subscribe(msg => received.Add(msg));

        channel.Publish("first");
        channel.Unsubscribe(id);
        channel.Publish("second");

        Assert.Single(received);
        Assert.Equal("first", received[0]);
    }

    [Fact]
    public void ConsoleOutputChannel_Unsubscribe_InvalidId_ReturnsFalse()
    {
        var channel = new ConsoleOutputChannel();
        Assert.False(channel.Unsubscribe(9999));
    }

    [Fact]
    public void ConsoleOutputChannel_MultipleSubscribers()
    {
        var channel = new ConsoleOutputChannel();
        var list1 = new List<string>();
        var list2 = new List<string>();
        channel.Subscribe(msg => list1.Add(msg));
        channel.Subscribe(msg => list2.Add(msg));

        channel.Publish("msg");
        Assert.Single(list1);
        Assert.Single(list2);
    }

    [Fact]
    public void ConsoleOutputChannel_Publish_NullBroadcastsEmpty()
    {
        var channel = new ConsoleOutputChannel();
        var received = new List<string>();
        channel.Subscribe(msg => received.Add(msg));

        channel.Publish(null!);
        Assert.Single(received);
        Assert.Equal(string.Empty, received[0]);
    }

    [Fact]
    public void ConsoleOutputChannel_Subscribe_ThrowsOnNull()
    {
        var channel = new ConsoleOutputChannel();
        Assert.Throws<ArgumentNullException>(() => channel.Subscribe(null!));
    }
}
