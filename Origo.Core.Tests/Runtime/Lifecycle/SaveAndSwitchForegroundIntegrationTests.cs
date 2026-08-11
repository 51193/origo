using Origo.Core.Runtime.Lifecycle;
using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Xunit;
using Origo.Core.Abstractions.Lifecycle;
using static Origo.Core.Tests.SaveAndSwitchForegroundTestInfrastructure;

namespace Origo.Core.Tests;

[Collection("StrategyStateTests")]
public class SaveAndSwitchForegroundIntegrationTests
{
    // ── FullMemorySndSceneHost: FindByName during hooks ──────────────────

    [Fact]
    public void FullMemorySndSceneHost_Spawn_FindByName_FindsSelfDuringAfterSpawn()
    {
        var events = new List<string>();
        var (ctx, _) = CreateForegroundContext(world =>
        {
            FindByNameStrategy.Bind(events);
            world.RegisterStrategy(() => new FindByNameStrategy());
        });

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        var host = (FullMemorySndSceneHost)((SessionRun)bg).SceneHost;

        var entityA = host.CreateEntity(CreateMetaWithStrategy("EntityA",
            [FindByNameStrategyIndex]));
        if (entityA is IEntityLifecycle lc)
            lc.FireAfterSpawnHooks();

        Assert.Contains($"{AfterSpawnEventPrefix}EntityA:self=true", events);
        Assert.NotNull(host.FindByName("EntityA"));
    }

    [Fact]
    public void FullMemorySndSceneHost_Spawn_FindByName_FindsSiblingsDuringAfterSpawn()
    {
        var events = new List<string>();
        var (ctx, _) = CreateForegroundContext(world =>
        {
            FindByNameStrategy.Bind(events);
            world.RegisterStrategy(() => new FindByNameStrategy());
        });

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        var host = (FullMemorySndSceneHost)((SessionRun)bg).SceneHost;

        host.CreateEntity(CreateMetaWithStrategy("EntityA"));
        var entityB = host.CreateEntity(CreateMetaWithStrategy("EntityB",
            [FindByNameStrategyIndex]));
        if (entityB is IEntityLifecycle lc)
            lc.FireAfterSpawnHooks();

        Assert.Contains($"{AfterSpawnEventPrefix}EntityB:sibling=EntityA", events);
    }

    [Fact]
    public void FullMemorySndSceneHost_LoadFromMetaList_FindByName_FindsSelfDuringAfterLoad()
    {
        var events = new List<string>();
        var (ctx, _) = CreateForegroundContext(world =>
        {
            FindByNameStrategy.Bind(events);
            world.RegisterStrategy(() => new FindByNameStrategy());
        });

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        var host = (FullMemorySndSceneHost)((SessionRun)bg).SceneHost;

        host.RecoverFromMetaList(
        [
            CreateMetaWithStrategy("EntityC", [FindByNameStrategyIndex])
        ]);
        foreach (var e in host.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireAfterLoadHooks();

        Assert.Contains($"{AfterLoadEventPrefix}EntityC:self=true", events);
        Assert.NotNull(host.FindByName("EntityC"));
    }

    [Fact]
    public void FullMemorySndSceneHost_LoadFromMetaList_FindByName_FindsSiblingsDuringAfterLoad()
    {
        var events = new List<string>();
        var (ctx, _) = CreateForegroundContext(world =>
        {
            FindByNameStrategy.Bind(events);
            world.RegisterStrategy(() => new FindByNameStrategy());
        });

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");
        var host = (FullMemorySndSceneHost)((SessionRun)bg).SceneHost;

        host.RecoverFromMetaList(
        [
            CreateMetaWithStrategy("EntityD"),
            CreateMetaWithStrategy("EntityE", [FindByNameStrategyIndex])
        ]);
        foreach (var e in host.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireAfterLoadHooks();

        Assert.Contains($"{AfterLoadEventPrefix}EntityE:sibling=EntityD", events);
    }

    // ── Core: save background session, then switch foreground ──────────

    [Fact]
    public void SaveBackgroundWithEntities_ThenSwitchForeground_LoadsEntitiesIntoForeground()
    {
        var (ctx, fs) = CreateForegroundContext();

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "game", true);
        bg.Spawn(CreateMeta("BgEntity1"));
        bg.Spawn(CreateMeta("BgEntity2"));
        bg.SessionBlackboard.SetValue("bg_value", 42);

        ctx.Save.RequestSaveGameAuto();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        ctx.Runtime.SessionManager.DestroySession("bg");
        ctx.Save.RequestSwitchForegroundLevel("game");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        var fg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("game", fg.LevelId);

        var entities = fg.GetEntities();
        Assert.Equal(2, entities.Count);
        Assert.Contains(entities, e => e.Name == "BgEntity1");
        Assert.Contains(entities, e => e.Name == "BgEntity2");
    }

    [Fact]
    public void SaveBackgroundWithEntities_ThenSwitchForeground_PreservesBlackboard()
    {
        var (ctx, _) = CreateForegroundContext();

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "game", true);
        bg.Spawn(CreateMeta("BgEntity"));
        bg.SessionBlackboard.SetValue("key_int", 100);
        bg.SessionBlackboard.SetValue("key_str", "hello");

        ctx.Save.RequestSaveGameAuto();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        ctx.Runtime.SessionManager.DestroySession("bg");
        ctx.Save.RequestSwitchForegroundLevel("game");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        var fg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);

        var (foundInt, intValue) = fg.SessionBlackboard.TryGet<int>("key_int");
        Assert.True(foundInt);
        Assert.Equal(100, intValue);

        var (foundStr, strValue) = fg.SessionBlackboard.TryGet<string>("key_str");
        Assert.True(foundStr);
        Assert.Equal("hello", strValue);
    }

    [Fact]
    public void SaveBackgroundWithEntities_ThenSwitchForeground_LevelIdMustNotConflict()
    {
        var (ctx, _) = CreateForegroundContext();

        using var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "game", true);
        bg.Spawn(CreateMeta("BgEntity"));
        bg.SessionBlackboard.SetValue("bg_only", 99);

        ctx.Save.RequestSaveGameAuto();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        ctx.Runtime.SessionManager.DestroySession("bg");
        ctx.Save.RequestSwitchForegroundLevel("game");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        var stillAlive = ctx.Runtime.SessionManager.TryGet("bg");
        Assert.Null(stillAlive);

        var fg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        Assert.Equal("game", fg.LevelId);
    }
}
