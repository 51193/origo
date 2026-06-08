using Origo.Core.Abstractions.Console;
using Origo.Core.Runtime.Console;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     IConsoleInputSource 接口契约测试。
///     任何 IConsoleInputSource 的实现者都必须通过此套测试。
/// </summary>
public class ConsoleInputSourceContractTests
{
    private static IConsoleInputSource CreateSut() => new ConsoleInputQueue();

    [Fact]
    public void Enqueue_And_TryDequeue_RoundTrip()
    {
        var sut = CreateSut();
        sut.Enqueue("test_command");
        Assert.True(sut.TryDequeueCommand(out var cmd));
        Assert.Equal("test_command", cmd);
    }

    [Fact]
    public void TryDequeue_EmptyQueue_ReturnsFalse()
    {
        var sut = CreateSut();
        Assert.False(sut.TryDequeueCommand(out var cmd));
        Assert.Null(cmd);
    }

    [Fact]
    public void TryDequeue_AfterExhausting_ReturnsFalse()
    {
        var sut = CreateSut();
        sut.Enqueue("only_one");
        sut.TryDequeueCommand(out _);
        Assert.False(sut.TryDequeueCommand(out _));
    }

    [Fact]
    public void Enqueue_FifoOrder_Preserved()
    {
        var sut = CreateSut();
        sut.Enqueue("first");
        sut.Enqueue("second");
        sut.Enqueue("third");

        sut.TryDequeueCommand(out var cmd1);
        sut.TryDequeueCommand(out var cmd2);
        sut.TryDequeueCommand(out var cmd3);

        Assert.Equal("first", cmd1);
        Assert.Equal("second", cmd2);
        Assert.Equal("third", cmd3);
    }

    [Fact]
    public void Enqueue_EmptyString_Ignored()
    {
        var sut = CreateSut();
        sut.Enqueue("");
        Assert.False(sut.TryDequeueCommand(out _));
    }

    [Fact]
    public void Enqueue_WhitespaceOnly_Ignored()
    {
        var sut = CreateSut();
        sut.Enqueue("   \t  ");
        Assert.False(sut.TryDequeueCommand(out _));
    }

    [Fact]
    public void Enqueue_TrimsWhitespaceAroundContent()
    {
        var sut = CreateSut();
        sut.Enqueue("  hello world  ");
        Assert.True(sut.TryDequeueCommand(out var cmd));
        Assert.Equal("hello world", cmd);
    }

    [Fact]
    public void Clear_EmptiesAllPendingCommands()
    {
        var sut = CreateSut();
        sut.Enqueue("cmd1");
        sut.Enqueue("cmd2");
        sut.Enqueue("cmd3");

        sut.Clear();

        Assert.False(sut.TryDequeueCommand(out _));
    }

    [Fact]
    public void Clear_OnAlreadyEmpty_DoesNotThrow()
    {
        var sut = CreateSut();
        sut.Clear();
        Assert.False(sut.TryDequeueCommand(out _));
    }

    [Fact]
    public void Enqueue_AfterClear_WorksNormally()
    {
        var sut = CreateSut();
        sut.Enqueue("before_clear");
        sut.Clear();
        sut.Enqueue("after_clear");

        Assert.True(sut.TryDequeueCommand(out var cmd));
        Assert.Equal("after_clear", cmd);
    }

    [Fact]
    public void Enqueue_Null_Ignored()
    {
        var sut = CreateSut();
        sut.Enqueue(null!);
        Assert.False(sut.TryDequeueCommand(out _));
    }
}
