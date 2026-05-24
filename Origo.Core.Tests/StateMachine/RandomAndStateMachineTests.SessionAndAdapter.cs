using System.Collections.Generic;
using Origo.Core.Snd;
using Origo.Core.StateMachine;
using Xunit;

namespace Origo.Core.Tests;

public partial class RandomAndStateMachineTests
{
    [Fact]
    public void SessionRun_Dispose_InvokesPopAllOnQuit_TopToBottom()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var ctx = new SndContext(new SndContextParameters(runtime, fs, "root", "res://initial",
            "res://entry/entry.json"));
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPopStrategy());

        var events = new List<string>();
        SmPushStrategy.PushEvents = events;
        SmPopStrategy.PopRemoveEvents = events;
        SmPopStrategy.PopQuitEvents = events;

        try
        {
            var progressRun = TestFactory.CreateProgressRun("001", logger, fs, "root", runtime, ctx);
            ctx.SetProgressRun(progressRun);
            progressRun.LoadAndMountForeground("default");

            using var run = ctx.SessionManager.CreateBackgroundSession("bg", "bg");

            var sm = run.GetSessionStateMachines().CreateOrGet("ui", "sm.push.test", "sm.pop.test");
            sm.Push("a");
            sm.Push("b");

            run.Dispose();

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
    public void StateMachineStrategyContext_HoldsMachineKeyAndStackSnapshot()
    {
        var ctx = new StateMachineStrategyContext("mk", null, "top");
        Assert.Equal("mk", ctx.MachineKey);
        Assert.Null(ctx.BeforeTop);
        Assert.Equal("top", ctx.AfterTop);
    }
}
