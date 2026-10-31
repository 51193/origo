using System;
using System.Threading;
using Origo.Core;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Snd;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Origo.TestSupport;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Regression tests for shutdown attempts raised from inside a frame's
///     session processing, spawn, or kill sweep. The progress run still owns the
///     host iteration, so shutdown must be rejected before it can dispose the
///     run and empty the host mid-operation.
/// </summary>
public class SndContextShutdownGuardTests
{
    [Fact]
    public void DriveFrame_ShutdownFromProcess_IsRejectedWithoutCorruptingScene()
    {
        using var host = CreateHost(() => new ShutdownOnProcessStrategy());
        var session = host.Runtime.SessionManager.ForegroundSession
            ?? throw new InvalidOperationException("Test setup failed: no foreground session.");

        var probe = session.Spawn(new SndMetaFluentBuilder("process_shutdown_probe")
            .AddLifecycleStrategy(ShutdownOnProcessStrategy.Index)
            .Build());
        ShutdownOnProcessStrategy.Arm();

        var ex = Assert.Throws<InvalidOperationException>(() => host.DriveFrame(0.016));
        Assert.Contains("processed", ex.Message, StringComparison.Ordinal);

        // The context is not terminal and the scene host still owns the entity;
        // a later frame must run normally once the strategy stops requesting
        // shutdown.
        Assert.NotNull(host.Runtime.SessionManager.ForegroundSession);
        Assert.Same(probe, session.FindByName("process_shutdown_probe"));
        Assert.Null(Record.Exception(() => host.DriveFrame(0.016)));
    }

    [Fact]
    public void DriveFrame_ShutdownFromBeforeDead_IsRejectedAndBeforeQuitDoesNotRun()
    {
        using var host = CreateHost(() => new ShutdownOnBeforeDeadStrategy());
        var session = host.Runtime.SessionManager.ForegroundSession
            ?? throw new InvalidOperationException("Test setup failed: no foreground session.");

        session.Spawn(new SndMetaFluentBuilder("before_dead_shutdown_probe")
            .AddLifecycleStrategy(ShutdownOnBeforeDeadStrategy.Index)
            .Build());
        ShutdownOnBeforeDeadStrategy.Reset();
        ShutdownOnBeforeDeadStrategy.Arm();
        session.RequestKillEntity("before_dead_shutdown_probe");

        var ex = Assert.Throws<InvalidOperationException>(() => host.DriveFrame(0.016));
        Assert.Contains("killed", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, ShutdownOnBeforeDeadStrategy.BeforeQuitCount);
        Assert.Null(session.FindByName("before_dead_shutdown_probe"));
        Assert.Null(Record.Exception(() => host.DriveFrame(0.016)));
    }

    [Fact]
    public void Spawn_ShutdownFromAfterSpawn_IsRejectedAndEntityIsRolledBack()
    {
        using var host = CreateHost(() => new ShutdownOnAfterSpawnStrategy());
        var session = host.Runtime.SessionManager.ForegroundSession
            ?? throw new InvalidOperationException("Test setup failed: no foreground session.");

        var ex = Assert.Throws<InvalidOperationException>(() => session.Spawn(
            new SndMetaFluentBuilder("after_spawn_shutdown_probe")
                .AddLifecycleStrategy(ShutdownOnAfterSpawnStrategy.Index)
                .Build()));
        Assert.Contains("spawned", ex.Message, StringComparison.Ordinal);
        Assert.Null(session.FindByName("after_spawn_shutdown_probe"));
        Assert.NotNull(host.Runtime.SessionManager.ForegroundSession);
        Assert.Null(Record.Exception(() => host.DriveFrame(0.016)));
    }

    private static OrigoHost CreateHost<TStrategy>(Func<TStrategy> strategyFactory)
        where TStrategy : LifecycleStrategyBase
    {
        var fs = new TestMemoryFileSystem();
        var host = OrigoHost.Create(new OrigoHostOptions
        {
            Meta = new OrigoMeta("ShutdownGuard", "1.0.0", "test"),
            FileSystem = fs,
            SaveRootPath = "root",
            InitialSaveRootPath = "res://initial",
            EntryConfigPath = "entry.json",
            AutoDiscoverStrategies = false,
        });
        fs.SeedFile("entry.json",
            """{ "levels": { "main_menu": { "snd_scene": "res://levels/main_menu.json" } }, "main_menu_level": "main_menu" }""");
        fs.SeedFile("res://levels/main_menu.json", "[]");
        host.Runtime.SndWorld.RegisterStrategy(strategyFactory);
        host.Bootstrap();
        host.DriveFrame(0.016);
        return host;
    }

    [StrategyIndex("shutdown.guard.process")]
    private sealed class ShutdownOnProcessStrategy : LifecycleStrategyBase
    {
        public const string Index = "shutdown.guard.process";
        private static readonly AsyncLocal<bool> _armed = new();

        public static void Arm() => _armed.Value = true;

        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            if (!_armed.Value)
                return;
            _armed.Value = false;
            ctx.Shutdown();
        }
    }

    [StrategyIndex("shutdown.guard.before_dead")]
    private sealed class ShutdownOnBeforeDeadStrategy : LifecycleStrategyBase
    {
        public const string Index = "shutdown.guard.before_dead";
        private static readonly AsyncLocal<bool> _armed = new();
        private static readonly AsyncLocal<int> _beforeQuitCount = new();

        public static int BeforeQuitCount => _beforeQuitCount.Value;

        public static void Arm() => _armed.Value = true;

        public static void Reset()
        {
            _armed.Value = false;
            _beforeQuitCount.Value = 0;
        }

        public override void BeforeDead(ISndEntity entity, ISndContext ctx)
        {
            if (!_armed.Value)
                return;
            _armed.Value = false;
            ctx.Shutdown();
        }

        public override void BeforeQuit(ISndEntity entity, ISndContext ctx) => _beforeQuitCount.Value++;
    }

    [StrategyIndex("shutdown.guard.after_spawn")]
    private sealed class ShutdownOnAfterSpawnStrategy : LifecycleStrategyBase
    {
        public const string Index = "shutdown.guard.after_spawn";

        public override void AfterSpawn(ISndEntity entity, ISndContext ctx) => ctx.Shutdown();
    }
}
