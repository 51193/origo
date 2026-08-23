using System;
using Origo.Core.Scheduling;
using Xunit;

namespace Origo.Core.Tests;

public class ConcurrentActionQueueTests
{
    [Fact]
    public void ConcurrentActionQueue_Enqueue_IncreasesCount()
    {
        var queue = new ConcurrentActionQueue(new TestLogger());
        queue.Enqueue(() => { });
        queue.Enqueue(() => { });
        Assert.Equal(2, queue.Count);
    }

    [Fact]
    public void ConcurrentActionQueue_ExecuteAll_RunsAllActions()
    {
        var queue = new ConcurrentActionQueue(new TestLogger());
        var callCount = 0;
        queue.Enqueue(() => callCount++);
        queue.Enqueue(() => callCount++);
        queue.Enqueue(() => callCount++);
        var executed = queue.ExecuteAll();
        Assert.Equal(3, executed);
        Assert.Equal(3, callCount);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void ConcurrentActionQueue_ExecuteAll_EmptyQueue_ReturnsZero()
    {
        var queue = new ConcurrentActionQueue(new TestLogger());
        Assert.Equal(0, queue.ExecuteAll());
    }

    [Fact]
    public void ConcurrentActionQueue_Clear_EmptiesQueue()
    {
        var queue = new ConcurrentActionQueue(new TestLogger());
        queue.Enqueue(() => { });
        queue.Enqueue(() => { });
        queue.Clear();
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void ConcurrentActionQueue_ExecuteAll_ActionThatReenqueues()
    {
        var queue = new ConcurrentActionQueue(new TestLogger());
        var executed = false;
        queue.Enqueue(() => queue.Enqueue(() => executed = true));
        var count = queue.ExecuteAll();
        Assert.Equal(2, count);
        Assert.True(executed);
    }

    [Fact]
    public void ConcurrentActionQueue_Enqueue_ThrowsOnNull()
    {
        var queue = new ConcurrentActionQueue(new TestLogger());
        Assert.Throws<ArgumentNullException>(() => queue.Enqueue(null!));
    }

    [Fact]
    public void ConcurrentActionQueue_Constructor_ThrowsOnNullLogger() =>
        Assert.Throws<ArgumentNullException>(() => new ConcurrentActionQueue(null!));

    [Fact]
    public void ConcurrentActionQueue_ExecuteAll_PropagatesException()
    {
        var queue = new ConcurrentActionQueue(new TestLogger());
        queue.Enqueue(() => throw new InvalidOperationException("boom"));
        Assert.Throws<InvalidOperationException>(() => queue.ExecuteAll());
    }

    [Fact]
    public void ConcurrentActionQueue_ExecuteAll_DiscardCallbackThrows_RunsRemainingCallbacksAndAggregates()
    {
        var queue = new ConcurrentActionQueue(new TestLogger());
        var remainingDiscardRan = false;
        queue.Enqueue(() => throw new InvalidOperationException("action failed"));
        queue.Enqueue(() => { }, () => throw new InvalidOperationException("discard failed"));
        queue.Enqueue(() => { }, () => remainingDiscardRan = true);

        var ex = Assert.Throws<AggregateException>(() => queue.ExecuteAll());

        Assert.Equal(2, ex.InnerExceptions.Count);
        Assert.Contains(ex.InnerExceptions, e => e.Message == "action failed");
        Assert.Contains(ex.InnerExceptions, e => e.Message == "discard failed");
        Assert.True(remainingDiscardRan);
    }

    [Fact]
    public void ConcurrentActionQueue_Clear_DiscardCallbackThrows_RunsRemainingCallbacksAndRethrowsFirst()
    {
        var queue = new ConcurrentActionQueue(new TestLogger());
        var remainingDiscardRan = false;
        queue.Enqueue(() => { }, () => throw new InvalidOperationException("discard1 failed"));
        queue.Enqueue(() => { }, () => remainingDiscardRan = true);

        var ex = Assert.Throws<InvalidOperationException>(() => queue.Clear());

        Assert.Equal("discard1 failed", ex.Message);
        Assert.True(remainingDiscardRan);
    }
}
