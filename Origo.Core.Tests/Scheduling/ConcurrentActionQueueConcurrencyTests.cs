using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Origo.Core.Scheduling;
using Xunit;

namespace Origo.Core.Tests;

public class ConcurrentActionQueueConcurrencyTests
{
    [Fact]
    public async Task Enqueue_FromManyThreads_ExecuteAllRunsAllActions()
    {
        var queue = new ConcurrentActionQueue(new TestLogger());
        var executedCount = 0;
        const int workerCount = 8;
        const int actionsPerWorker = 50;

        var tasks = Enumerable.Range(0, workerCount)
            .Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < actionsPerWorker; i++)
                    queue.Enqueue(() => Interlocked.Increment(ref executedCount));
            }));

        await Task.WhenAll(tasks);

        var executed = queue.ExecuteAll();
        Assert.Equal(workerCount * actionsPerWorker, executed);
        Assert.Equal(workerCount * actionsPerWorker, executedCount);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void ExecuteAll_WhenActionsKeepReenqueueing_ThrowsAtMaxReentrantDepth()
    {
        var queue = new ConcurrentActionQueue(new TestLogger());
        queue.Enqueue(() => queue.Enqueue(() => queue.Enqueue(() => { })));

        Action selfRequeue = null!;
        selfRequeue = () => queue.Enqueue(selfRequeue);
        queue.Enqueue(selfRequeue);

        var ex = Assert.Throws<InvalidOperationException>(() => queue.ExecuteAll());
        Assert.Contains("max re-entrant drain depth", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExecuteAll_ExactlyMaxDepthBatches_ThenQueueEmpty_DoesNotThrow()
    {
        // Regression: exactly 100 batches with the queue becoming empty right
        // after the 100th batch used to trigger the depth exception on the
        // next loop iteration, which should instead terminate normally.
        const int chainDepth = 100;
        var queue = new ConcurrentActionQueue(new TestLogger());
        var executed = 0;

        Action action = () => executed++;
        for (var i = 0; i < chainDepth - 1; i++)
        {
            var next = action;
            action = () =>
            {
                executed++;
                queue.Enqueue(next);
            };
        }

        queue.Enqueue(action);

        var ex = Record.Exception(() => queue.ExecuteAll());
        Assert.Null(ex);
        Assert.Equal(chainDepth, executed);
    }

    [Fact]
    public void ExecuteAll_EmptyQueue_IsIdempotent()
    {
        var queue = new ConcurrentActionQueue(new TestLogger());

        Assert.Equal(0, queue.ExecuteAll());
        Assert.Equal(0, queue.ExecuteAll());
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void ExecuteAll_AfterClear_DoesNotExecuteClearedActions()
    {
        var queue = new ConcurrentActionQueue(new TestLogger());
        var callCount = 0;
        queue.Enqueue(() => callCount++);
        queue.Enqueue(() => callCount++);
        queue.Clear();

        var executed = queue.ExecuteAll();

        Assert.Equal(0, executed);
        Assert.Equal(0, callCount);
        Assert.Equal(0, queue.Count);
    }
}
