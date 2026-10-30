using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.DataSource;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;
using Origo.Core.StateMachine;
using Xunit;

namespace Origo.Core.Tests;

public partial class RandomAndStateMachineTests
{
    [Fact]
    public void StateMachineContainer_PopAllRuntime_InvokesBeforeRemoveTopToBottom()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPopStrategy());

        var events = new List<string>();
        SmPushStrategy.PushEvents = events;
        SmPopStrategy.PopRemoveEvents = events;
        SmPopStrategy.PopQuitEvents = events;

        try
        {
            var container = new StateMachineContainer(pool, ctx.StateMachineContext);
            var sm = container.CreateOrGet("ui", "sm.push.test", "sm.pop.test");
            sm.Push("a");
            sm.Push("b");

            container.PopAllRuntime();
            container.Clear();

            Assert.Equal(
                new[]
                {
                    "push:runtime:null->a",
                    "push:runtime:a->b",
                    "pop:runtime:b->a",
                    "pop:runtime:a->null"
                },
                events);
        }
        finally
        {
            ResetStrategyHooks();
        }
    }

    [Fact]
    public void StateMachineContainer_PopAllOnQuit_InvokesBeforeQuitTopToBottom()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPopStrategy());

        var events = new List<string>();
        SmPushStrategy.PushEvents = events;
        SmPopStrategy.PopRemoveEvents = events;
        SmPopStrategy.PopQuitEvents = events;

        try
        {
            var container = new StateMachineContainer(pool, ctx.StateMachineContext);
            var sm = container.CreateOrGet("ui", "sm.push.test", "sm.pop.test");
            sm.Push("a");
            sm.Push("b");

            container.PopAllOnQuit();
            container.Clear();

            Assert.Equal(
                new[]
                {
                    "push:runtime:null->a",
                    "push:runtime:a->b",
                    "pop:beforeQuit:b->a",
                    "pop:beforeQuit:a->null"
                },
                events);
        }
        finally
        {
            ResetStrategyHooks();
        }
    }

    [Fact]
    public void StateMachineContainer_PopAllOnQuit_TraversesMachinesInInsertionOrder()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPopOrderProbeStrategy());

        var events = new List<string>();
        SmPopOrderProbeStrategy.Events = events;

        try
        {
            var container = new StateMachineContainer(pool, ctx.StateMachineContext);
            // insertion order: b -> a
            var smB = container.CreateOrGet("b", "sm.push.test", "sm.pop.orderprobe");
            var smA = container.CreateOrGet("a", "sm.push.test", "sm.pop.orderprobe");

            smB.Push("b1");
            smA.Push("a1");

            container.PopAllOnQuit();
            container.Clear();

            Assert.Equal(new[] { "b", "a" }, events);
        }
        finally
        {
            ResetStrategyHooks();
        }
    }

    [Fact]
    public void StateMachineContainer_PopAllOnQuit_OneThrowingLevel_DoesNotStrandLowerLevels()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmThrowOnTopQuitStrategy());

        var events = new List<string>();
        SmThrowOnTopQuitStrategy.Events = events;

        try
        {
            var container = new StateMachineContainer(pool, ctx.StateMachineContext);
            var sm = container.CreateOrGet("fsm", "sm.push.test", "sm.pop.throw_on_top");
            sm.Push("bottom");
            sm.Push("boom");

            // The top level's quit hook throws. The top level is force-removed
            // without replaying hooks so the lower level still receives its
            // quit hook instead of being stranded on the stack.
            var ex = Assert.Throws<InvalidOperationException>(() => container.PopAllOnQuit());
            Assert.Contains("quit hook failure on top level", ex.Message, StringComparison.Ordinal);
            Assert.Equal(new[] { "quit:boom->bottom", "quit:bottom->null" }, events);

            container.Clear();
        }
        finally
        {
            ResetStrategyHooks();
        }
    }

    [Fact]
    public void StateMachineContainer_PopAllOnQuit_HookPushesThenThrows_TerminatesAndKeepsLowerLevels()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPushThenThrowOnQuitStrategy());

        var events = new List<string>();
        SmPushThenThrowOnQuitStrategy.Events = events;
        SmPushThenThrowOnQuitStrategy.Reset();

        try
        {
            var container = new StateMachineContainer(pool, ctx.StateMachineContext);
            var sm = container.CreateOrGet("fsm", "sm.push.test", "sm.pop.push_then_throw");
            SmPushThenThrowOnQuitStrategy.Machine = sm;
            sm.Push("bottom");
            sm.Push("boom");
            Assert.Equal(["bottom", "boom"], sm.Snapshot());

            // The "boom" hook pushes "extra" onto the stack and then throws.
            // Force-popping must use the snapshot captured before the hook, so
            // the machine drops "boom" (and the hook's extra level), terminates,
            // and still runs the lower "bottom" quit hook.
            var ex = Assert.Throws<InvalidOperationException>(() => container.PopAllOnQuit());
            Assert.Contains("pushed a level then threw", ex.Message, StringComparison.Ordinal);
            Assert.Equal(new[] { "quit:boom->bottom", "quit:bottom->null" }, events);
            Assert.Empty(sm.Snapshot());

            container.Clear();
        }
        finally
        {
            ResetStrategyHooks();
        }
    }

    [Fact]
    public void StateMachineContainer_PopAllOnQuit_HookPushesThenReturns_TerminatesAndKeepsLowerLevels()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPushThenReturnOnQuitStrategy());

        var events = new List<string>();
        SmPushThenReturnOnQuitStrategy.Events = events;
        SmPushThenReturnOnQuitStrategy.Reset();

        try
        {
            var container = new StateMachineContainer(pool, ctx.StateMachineContext);
            var sm = container.CreateOrGet("fsm", "sm.push.test", "sm.pop.push_then_return");
            SmPushThenReturnOnQuitStrategy.Machine = sm;
            sm.Push("bottom");
            sm.Push("boom");

            // The "boom" hook pushes "extra" and returns normally, so the pop
            // call reports success by removing the pushed level and the original
            // stack does not shrink. The no-progress detection must drop "boom"
            // from the pre-hook snapshot, terminate, and still run the lower
            // "bottom" quit hook — with the "boom" hook invoked exactly once.
            var ex = Record.Exception(() => container.PopAllOnQuit());
            Assert.Null(ex);
            Assert.Equal(new[] { "quit:boom->bottom", "quit:bottom->null" }, events);
            Assert.Empty(sm.Snapshot());

            container.Clear();
        }
        finally
        {
            ResetStrategyHooks();
        }
    }

    [Fact]
    public void StateMachineContainer_PopAllOnQuit_AggregatesTwoMachineFailures_AndProcessesLaterMachines()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmThrowOnQuitStrategy());
        pool.Register(() => new SmPopOrderProbeStrategy());

        var events = new List<string>();
        SmPopOrderProbeStrategy.Events = events;

        try
        {
            var container = new StateMachineContainer(pool, ctx.StateMachineContext);
            var first = container.CreateOrGet("first", "sm.push.test", "sm.pop.throw");
            var second = container.CreateOrGet("second", "sm.push.test", "sm.pop.throw");
            var third = container.CreateOrGet("third", "sm.push.test", "sm.pop.orderprobe");
            first.Push("a");
            second.Push("b");
            third.Push("c");

            // Two throwing machines aggregate into one AggregateException; the
            // later healthy machine is still visited.
            var ex = Assert.Throws<AggregateException>(() => container.PopAllOnQuit());
            Assert.Equal(2, ex.InnerExceptions.Count);
            Assert.All(ex.InnerExceptions, inner =>
                Assert.Contains("quit hook failure", inner.Message, StringComparison.Ordinal));
            Assert.Equal(new[] { "third" }, events);

            container.Clear();
        }
        finally
        {
            ResetStrategyHooks();
        }
    }

    [Fact]
    public void StateMachineContainer_Clear_ReleasesAllMachines_WhenOneDisposeThrows()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPopStrategy());

        var container = new StateMachineContainer(pool, ctx.StateMachineContext);
        container.CreateOrGet("first", "sm.push.test", "sm.pop.test");
        container.CreateOrGet("second", "sm.push.test", "sm.pop.test");

        // Sabotage one machine's push reference so its Dispose throws; the
        // container must still dispose the remaining machines and clear its
        // dictionaries before rethrowing.
        pool.ReleaseStrategy("sm.push.test");

        Assert.Throws<InvalidOperationException>(() => container.Clear());
        Assert.False(container.TryGet("first", out _));
        Assert.False(container.TryGet("second", out _));
    }

    [Fact]
    public void StateMachineContainer_DeserializeFromNode_ThrowsOnNullNode()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var pool = runtime.SndWorld.StrategyPool;

        var container = new StateMachineContainer(pool, ctx.StateMachineContext);

        Assert.Throws<ArgumentNullException>(() =>
            container.DeserializeFromNode(null!, TestFactory.CreateRegistry()));
    }

    [Fact]
    public void StateMachineContainer_DeserializeFromNode_ArrayRoot_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var pool = runtime.SndWorld.StrategyPool;
        var container = new StateMachineContainer(pool, ctx.StateMachineContext);

        using var arrayRoot = TestFactory.CreateJsonCodec().Decode("[]");

        // A structurally wrong payload (array root) must fail the load instead
        // of silently clearing every machine.
        Assert.Throws<InvalidOperationException>(() =>
            container.DeserializeFromNode(arrayRoot, TestFactory.CreateRegistry()));
    }

    [Fact]
    public void StateMachineContainer_DeserializeFromNode_MissingMachinesKey_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var pool = runtime.SndWorld.StrategyPool;
        var container = new StateMachineContainer(pool, ctx.StateMachineContext);

        using var missingKey = TestFactory.CreateJsonCodec().Decode("""{ "unrelated": 1 }""");

        Assert.Throws<InvalidOperationException>(() =>
            container.DeserializeFromNode(missingKey, TestFactory.CreateRegistry()));
    }

    [Fact]
    public void StateMachineContainer_SerializeDeserialize_RoundTrip()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPopStrategy());

        var c1 = new StateMachineContainer(pool, ctx.StateMachineContext);
        var sm = c1.CreateOrGet("ui", "sm.push.test", "sm.pop.test");
        sm.Push("p");
        sm.Push("q");

        using var node = c1.SerializeToNode(TestFactory.CreateRegistry());
        var c2 = new StateMachineContainer(pool, ctx.StateMachineContext);
        c2.DeserializeFromNode(node, TestFactory.CreateRegistry());
        c2.TryGet("ui", out var restored);
        Assert.NotNull(restored);
        Assert.Equal(["p", "q"], restored!.Snapshot());

        c1.PopAllOnQuit();
        c2.PopAllOnQuit();
    }

    [Fact]
    public void StateMachineContainer_DeserializeWithoutHooks_SwapsAtomically()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SwapTestPushStrategy());
        pool.Register(() => new SwapTestPopStrategy());

        var container = new StateMachineContainer(pool, ctx.StateMachineContext);

        // Serialize an empty container, then deserialize back – no-op swap.
        using var emptyNode = container.SerializeToNode(TestFactory.CreateRegistry());
        container.DeserializeFromNode(emptyNode, TestFactory.CreateRegistry());
        Assert.False(container.TryGet("anything", out _));

        // Populate with a machine, serialize, then deserialize into the same container.
        var sm = container.CreateOrGet("nav", "sm.swap.push", "sm.swap.pop");
        ((IStateMachine)sm).RestoreStackWithoutHooks(["a", "b"]);
        using var node = container.SerializeToNode(TestFactory.CreateRegistry());

        // Deserialize replaces old state atomically.
        container.DeserializeFromNode(node, TestFactory.CreateRegistry());

        Assert.True(container.TryGet("nav", out var restored));
        Assert.NotNull(restored);
        Assert.Equal(["a", "b"], restored!.Snapshot());

        // Old key must still be accessible (same key name).
        Assert.True(container.TryGet("nav", out _));

        container.Clear();
    }

    [Fact]
    public void StateMachineContainer_CreateOrGet_IdempotentForSameKeyAndIndices()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "r", "i", "e.json"));
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPopStrategy());
        try
        {
            var c = new StateMachineContainer(pool, ctx.StateMachineContext);
            var a = c.CreateOrGet("k", "sm.push.test", "sm.pop.test");
            var b = c.CreateOrGet("k", "sm.push.test", "sm.pop.test");
            Assert.Same(a, b);
            c.Clear();
        }
        finally
        {
            ResetStrategyHooks();
        }
    }

    [Fact]
    public void StateMachineContainer_CreateOrGet_ConflictingIndices_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "r", "i", "e.json"));
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPopStrategy());
        pool.Register(() => new SmPopOrderProbeStrategy());
        try
        {
            var c = new StateMachineContainer(pool, ctx.StateMachineContext);
            c.CreateOrGet("k", "sm.push.test", "sm.pop.test");
            Assert.Throws<InvalidOperationException>(() =>
                c.CreateOrGet("k", "sm.push.test", "sm.pop.orderprobe"));
            c.Clear();
        }
        finally
        {
            ResetStrategyHooks();
        }
    }

    [Fact]
    public void StateMachineContainer_Remove_UnknownKey_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "r", "i", "e.json"));
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPopStrategy());
        try
        {
            var c = new StateMachineContainer(pool, ctx.StateMachineContext);

            var ex = Assert.Throws<InvalidOperationException>(() => c.Remove("missing"));
            Assert.Contains("missing", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            ResetStrategyHooks();
        }
    }

    [Fact]
    public void StateMachineContainer_Remove_MountedKey_DisposesAndRemoves()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "r", "i", "e.json"));
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPopStrategy());
        try
        {
            var c = new StateMachineContainer(pool, ctx.StateMachineContext);
            c.CreateOrGet("k", "sm.push.test", "sm.pop.test");

            c.Remove("k");

            Assert.False(c.TryGet("k", out _));
        }
        finally
        {
            ResetStrategyHooks();
        }
    }

    [Fact]
    public void StateMachineContainer_FlushAllAfterLoad_NotifiesPushStrategy()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "r", "i", "e.json"));
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPopStrategy());
        var after = new List<string>();
        SmPushStrategy.AfterLoadEvents = after;
        try
        {
            var c = new StateMachineContainer(pool, ctx.StateMachineContext);
            var sm = c.CreateOrGet("ui", "sm.push.test", "sm.pop.test");
            ((IStateMachine)sm).RestoreStackWithoutHooks(["x", "y"]);
            c.FlushAllAfterLoad();
            Assert.Equal(
                new[] { "push:afterload:null->x", "push:afterload:x->y" },
                after);
            c.Clear();
        }
        finally
        {
            ResetStrategyHooks();
        }
    }

    [Fact]
    public void StateMachineContainer_PopAllOnQuit_FirstHookThrows_StillRunsLaterMachines()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmThrowOnQuitStrategy());
        pool.Register(() => new SmPopOrderProbeStrategy());
        var events = new List<string>();
        SmPopOrderProbeStrategy.Events = events;
        try
        {
            var container = new StateMachineContainer(pool, ctx.StateMachineContext);
            container.CreateOrGet("a", "sm.push.test", "sm.pop.throw").Push("a");
            container.CreateOrGet("b", "sm.push.test", "sm.pop.orderprobe").Push("b");

            Assert.Throws<InvalidOperationException>(() => container.PopAllOnQuit());
            Assert.Contains("b", events);
            container.Clear();
        }
        finally
        {
            ResetStrategyHooks();
        }
    }

    [Fact]
    public void StateMachineContainer_DeserializeFromNode_DuplicateMachineKey_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "r", "i", "e.json"));
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPopStrategy());
        var json =
            """
            {
              "machines": [
                { "key": "dup", "pushIndex": "sm.push.test", "popIndex": "sm.pop.test", "stack": [] },
                { "key": "dup", "pushIndex": "sm.push.test", "popIndex": "sm.pop.test", "stack": [] }
              ]
            }
            """;
        using var node = TestFactory.NodeFromJson(json);
        var c = new StateMachineContainer(pool, ctx.StateMachineContext);
        Assert.Throws<InvalidOperationException>(() => c.DeserializeFromNode(node, TestFactory.CreateRegistry()));
    }

    [Fact]
    public void StateMachineContainer_DeserializeFromNode_BlankStackEntry_DoesNotLeakPoolReferences()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "r", "i", "e.json"));
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPopStrategy());
        var json =
            """
            {
              "machines": [
                { "key": "nav", "pushIndex": "sm.push.test", "popIndex": "sm.pop.test", "stack": [""] }
              ]
            }
            """;
        using var node = TestFactory.NodeFromJson(json);
        var container = new StateMachineContainer(pool, ctx.StateMachineContext);

        Assert.Throws<InvalidOperationException>(() =>
            container.DeserializeFromNode(node, TestFactory.CreateRegistry()));

        pool.LogPoolLeaks();
        Assert.DoesNotContain(logger.Warnings, w => w.Contains("refCount", StringComparison.Ordinal));
    }

    [StrategyIndex("sm.swap.push")]
    private sealed class SwapTestPushStrategy : StateMachineStrategyBase
    {
    }

    [StrategyIndex("sm.swap.pop")]
    private sealed class SwapTestPopStrategy : StateMachineStrategyBase
    {
    }
}
