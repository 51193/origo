using System;
using System.Collections.Generic;
using Origo.Core.DataSource;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Snd;
using Origo.Core.Save;
using Origo.TestSupport;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Tests that verify the SessionRun Dispose contract: no persist,
///     no BeforeSave, idempotency, and post-dispose state.
/// </summary>
[Collection("StrategyStateTests")]
public class DisposeSemanticsTestsSessionRun
{
    [Fact]
    public void SessionRun_Dispose_DoesNotWriteFilesToCurrent()
    {
        var (ctx, fs) = DisposeSemanticsTestInfrastructure.CreateForegroundContext();

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Spawn(DisposeSemanticsTestInfrastructure.CreateMeta("Entity"));

        var progressRun = ctx.EnsureProgressRun();
        progressRun.PersistProgress();
        bg.Dispose();

        Assert.False(fs.Exists("root/current/level_bg_level/snd_scene.json"));
        Assert.False(fs.Exists("root/current/level_bg_level/session.json"));
        Assert.False(fs.Exists("root/current/level_bg_level/session_state_machines.json"));
    }

    [Fact]
    public void SessionRun_Dispose_DoesNotTriggerBeforeSave()
    {
        var events = new List<string>();
        var (ctx, _) = DisposeSemanticsTestInfrastructure.CreateForegroundContext(world =>
        {
            DisposeSemanticsTestInfrastructure.BeforeSaveSpyStrategy.Bind(events);
            world.RegisterStrategy(() => new DisposeSemanticsTestInfrastructure.BeforeSaveSpyStrategy());
        });

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Spawn(DisposeSemanticsTestInfrastructure.CreateMetaWithIndex("Entity",
            DisposeSemanticsTestInfrastructure.BeforeSaveStrategyIndex));

        events.Clear();
        bg.Dispose();

        Assert.DoesNotContain("BeforeSave:Entity", events);
    }

    [Fact]
    public void SessionRun_Dispose_TriggersBeforeQuit()
    {
        var events = new List<string>();
        var (ctx, _) = DisposeSemanticsTestInfrastructure.CreateForegroundContext(world =>
        {
            DisposeSemanticsTestInfrastructure.BeforeQuitSpyStrategy.Bind(events);
            world.RegisterStrategy(() => new DisposeSemanticsTestInfrastructure.BeforeQuitSpyStrategy());
        });

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Spawn(DisposeSemanticsTestInfrastructure.CreateMetaWithIndex("Entity",
            DisposeSemanticsTestInfrastructure.BeforeQuitStrategyIndex));

        events.Clear();
        bg.Dispose();

        Assert.Contains("BeforeQuit:Entity", events);
    }

    [Fact]
    public void SessionRun_ExplicitPersistLevelState_WritesToCurrent_BeforeDispose()
    {
        var (ctx, fs) = DisposeSemanticsTestInfrastructure.CreateForegroundContext();

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Spawn(DisposeSemanticsTestInfrastructure.CreateMeta("Entity"));
        bg.SessionBlackboard.SetValue("data", 42);

        ctx.Save.RequestSaveGame("explicit1");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists("root/save_explicit1/level_bg_level/snd_scene.json"));
        Assert.True(fs.Exists("root/save_explicit1/level_bg_level/session.json"));
        Assert.True(fs.Exists("root/save_explicit1/level_bg_level/session_state_machines.json"));

