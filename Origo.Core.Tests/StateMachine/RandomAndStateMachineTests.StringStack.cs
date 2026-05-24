using System;
using System.Collections.Generic;
using Origo.Core.Snd;
using Origo.Core.StateMachine;
using Xunit;

namespace Origo.Core.Tests;

public partial class RandomAndStateMachineTests
{
    [Fact]
    public void StringStackStateMachine_Snapshot_RestoreStackWithoutHooks_RoundTrip()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var ctx = new SndContext(new SndContextParameters(runtime, fs, "root", "initial", "entry.json"));
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPopStrategy());

        var sm1 = new StackStateMachine("m1", "sm.push.test", "sm.pop.test", pool, ctx);
        sm1.Push("p");
        sm1.Push("q");
        sm1.Push("r");
        var snapshot1 = sm1.Snapshot();

        var sm2 = new StackStateMachine("m2", "sm.push.test", "sm.pop.test", pool, ctx);
        sm2.RestoreStackWithoutHooks(snapshot1);
        var snapshot2 = sm2.Snapshot();

        sm1.Dispose();
        sm2.Dispose();

        Assert.Equal(snapshot1, snapshot2);
    }

    [Fact]
    public void StringStackStateMachine_PushPopRuntime_AfterAddAndBeforeRemove_OrderAndContext()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var ctx = new SndContext(new SndContextParameters(runtime, fs, "root", "initial", "entry.json"));
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPopStrategy());

        var events = new List<string>();
        SmPushStrategy.PushEvents = events;
        SmPopStrategy.PopRemoveEvents = events;
        SmPopStrategy.PopQuitEvents = events;

        try
        {
            var sm = new StackStateMachine("m1", "sm.push.test", "sm.pop.test", pool, ctx);
            sm.Push("a");
            sm.Push("b");
            Assert.True(sm.TryPopRuntime(out var p1));
            Assert.Equal("b", p1);
            Assert.True(sm.TryPopRuntime(out var p2));
            Assert.Equal("a", p2);
            Assert.False(sm.TryPopRuntime(out _));

            sm.Dispose();

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
    public void StringStackStateMachine_Throws_WhenStrategyNotRegistered()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var ctx = new SndContext(new SndContextParameters(runtime, fs, "root", "initial", "entry.json"));

        Assert.Throws<InvalidOperationException>(() =>
            new StackStateMachine("m1", "sm.push.missing", "sm.pop.missing", runtime.SndWorld.StrategyPool, ctx));
    }

    [Fact]
    public void StringStackStateMachine_PushPopOnQuit_AfterAddAndBeforeQuit_OrderAndContext()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var ctx = new SndContext(new SndContextParameters(runtime, fs, "root", "initial", "entry.json"));
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPopStrategy());

        var events = new List<string>();
        SmPushStrategy.PushEvents = events;
        SmPopStrategy.PopRemoveEvents = events;
        SmPopStrategy.PopQuitEvents = events;

        try
        {
            var sm = new StackStateMachine("m1", "sm.push.test", "sm.pop.test", pool, ctx);
            sm.Push("a");
            sm.Push("b");

            Assert.True(sm.TryPopRuntime(out var p1));
            Assert.Equal("b", p1);

            Assert.True(sm.TryPopOnQuit(out var p2));
            Assert.Equal("a", p2);
            Assert.False(sm.TryPopOnQuit(out _));

            sm.Dispose();

            Assert.Equal(
                new[]
                {
                    "push:runtime:null->a",
                    "push:runtime:a->b",
                    "pop:runtime:b->a",
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
    public void StringStackStateMachine_FlushAfterLoad_CallsAfterLoadInPushOrder()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var ctx = new SndContext(new SndContextParameters(runtime, fs, "root", "initial", "entry.json"));
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPopStrategy());

        var events = new List<string>();
        SmPushStrategy.AfterLoadEvents = events;

        try
        {
            var sm = new StackStateMachine("m1", "sm.push.test", "sm.pop.test", pool, ctx);
            sm.RestoreStackWithoutHooks(new[] { "x", "y", "z" });
            sm.FlushAfterLoad();
            sm.Dispose();

            Assert.Equal(
                new[]
                {
                    "push:afterload:null->x",
                    "push:afterload:x->y",
                    "push:afterload:y->z"
                },
                events);
        }
        finally
        {
            ResetStrategyHooks();
        }
    }
}
