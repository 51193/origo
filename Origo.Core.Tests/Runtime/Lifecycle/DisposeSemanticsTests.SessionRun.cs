using System;
using System.Collections.Generic;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Snd;
using Origo.Core.Save;
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

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
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

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
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

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
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

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
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

        using var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
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

        var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Dispose();

        var ex = Record.Exception(() => bg.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void SessionRun_AfterDispose_SaveDoesNotPersistSessionData()
    {
        var (ctx, fs) = DisposeSemanticsTestInfrastructure.CreateForegroundContext();

        var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
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

        var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
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

        var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Dispose();

        Assert.Throws<ObjectDisposedException>(() => bg.SessionBlackboard);
    }

    [Fact]
    public void SessionRun_AfterDispose_SceneHost_ThrowsObjectDisposed()
    {
        var (ctx, _) = DisposeSemanticsTestInfrastructure.CreateForegroundContext();

        var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Dispose();

        Assert.Throws<ObjectDisposedException>(() => bg.FindByName("any"));
    }

    [Fact]
    public void SessionRun_AfterDispose_GetSessionStateMachines_ThrowsObjectDisposed()
    {
        var (ctx, _) = DisposeSemanticsTestInfrastructure.CreateForegroundContext();

        var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        bg.Dispose();

        Assert.Throws<ObjectDisposedException>(() => bg.GetSessionStateMachines());
    }
}