        bg.Dispose();
    }

    [Fact]
    public void SessionRun_ExplicitPersistLevelState_TriggersBeforeSave()
    {
        var events = new List<string>();
        var (ctx, _) = DisposeSemanticsTestInfrastructure.CreateForegroundContext(world =>
        {
            DisposeSemanticsTestInfrastructure.BeforeSaveSpyStrategy.Bind(events);
            world.RegisterStrategy(() => new DisposeSemanticsTestInfrastructure.BeforeSaveSpyStrategy());
        });

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Spawn(DisposeSemanticsTestInfrastructure.CreateMetaWithIndex("Entity",
            DisposeSemanticsTestInfrastructure.BeforeSaveStrategyIndex));

        events.Clear();
        ctx.Save.RequestSaveGame("before_save_test");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        Assert.Contains("BeforeSave:Entity", events);
    }

    [Fact]
    public void SessionRun_Dispose_Twice_IsIdempotent()
    {
        var (ctx, _) = DisposeSemanticsTestInfrastructure.CreateForegroundContext();

        var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Dispose();

        var ex = Record.Exception(() => bg.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void SessionRun_Dispose_DisposingSubscriberThrows_PropagatesAndSessionStillReleases()
    {
        var (ctx, _) = DisposeSemanticsTestInfrastructure.CreateForegroundContext();

        var bg = (SessionRun)(SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Spawn(DisposeSemanticsTestInfrastructure.CreateMeta("Entity"));
        bg.Disposing += () => throw new InvalidOperationException("subscriber failure");

        Assert.Throws<InvalidOperationException>(() => bg.Dispose());

        // The dispose state must still be committed: a second dispose is a
        // no-op and post-dispose access fails fast (no half-disposed session).
        var second = Record.Exception(() => bg.Dispose());
        Assert.Null(second);
        Assert.Throws<ObjectDisposedException>(() => bg.SessionBlackboard);
    }

    [Fact]
    public void SessionRun_AfterDispose_SaveDoesNotPersistSessionData()
    {
        var (ctx, fs) = DisposeSemanticsTestInfrastructure.CreateForegroundContext();

        var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Spawn(DisposeSemanticsTestInfrastructure.CreateMeta("DisposedEntity"));
        bg.SessionBlackboard.SetValue("disposed_key", "disposed_val");
        bg.Dispose();

        ctx.Save.RequestSaveGame("after_dispose_save");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        Assert.False(fs.Exists("root/save_after_dispose_save/level_bg_level/snd_scene.json"));
    }

    [Fact]
    public void SessionRun_AfterDispose_SaveExcludesDisposedSession()
    {
        var (ctx, fs) = DisposeSemanticsTestInfrastructure.CreateForegroundContext();

        var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.SessionBlackboard.SetValue("data", 42);
        bg.Spawn(DisposeSemanticsTestInfrastructure.CreateMeta("DisposedEntity"));
        bg.Dispose();

        ctx.Save.RequestSaveGame("exclude_disposed");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        Assert.False(fs.Exists("root/save_exclude_disposed/level_bg_level/snd_scene.json"));
    }

    [Fact]
    public void SessionRun_AfterDispose_SessionBlackboard_ThrowsObjectDisposed()
    {
        var (ctx, _) = DisposeSemanticsTestInfrastructure.CreateForegroundContext();

        var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Dispose();

        Assert.Throws<ObjectDisposedException>(() => bg.SessionBlackboard);
    }

    [Fact]
    public void SessionRun_AfterDispose_SceneHost_ThrowsObjectDisposed()
    {
        var (ctx, _) = DisposeSemanticsTestInfrastructure.CreateForegroundContext();

        var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Dispose();

        Assert.Throws<ObjectDisposedException>(() => bg.FindByName("any"));
    }

    [Fact]
    public void SessionRun_AfterDispose_GetSessionStateMachines_ThrowsObjectDisposed()
    {
        var (ctx, _) = DisposeSemanticsTestInfrastructure.CreateForegroundContext();

        var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Dispose();

        Assert.Throws<ObjectDisposedException>(() => bg.GetSessionStateMachines());
    }

    [Fact]
    public void SessionRun_Dispose_PopHookThrows_SessionMachinesAndEntitiesStillReleased()
    {
        var (ctx, logger) = CreateContext(world =>
        {
            world.StrategyPool.Register(() => new DisposeSemanticsTestInfrastructure.BeforeQuitSpyStrategy());
            world.StrategyPool.Register(() => new DisposeSemanticsTestInfrastructure.PopHookThrowsPushStrategy());
            world.StrategyPool.Register(() => new DisposeSemanticsTestInfrastructure.PopHookThrowsPopStrategy());
        });

        var bg = (SessionRun)(SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Spawn(DisposeSemanticsTestInfrastructure.CreateMetaWithIndex("Entity",
            DisposeSemanticsTestInfrastructure.BeforeQuitStrategyIndex));
        bg.GetSessionStateMachines().CreateOrGet("machine",
            DisposeSemanticsTestInfrastructure.PopHookThrowsPushIndex,
            DisposeSemanticsTestInfrastructure.PopHookThrowsPopIndex).Push("state");

        // A quit-pop hook throws inside Dispose: the exception must propagate
        // (fail-fast), but the session state machines and entity strategies
        // must still be released and the disposed flag committed.
        Assert.Throws<InvalidOperationException>(() => bg.Dispose());

        Assert.Null(Record.Exception(() => bg.Dispose()));
        Assert.Throws<ObjectDisposedException>(() => bg.SessionBlackboard);

        ctx.Runtime.SndWorld.StrategyPool.LogPoolLeaks();
        Assert.DoesNotContain(logger.Warnings, w => w.Contains("refCount"));
    }

    [Fact]
    public void SessionRun_Dispose_DisposingSubscriberThrows_SessionMachinesAndEntitiesStillReleased()
    {
        var (ctx, logger) = CreateContext(world =>
        {
            world.StrategyPool.Register(() => new DisposeSemanticsTestInfrastructure.BeforeQuitSpyStrategy());
            world.StrategyPool.Register(() => new DisposeSemanticsTestInfrastructure.PopHookThrowsPushStrategy());
            world.StrategyPool.Register(() => new DisposeSemanticsTestInfrastructure.PopHookThrowsPopStrategy());
        });

        var bg = (SessionRun)(SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Spawn(DisposeSemanticsTestInfrastructure.CreateMetaWithIndex("Entity",
            DisposeSemanticsTestInfrastructure.BeforeQuitStrategyIndex));
        bg.GetSessionStateMachines().CreateOrGet("machine",
            DisposeSemanticsTestInfrastructure.PopHookThrowsPushIndex,
            DisposeSemanticsTestInfrastructure.PopHookThrowsPopIndex).Push("state");
        bg.Disposing += () => throw new InvalidOperationException("subscriber failure");

        // The disposing-subscriber exception must propagate (fail-fast), but
        // the session state machines and entity strategies must still be
        // released and the disposed flag committed.
        Assert.Throws<InvalidOperationException>(() => bg.Dispose());

        Assert.Null(Record.Exception(() => bg.Dispose()));
        Assert.Throws<ObjectDisposedException>(() => bg.SessionBlackboard);

        ctx.Runtime.SndWorld.StrategyPool.LogPoolLeaks();
        Assert.DoesNotContain(logger.Warnings, w => w.Contains("refCount"));
    }

    [Fact]
    public void SessionRun_Dispose_StateMachineClearThrows_EntitiesStillReleased()
    {
        var (ctx, logger) = CreateContext(world =>
        {
            world.StrategyPool.Register(() => new DisposeSemanticsTestInfrastructure.BeforeQuitSpyStrategy());
            world.StrategyPool.Register(() => new DisposeSemanticsTestInfrastructure.PopHookThrowsPushStrategy());
            world.StrategyPool.Register(() => new DisposeSemanticsTestInfrastructure.PopHookThrowsPopStrategy());
        });

        var bg = (SessionRun)(SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Spawn(DisposeSemanticsTestInfrastructure.CreateMetaWithIndex("Entity",
            DisposeSemanticsTestInfrastructure.BeforeQuitStrategyIndex));
        bg.GetSessionStateMachines().CreateOrGet("machine",
            DisposeSemanticsTestInfrastructure.PopHookThrowsPushIndex,
            DisposeSemanticsTestInfrastructure.PopHookThrowsPopIndex);

        // Sabotage one state machine's push reference so
        // StateMachineContainer.Clear throws after releasing all machines.
        // SessionRun.Dispose must still release entities and commit the
        // disposed flag after that cleanup failure.
        ctx.Runtime.SndWorld.StrategyPool.ReleaseStrategy(
            DisposeSemanticsTestInfrastructure.PopHookThrowsPushIndex);

        Assert.Throws<InvalidOperationException>(() => bg.Dispose());

        Assert.Null(Record.Exception(() => bg.Dispose()));
        Assert.Throws<ObjectDisposedException>(() => bg.SessionBlackboard);

        ctx.Runtime.SndWorld.StrategyPool.LogPoolLeaks();
        Assert.DoesNotContain(logger.Warnings, w => w.Contains("refCount"));
    }

    [Fact]
    public void SessionRun_Dispose_EntityQuitHookThrows_LaterEntitiesStillReleased()
    {
        var (ctx, logger) = CreateContext(world =>
        {
            world.StrategyPool.Register(() => new DisposeSemanticsTestInfrastructure.BeforeQuitSpyStrategy());
            world.StrategyPool.Register(() => new DisposeSemanticsTestInfrastructure.ThrowingQuitStrategy());
        });

        var bg = (SessionRun)(SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Spawn(DisposeSemanticsTestInfrastructure.CreateMetaWithIndex("Throwing",
            DisposeSemanticsTestInfrastructure.ThrowingQuitStrategyIndex));
        bg.Spawn(DisposeSemanticsTestInfrastructure.CreateMetaWithIndex("Normal",
            DisposeSemanticsTestInfrastructure.BeforeQuitStrategyIndex));

        // The first entity's BeforeQuit hook throws mid-release (fail-fast
        // contract: the exception propagates), but the remaining entities'
        // strategies must still be released instead of leaking pool
        // references.
        Assert.Throws<InvalidOperationException>(() => bg.Dispose());

        ctx.Runtime.SndWorld.StrategyPool.LogPoolLeaks();
        Assert.DoesNotContain(logger.Warnings, w => w.Contains("refCount"));
    }

    private static (SndContext ctx, TestLogger logger) CreateContext(
        Action<SndWorld>? configureWorld = null)
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("res://entry/entry.json",
            "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]");
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var tm = new TypeStringMapping();
        var systemBb = new Blackboard.Blackboard();
        var runtime = TestFactory.CreateRuntime(logger, host, tm, systemBb, dataSourceIo);
        configureWorld?.Invoke(runtime.SndWorld);

        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver,
            "root", "res://initial", "res://entry/entry.json"));

        var progressRun = TestFactory.CreateProgressRun(
            "test_save", logger, metaAccess, pathResolver, "root", runtime, ctx, sharedDataSourceIo: dataSourceIo);
        ctx.SetProgressRun(progressRun);

        return (ctx, logger);
    }
}
