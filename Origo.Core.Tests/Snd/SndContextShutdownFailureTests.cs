using System;
using System.IO;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.DataSource;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd.Metadata;
using Origo.Core;
using Origo.Core.Snd;
using Origo.Core.Snd.Scene;
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
    public void Workflow_ShutdownFromQuitHook_WhenCurrentDeleteFails_RemainsRetryable()
    {
        var fs = new TestMemoryFileSystem();
        var host = OrigoHost.Create(new OrigoHostOptions
        {
            Meta = new OrigoMeta("ShutdownRetry", "1.0.0", "test"),
            FileSystem = fs,
            SaveRootPath = "root",
            InitialSaveRootPath = "res://initial",
            EntryConfigPath = "entry.json",
            AutoDiscoverStrategies = false,
        });
        fs.SeedFile("entry.json",
            """{ "levels": { "main_menu": { "snd_scene": "res://levels/main_menu.json" } }, "main_menu_level": "main_menu" }""");
        fs.SeedFile("res://levels/main_menu.json", "[]");
        host.Runtime.SndWorld.RegisterStrategy(() => new ShutdownOnQuitStrategy());
        host.Bootstrap();
        host.DriveFrame(0.016);
        host.Runtime.SessionManager.ForegroundSession!.Spawn(new SndMetaFluentBuilder("shutdown_retry_probe")
            .AddLifecycleStrategy(ShutdownOnQuitStrategy.Index)
            .Build());
        fs.SeedFile("root/current/retry_probe.txt", "probe");
        fs.BeforeDeleteDirectory = path =>
        {
            if (path.EndsWith("current", StringComparison.Ordinal))
                throw new IOException("simulated current delete failure");
        };

        host.Context.Lifecycle.RequestLoadMainMenuEntrySave();
        Assert.Throws<IOException>(() => host.DriveFrame(0.016));
        Assert.True(fs.DirectoryExists("root/current"));

        fs.BeforeDeleteDirectory = null;
        host.Dispose();
        Assert.False(fs.DirectoryExists("root/current"));
    }

    [Fact]
    public void Shutdown_DuringSwitchTeardown_ThrowsBeforeTouchingProgress()
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

        ctx.BeginSwitchTeardown();
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => ctx.Shutdown());
            Assert.Contains("level switch", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            ctx.EndSwitchTeardown();
        }

        ctx.Shutdown();
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

    [Fact]
    public void Shutdown_ReenteredFromQuitHook_DoesNotCommitTerminalStateBeforeOuterCleanup()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestMemoryFileSystem();
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), fs);
        runtime.SndWorld.RegisterStrategy(() => new NoopPushStrategy());
        runtime.SndWorld.RegisterStrategy(() => new ShutdownOnQuitPopStrategy());
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
        var foreground = ctx.Runtime.SessionManager.ForegroundSession
            ?? throw new InvalidOperationException("Test setup failed: no foreground session after entry load.");
        var machine = foreground.GetSessionStateMachines().CreateOrGet(
            "reentrant", NoopPushIndex, ShutdownPopIndex);
        machine.Push("armed");
        fs.SeedFile("root/current/reentrant_probe.txt", "probe");
        ShutdownOnQuitPopStrategy.Context = ctx;

        fs.BeforeDeleteDirectory = path =>
        {
            if (path.EndsWith("current", StringComparison.Ordinal))
                throw new IOException("simulated current delete failure");
        };
        try
        {
            Assert.Throws<IOException>(() => ctx.Shutdown());
            Assert.True(fs.DirectoryExists("root/current"));

            fs.BeforeDeleteDirectory = null;
            ctx.Shutdown();
            Assert.False(fs.DirectoryExists("root/current"));
            ctx.Shutdown();
            Assert.Throws<InvalidOperationException>(() => ctx.Bootstrap());
        }
        finally
        {
            ShutdownOnQuitPopStrategy.Context = null;
        }
    }

    private const string NoopPushIndex = "shutdown.noop_push";
    private const string ShutdownPopIndex = "shutdown.shutdown_pop";
    private const string ThrowOnQuitPopIndex = "shutdown.throw_quit_pop";

    [StrategyIndex("shutdown.quit_shutdown")]
    private sealed class ShutdownOnQuitStrategy : LifecycleStrategyBase
    {
        public const string Index = "shutdown.quit_shutdown";

        public override void BeforeQuit(ISndEntity entity, ISndContext ctx) => ctx.Shutdown();
    }

    [StrategyIndex(NoopPushIndex)]
    private sealed class NoopPushStrategy : StateMachineStrategyBase
    {
    }

    [StrategyIndex(ShutdownPopIndex)]
    private sealed class ShutdownOnQuitPopStrategy : StateMachineStrategyBase
    {
        public static ISndContext? Context;

        public override void OnPopBeforeQuit(StateMachineStrategyContext context, IStateMachineContext ctx) =>
            Context?.Shutdown();
    }

    [StrategyIndex(ThrowOnQuitPopIndex)]
    private sealed class ThrowOnQuitPopStrategy : StateMachineStrategyBase
    {
        public override void OnPopBeforeQuit(StateMachineStrategyContext context, IStateMachineContext ctx) =>
            throw new InvalidOperationException("POP_QUIT_BOOM");
    }
}
