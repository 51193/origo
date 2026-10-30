using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public class PersistenceRequestTrackingTests
{
    private static SndContext CreateContext(TestMemoryFileSystem fs)
    {
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());

        var ctx = new SndContext(new SndContextParameters(
            runtime, io, metaAccess, pathResolver, "root", "res://initial", "entry.json")
        {
            AutoDiscoverStrategies = false
        });
        fs.SeedFile("entry.json",
            "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]");
        return ctx;
    }

    [Fact]
    public void RequestSaveGame_IsTrackedUntilFlushed()
    {
        var fs = new TestMemoryFileSystem();
        var ctx = CreateContext(fs);
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.FlushFrame();

        ctx.Save.RequestSaveGame("tracked_slot");
        Assert.Equal(1, ctx.Deferred.GetPendingPersistenceRequestCount());

        ctx.FlushFrame();
        Assert.Equal(0, ctx.Deferred.GetPendingPersistenceRequestCount());
    }

    [Fact]
    public void RequestContinueGame_IsTrackedUntilFlushed()
    {
        var fs = new TestMemoryFileSystem();
        var ctx = CreateContext(fs);
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.FlushFrame();
        ctx.Save.RequestSaveGame("continue_slot");
        ctx.FlushFrame();

        Assert.True(ctx.Lifecycle.RequestContinueGame());
        Assert.Equal(1, ctx.Deferred.GetPendingPersistenceRequestCount());

        ctx.FlushFrame();
        Assert.Equal(0, ctx.Deferred.GetPendingPersistenceRequestCount());
    }

    [Fact]
    public void RequestLoadInitialSave_IsTrackedUntilFlushed()
    {
        // Loading the initial save requires an initial-storage snapshot that
        // this harness does not provide; only the tracking semantics of the
        // request are verified here.
        var fs = new TestMemoryFileSystem();
        var ctx = CreateContext(fs);

        ctx.Lifecycle.RequestLoadInitialSave();
        Assert.Equal(1, ctx.Deferred.GetPendingPersistenceRequestCount());
    }

    [Fact]
    public void RequestLoadMainMenuEntrySave_IsTrackedUntilFlushed()
    {
        var fs = new TestMemoryFileSystem();
        var ctx = CreateContext(fs);

        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        Assert.Equal(1, ctx.Deferred.GetPendingPersistenceRequestCount());

        ctx.FlushFrame();
        Assert.Equal(0, ctx.Deferred.GetPendingPersistenceRequestCount());
    }

    [Fact]
    public void RequestSwitchForegroundLevel_IsTrackedUntilFlushed()
    {
        var fs = new TestMemoryFileSystem();
        var ctx = CreateContext(fs);
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.FlushFrame();

        ctx.Save.RequestSwitchForegroundLevel("other_level");
        Assert.Equal(1, ctx.Deferred.GetPendingPersistenceRequestCount());

        ctx.FlushFrame();
        Assert.Equal(0, ctx.Deferred.GetPendingPersistenceRequestCount());
    }

    [Fact]
    public void FailedTrackedRequest_DiscardingLaterTrackedRequest_ReturnsPendingCountToZero()
    {
        var fs = new TestMemoryFileSystem();
        var ctx = CreateContext(fs);
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.FlushFrame();

        // The first tracked request fails while loading the initial save;
        // fail-fast queue semantics discard the second tracked request in the
        // same system-deferred batch. The discarded request is no longer
        // pending and must release its persistence-request count.
        ctx.Lifecycle.RequestLoadInitialSave();
        ctx.Save.RequestSaveGame("doomed");
        Assert.Equal(2, ctx.Deferred.GetPendingPersistenceRequestCount());

        Assert.Throws<InvalidOperationException>(() => ctx.FlushFrame());

        Assert.Equal(0, ctx.Deferred.GetPendingPersistenceRequestCount());
    }

    [Fact]
    public void IsPersistenceIdle_ReflectsPendingRequestTracking()
    {
        var fs = new TestMemoryFileSystem();
        var ctx = CreateContext(fs);
        Assert.True(ctx.Deferred.IsPersistenceIdle);

        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        Assert.False(ctx.Deferred.IsPersistenceIdle);

        ctx.FlushFrame();
        Assert.True(ctx.Deferred.IsPersistenceIdle);

        ctx.Save.RequestSaveGame("idle_slot");
        Assert.False(ctx.Deferred.IsPersistenceIdle);

        ctx.FlushFrame();
        Assert.True(ctx.Deferred.IsPersistenceIdle);
    }

    [Fact]
    public void RequestSaveGameAuto_ConcurrentCalls_ProduceUniqueIds()
    {
        // Deferred enqueueing is documented as thread-safe, so auto-save id
        // allocation must be atomic: two callers released at the same
        // millisecond must not receive the same slot id. The id counter is also
        // monotonic by construction, which is asserted sequentially below.
        var fs = new TestMemoryFileSystem();
        var ctx = CreateContext(fs);
        const int threadCount = 8;
        const int callsPerThread = 2;
        var ids = new List<string>(threadCount * callsPerThread);
        var idsLock = new Lock();
        Exception? firstError = null;
        using var start = new ManualResetEventSlim(false);
        using var done = new CountdownEvent(threadCount);

        for (var t = 0; t < threadCount; t++)
        {
            var thread = new Thread(() =>
            {
                try
                {
                    start.Wait();
                    for (var i = 0; i < callsPerThread; i++)
                    {
                        var id = ctx.Save.RequestSaveGameAuto();
                        lock (idsLock)
                            ids.Add(id);
                    }
                }
                catch (Exception ex)
                {
                    lock (idsLock)
                        firstError ??= ex;
                }
                finally
                {
                    done.Signal();
                }
            });
            thread.Start();
        }

        start.Set();
        Assert.True(done.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken),
            "Concurrent auto-save requests did not finish.");
        Assert.Null(firstError);
        Assert.Equal(threadCount * callsPerThread, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());

        // Monotonicity: an allocation made after the concurrent batch is
        // numerically greater than every id handed out before it.
        var sequentialId = long.Parse(
            ctx.Save.RequestSaveGameAuto(), CultureInfo.InvariantCulture);
        Assert.All(ids, id => Assert.True(
            sequentialId > long.Parse(id, CultureInfo.InvariantCulture),
            $"auto-save id '{id}' is not below the later allocation '{sequentialId}'."));

        // Leave no pending requests behind: with no active ProgressRun the
        // first queued save fails fast and the deferred queue discards the
        // rest, which releases every tracked persistence counter.
        Assert.Throws<InvalidOperationException>(() => ctx.FlushFrame());
        Assert.Equal(0, ctx.Deferred.GetPendingPersistenceRequestCount());
    }

    [Fact]
    public void Shutdown_CancelsQueuedPersistenceRequests_AndRunsDiscardCallbacks()
    {
        var fs = new TestMemoryFileSystem();
        var ctx = CreateContext(fs);

        // A save queued before shutdown must not stay pending (it would keep
        // IsPersistenceIdle false and throw on a later frame because the
        // context is terminal).
        ctx.Save.RequestSaveGame("queued_slot");
        Assert.Equal(1, ctx.Deferred.GetPendingPersistenceRequestCount());
        Assert.False(ctx.Deferred.IsPersistenceIdle);

        // An action queued with a discard callback must still have that callback
        // run when shutdown cancels it.
        var discarded = 0;
        ctx.Runtime.EnqueueSystemDeferred(
            () => throw new InvalidOperationException("A cancelled deferred action must not run."),
            onDiscard: () => discarded++);

        ctx.Shutdown();

        Assert.Equal(1, discarded);
        Assert.Equal(0, ctx.Deferred.GetPendingPersistenceRequestCount());
        Assert.True(ctx.Deferred.IsPersistenceIdle);
        Assert.False(fs.DirectoryExists("root/save_queued_slot"));

        // A frame after shutdown must not throw from the cancelled queue.
        var ex = Record.Exception(() => ctx.FlushFrame());
        Assert.Null(ex);
    }

    [Fact]
    public void Shutdown_WhenDiscardCallbackThrows_ClearsQueueAndRemainsRetryable()
    {
        var fs = new TestMemoryFileSystem();
        var ctx = CreateContext(fs);

        // A cancelled action's discard callback runs during shutdown. If that
        // callback throws, the queue has still been cleared (so retrying the
        // shutdown cannot replay the action), but the failure must be reported
        // and must not commit the terminal shutdown state.
        var discarded = 0;
        ctx.Runtime.EnqueueSystemDeferred(
            () => throw new InvalidOperationException("A cancelled deferred action must not run."),
            onDiscard: () =>
            {
                discarded++;
                throw new InvalidOperationException("discard callback boom");
            });

        var firstFailure = Assert.Throws<InvalidOperationException>(() => ctx.Shutdown());
        Assert.Contains("discard callback boom", firstFailure.Message, StringComparison.Ordinal);
        Assert.Equal(1, discarded);

        // Clearing already happened; the retry runs only idempotent cleanup and
        // commits the terminal state.
        ctx.Shutdown();
        Assert.Throws<InvalidOperationException>(() => ctx.Save.RequestSaveGame("after_retry_shutdown"));
        Assert.Null(Record.Exception(() => ctx.FlushFrame()));
    }
}
