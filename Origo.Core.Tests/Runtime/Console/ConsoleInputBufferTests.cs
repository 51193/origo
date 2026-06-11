using Origo.Core.Runtime.Console;
using Xunit;

namespace Origo.Core.Tests;

public class ConsoleInputBufferTests
{
    [Fact]
    public void ConsoleInputBuffer_Enqueue_And_TryDequeue()
    {
        var queue = new ConsoleInputBuffer();
        queue.Enqueue("help");

        var ok = queue.TryDequeueCommand(out var line);
        Assert.True(ok);
        Assert.Equal("help", line);
    }

    [Fact]
    public void ConsoleInputBuffer_TryDequeue_EmptyQueue_ReturnsFalse()
    {
        var queue = new ConsoleInputBuffer();
        var ok = queue.TryDequeueCommand(out var line);
        Assert.False(ok);
        Assert.Null(line);
    }

    [Fact]
    public void ConsoleInputBuffer_Enqueue_WhitespaceIgnored()
    {
        var queue = new ConsoleInputBuffer();
        queue.Enqueue("  ");
        queue.Enqueue("");

        var ok = queue.TryDequeueCommand(out _);
        Assert.False(ok);
    }

    [Fact]
    public void ConsoleInputBuffer_Enqueue_TrimsInput()
    {
        var queue = new ConsoleInputBuffer();
        queue.Enqueue("  hello  ");

        queue.TryDequeueCommand(out var line);
        Assert.Equal("hello", line);
    }

    [Fact]
    public void ConsoleInputBuffer_FIFO_Order()
    {
        var queue = new ConsoleInputBuffer();
        queue.Enqueue("first");
        queue.Enqueue("second");

        queue.TryDequeueCommand(out var line1);
        queue.TryDequeueCommand(out var line2);
        Assert.Equal("first", line1);
        Assert.Equal("second", line2);
    }

    [Fact]
    public void ConsoleInputBuffer_Clear_EmptiesQueue()
    {
        var queue = new ConsoleInputBuffer();
        queue.Enqueue("a");
        queue.Enqueue("b");
        queue.Clear();

        Assert.False(queue.TryDequeueCommand(out _));
    }
}

// ── ConsoleOutputChannel ───────────────────────────────────────────────
