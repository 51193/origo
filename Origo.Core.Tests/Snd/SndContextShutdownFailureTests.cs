using System;
using System.IO;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.DataSource;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;
using Origo.Core.StateMachine;
using Origo.TestSupport;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Regression tests for the workflow teardown invariant: when disposing
///     the previous ProgressRun throws (a reachable path through a throwing
///     quit hook), the context must still clear its ProgressRun reference.
///     A stale reference would expose a disposed progress run to the next
///     save/load request instead of failing with the "no active ProgressRun"
///     contract.
/// </summary>
public class SndContextShutdownFailureTests
{
    [Fact]
    public void Workflow_WhenOldProgressDisposeThrows_ClearsProgressRunReference()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestMemoryFileSystem();
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), fs);
        runtime.SndWorld.RegisterStrategy(() => new NoopPushStrategy());
        runtime.SndWorld.RegisterStrategy(() => new ThrowOnQuitPopStrategy());

        var dataSourceIo = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(
            runtime, dataSourceIo, metaAccess, pathResolver,
            "root", "initial", "entry.json")
        {
            AutoDiscoverStrategies = false
        });

        fs.SeedFile("entry.json",
            """{ "levels": { "main_menu": { "snd_scene": "res://levels/main_menu.json" } }, "main_menu_level": "main_menu" }""");
        fs.SeedFile("res://levels/main_menu.json", "[]");

        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.FlushFrame();

        var foreground = ctx.Runtime.SessionManager.ForegroundSession
            ?? throw new InvalidOperationException("Test setup failed: no foreground session after entry load.");
        var machine = foreground.GetSessionStateMachines().CreateOrGet("boom", NoopPushIndex, ThrowOnQuitPopIndex);
        machine.Push("armed");

        // A second entry workflow disposes the old ProgressRun. The session
        // state machine's quit-time pop hook throws, so Dispose propagates
        // after its cleanup steps complete.
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        var ex = Assert.ThrowsAny<Exception>(() => ctx.FlushFrame());
        Assert.Contains("POP_QUIT_BOOM", ex.Message, StringComparison.Ordinal);

        Assert.Null(ctx.Runtime.SessionManager.ForegroundSession);
        Assert.Null(ctx._progressRun);
    }

    [Fact]
    public void Shutdown_WhenCurrentDeleteFails_RetriesCleanupWithoutReplayingHooks()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestMemoryFileSystem();
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), fs);
        var dataSourceIo = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(
            runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json")
        {
            AutoDiscoverStrategies = false
        });
        fs.SeedFile("entry.json",
            """{ "levels": { "main_menu": { "snd_scene": "res://levels/main_menu.json" } }, "main_menu_level": "main_menu" }""");
        fs.SeedFile("res://levels/main_menu.json", "[]");
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.FlushFrame();
        fs.SeedFile("root/current/retry_probe.txt", "probe");

        fs.BeforeDeleteDirectory = path =>
        {
            if (path.EndsWith("current", StringComparison.Ordinal))
                throw new IOException("simulated current delete failure");
        };

        Assert.Throws<IOException>(() => ctx.Shutdown());
        Assert.Null(ctx.Blackboard.ProgressBlackboard);
        Assert.True(fs.DirectoryExists("root/current"));

        // The retry must only run idempotent cleanup: no progress run exists,
        // and the context becomes terminal after the cleanup succeeds.
        fs.BeforeDeleteDirectory = null;
        ctx.Shutdown();

        Assert.False(fs.DirectoryExists("root/current"));
        ctx.Shutdown();
        Assert.Throws<InvalidOperationException>(() => ctx.Bootstrap());
    }

    private const string NoopPushIndex = "shutdown.noop_push";
    private const string ThrowOnQuitPopIndex = "shutdown.throw_quit_pop";

    [StrategyIndex(NoopPushIndex)]
    private sealed class NoopPushStrategy : StateMachineStrategyBase
    {
    }

    [StrategyIndex(ThrowOnQuitPopIndex)]
    private sealed class ThrowOnQuitPopStrategy : StateMachineStrategyBase
    {
        public override void OnPopBeforeQuit(StateMachineStrategyContext context, IStateMachineContext ctx) =>
            throw new InvalidOperationException("POP_QUIT_BOOM");
    }
}
