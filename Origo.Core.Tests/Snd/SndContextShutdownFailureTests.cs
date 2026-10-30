using System;
using System.IO;
using System.Threading;
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
    public void Workflow_QuitHookQueuesRequestThenShutdown_CancelsQueuedWorkBeforeTerminalState()
    {
        // A quit hook can queue deferred work and then request shutdown while
        // the workflow tears the old progress run down. The workflow commits the
        // terminal state after teardown, so it must cancel that queued work
        // first: otherwise a later frame runs the request against the terminal
        // context and the persistence counter never drains.
        var fs = new TestMemoryFileSystem();
        var ctx = CreateEntryContext(fs);
        ctx.Runtime.SndWorld.RegisterStrategy(() => new QueueSaveThenShutdownOnQuitStrategy());
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.FlushFrame();

        var foreground = ctx.Runtime.SessionManager.ForegroundSession
            ?? throw new InvalidOperationException("Test setup failed: no foreground session after entry load.");
        foreground.Spawn(new SndMetaFluentBuilder("queue_then_shutdown_probe")
            .AddLifecycleStrategy(QueueSaveThenShutdownOnQuitStrategy.Index)
            .Build());

        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        var ex = Assert.Throws<InvalidOperationException>(() => ctx.FlushFrame());
        Assert.Contains("shut down", ex.Message, StringComparison.Ordinal);

        Assert.Equal(0, ctx.Deferred.GetPendingPersistenceRequestCount());
        Assert.True(ctx.Deferred.IsPersistenceIdle);
        Assert.False(fs.DirectoryExists("root/save_queued_after_quit"));

        // No deferred action survives to throw from the next frame.
        Assert.Null(Record.Exception(() => ctx.FlushFrame()));
        Assert.Throws<InvalidOperationException>(() => ctx.Save.RequestSaveGame("after_terminated"));
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

    [Fact]
    public void Shutdown_FromAfterSpawnHookDuringWorkflow_IsRejectedAndDoesNotTearDownWorkflow()
    {
        var fs = new TestMemoryFileSystem();
        var ctx = CreateEntryContext(fs);
        ctx.Runtime.SndWorld.RegisterStrategy(() => new ShutdownOnAfterSpawnStrategy());

        // The entry workflow's AfterSpawn hook asks for shutdown while the
        // main-menu workflow body is still mounting the new ProgressRun. The
        // request must be rejected; committing it would dispose the progress
        // run the workflow is still driving.
        fs.SeedFile("res://levels/main_menu.json",
            """
            [
              {
                "name": "EntryProbe",
                "node": { "pairs": {} },
                "strategy": { "lifecycle_indices": ["shutdown.after_spawn"] },
                "data": { "pairs": {} }
              }
            ]
            """);

        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        var ex = Assert.Throws<InvalidOperationException>(() => ctx.FlushFrame());
        Assert.Contains("lifecycle workflow", ex.Message, StringComparison.Ordinal);

        // The rejected shutdown did not commit terminal state: persistence
        // mutations are still accepted. The workflow itself aborted and left
        // no progress run mounted.
        ctx.Save.SetContinueTarget("retry_slot");
        Assert.Null(ctx.Blackboard.ProgressBlackboard);

        ctx.Shutdown();
        Assert.Throws<InvalidOperationException>(() => ctx.Bootstrap());
    }

    [Fact]
    public void Shutdown_FromBeforeSaveHookDuringSaveWorkflow_IsRejectedAndSaveIsNotWritten()
    {
        var fs = new TestMemoryFileSystem();
        var ctx = CreateEntryContext(fs);
        ctx.Runtime.SndWorld.RegisterStrategy(() => new ShutdownOnBeforeSaveStrategy());
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.FlushFrame();

        var foreground = ctx.Runtime.SessionManager.ForegroundSession
            ?? throw new InvalidOperationException("Test setup failed: no foreground session after entry load.");
        foreground.Spawn(new SndMetaFluentBuilder("save_probe")
            .AddLifecycleStrategy(ShutdownOnBeforeSaveStrategy.Index)
            .Build());

        ctx.Save.RequestSaveGame("forbidden_save");
        var ex = Assert.Throws<InvalidOperationException>(() => ctx.FlushFrame());
        Assert.Contains("BeforeSave hook", ex.Message, StringComparison.Ordinal);

        // Rejected before any snapshot write; the foreground session survives.
        Assert.False(fs.DirectoryExists("root/save_forbidden_save"));
        Assert.DoesNotContain("forbidden_save", ctx.Save.ListSaves());
        var stillMounted = ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(stillMounted);
        Assert.NotNull(stillMounted!.FindByName("save_probe"));

        ctx.Shutdown();
        Assert.Null(ctx.Blackboard.ProgressBlackboard);
    }

    [Fact]
    public void Shutdown_FromSwitchAfterLoadHook_IsRejectedAndSwitchCompletes()
    {
        var fs = new TestMemoryFileSystem();
        var ctx = CreateEntryContext(fs);
        ctx.Runtime.SndWorld.RegisterStrategy(() => new TryShutdownOnAfterLoadStrategy());
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.FlushFrame();

        // Seed the target-level payload into current/ AFTER the entry workflow
        // (that workflow deletes current/ at start), so the switch loads real
        // entities and their AfterLoad hooks run inside the switch workflow.
        fs.SeedFile("root/current/level_level_a/snd_scene.json",
            """
            [
              {
                "name": "SwitchProbe",
                "node": { "pairs": {} },
                "strategy": { "lifecycle_indices": ["shutdown.switch_after_load"] },
                "data": { "pairs": {} }
              }
            ]
            """);
        fs.SeedFile("root/current/level_level_a/session.json", "{}");
        fs.SeedFile("root/current/level_level_a/session_state_machines.json", """{"machines":[]}""");

        TryShutdownOnAfterLoadStrategy.ShutdownRejection = null;
        ctx.Save.RequestSwitchForegroundLevel("level_a");
        ctx.FlushFrame();

        // The hook's shutdown request was rejected with the workflow guard, so
        // the switch completes and the run stays live.
        var rejection = TryShutdownOnAfterLoadStrategy.ShutdownRejection;
        Assert.NotNull(rejection);
        Assert.Contains("lifecycle workflow", rejection!, StringComparison.Ordinal);
        var foreground = ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(foreground);
        Assert.Equal("level_a", foreground!.LevelId);
        Assert.NotNull(foreground.FindByName("SwitchProbe"));
        Assert.NotNull(ctx.Blackboard.ProgressBlackboard);
        ctx.Save.SetContinueTarget("after_switch");

        ctx.Shutdown();
        Assert.Null(ctx.Blackboard.ProgressBlackboard);
    }

    [Fact]
    public void Switch_WhenDeferredActionThrows_ReleasesWorkflowGuardForLaterWorkflows()
    {
        var fs = new TestMemoryFileSystem();
        var ctx = CreateEntryContext(fs);
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.FlushFrame();

        // Seed a target level with an unreadable scene so the deferred switch
        // action fails while loading the new foreground.
        fs.SeedFile("root/current/level_broken/snd_scene.json", "{invalid}");
        fs.SeedFile("root/current/level_broken/session.json", "{}");
        fs.SeedFile("root/current/level_broken/session_state_machines.json", """{"machines":[]}""");

        // A valid recovery target for the workflow after the failed switch.
        fs.SeedFile("root/current/level_recovered/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_recovered/session.json", "{}");
        fs.SeedFile("root/current/level_recovered/session_state_machines.json", """{"machines":[]}""");

        // The level id "broken" maps to current/level_broken/snd_scene.json.
        ctx.Save.RequestSwitchForegroundLevel("broken");
        var failure = Assert.Throws<InvalidOperationException>(() => ctx.FlushFrame());
        Assert.DoesNotContain("already in progress", failure.Message, StringComparison.Ordinal);

        // The failed switch released the lifecycle-workflow guard: a subsequent
        // switch, save, and shutdown all run without an "already in progress"
        // error (the run itself stays alive).
        ctx.Save.RequestSwitchForegroundLevel("recovered");
        Assert.Null(Record.Exception(() => ctx.FlushFrame()));
        var foreground = ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(foreground);
        Assert.Equal("recovered", foreground!.LevelId);

        ctx.Save.RequestSaveGame("after_failed_switch");
        Assert.Null(Record.Exception(() => ctx.FlushFrame()));
        Assert.Contains("after_failed_switch", ctx.Save.ListSaves());

        ctx.Shutdown();
        Assert.Null(ctx.Blackboard.ProgressBlackboard);
    }

    private static SndContext CreateEntryContext(TestMemoryFileSystem fs)
    {
        var logger = new TestLogger();
        // Full-memory adapter host so foreground entities are real SndEntity
        // instances whose AfterSpawn/BeforeSave hooks actually run. The bare
        // TestSndSceneHost creates lifecycle-less dummy entities.
        var host = new FullMemorySndSceneHost(logger);
        var runtime = TestFactory.CreateRuntime(
            logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), fs);
        host.BindWorld(runtime.SndWorld);
        var dataSourceIo = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(
            runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json")
        {
            AutoDiscoverStrategies = false
        });
        host.BindContext(ctx);
        fs.SeedFile("entry.json",
            """{ "levels": { "main_menu": { "snd_scene": "res://levels/main_menu.json" } }, "main_menu_level": "main_menu" }""");
        fs.SeedFile("res://levels/main_menu.json", "[]");
        return ctx;
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

    [StrategyIndex("shutdown.queue_save_then_shutdown")]
    private sealed class QueueSaveThenShutdownOnQuitStrategy : LifecycleStrategyBase
    {
        public const string Index = "shutdown.queue_save_then_shutdown";

        public override void BeforeQuit(ISndEntity entity, ISndContext ctx)
        {
            _ = ctx.Save.RequestSaveGameAuto("queued_after_quit");
            ctx.Shutdown();
        }
    }

    [StrategyIndex("shutdown.after_spawn")]
    private sealed class ShutdownOnAfterSpawnStrategy : LifecycleStrategyBase
    {
        public override void AfterSpawn(ISndEntity entity, ISndContext ctx) => ctx.Shutdown();
    }

    [StrategyIndex("shutdown.before_save")]
    private sealed class ShutdownOnBeforeSaveStrategy : LifecycleStrategyBase
    {
        public const string Index = "shutdown.before_save";

        public override void BeforeSave(ISndEntity entity, ISndContext ctx) => ctx.Shutdown();
    }

    /// <summary>
    ///     AfterLoad hook that requests shutdown and records the rejection, so a
    ///     level switch can complete while proving the workflow guard fired.
    /// </summary>
    [StrategyIndex("shutdown.switch_after_load")]
    private sealed class TryShutdownOnAfterLoadStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<string?> _shutdownRejection = new();

        public static string? ShutdownRejection
        {
            get => _shutdownRejection.Value;
            set => _shutdownRejection.Value = value;
        }

        public override void AfterLoad(ISndEntity entity, ISndContext ctx)
        {
            try
            {
                ctx.Shutdown();
            }
            catch (InvalidOperationException ex)
            {
                ShutdownRejection = ex.Message;
            }
        }
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
