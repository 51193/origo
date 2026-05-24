using Origo.Core.Runtime.Console;
using Xunit;

namespace Origo.Core.Tests;

public class ConsoleInputQueueTests
{
    [Fact]
    public void ConsoleInputQueue_Enqueue_And_TryDequeue()
    {
        var queue = new ConsoleInputQueue();
        queue.Enqueue("help");

        var ok = queue.TryDequeueCommand(out var line);
        Assert.True(ok);
        Assert.Equal("help", line);
    }

    [Fact]
    public void ConsoleInputQueue_TryDequeue_EmptyQueue_ReturnsFalse()
    {
        var queue = new ConsoleInputQueue();
        var ok = queue.TryDequeueCommand(out var line);
        Assert.False(ok);
        Assert.Null(line);
    }

    [Fact]
    public void ConsoleInputQueue_Enqueue_WhitespaceIgnored()
    {
        var queue = new ConsoleInputQueue();
        queue.Enqueue("  ");
        queue.Enqueue("");

        var ok = queue.TryDequeueCommand(out _);
        Assert.False(ok);
    }

    [Fact]
    public void ConsoleInputQueue_Enqueue_TrimsInput()
    {
        var queue = new ConsoleInputQueue();
        queue.Enqueue("  hello  ");

        queue.TryDequeueCommand(out var line);
        Assert.Equal("hello", line);
    }

    [Fact]
    public void ConsoleInputQueue_FIFO_Order()
    {
        var queue = new ConsoleInputQueue();
        queue.Enqueue("first");
        queue.Enqueue("second");

        queue.TryDequeueCommand(out var line1);
        queue.TryDequeueCommand(out var line2);
        Assert.Equal("first", line1);
        Assert.Equal("second", line2);
    }

    [Fact]
    public void ConsoleInputQueue_Clear_EmptiesQueue()
    {
        var queue = new ConsoleInputQueue();
        queue.Enqueue("a");
        queue.Enqueue("b");
        queue.Clear();

        Assert.False(queue.TryDequeueCommand(out _));
    }
}

// ── ConsoleOutputChannel ───────────────────────────────────────────────
