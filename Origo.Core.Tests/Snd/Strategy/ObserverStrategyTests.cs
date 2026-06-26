using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Snd;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class ObserverStrategyTests
{
    private const string SelfWatchIdx = "observer.test.self_watch";
    private const string MultiKeyIdx = "observer.test.multi_key";
    private const string NoDataKeyIdx = "observer.test.no_data_key";
    private const string NamedTargetIdx = "observer.test.named_target";
    private const string MemoryObservedIdx = "observer.test.memory";
    private const string ThrowOnMountIdx = "observer.test.throw_on_mount";

    // ── Registration ───────────────────────────────────────────────────

    [Fact]
    public void ObserverStrategy_CanBeRegistered()
    {
        var world = TestFactory.CreateSndWorld();
        world.RegisterStrategy(() => new SelfWatchObserver());

        Assert.Contains(SelfWatchIdx, world.GetRegisteredStrategyIndices());
    }

    [Fact]
    public void ObserverStrategy_StatelessEnforcement()
    {
        var world = TestFactory.CreateSndWorld();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            world.RegisterStrategy(() => new StatefulObserver()));
        Assert.Contains("invalid instance members", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ObserverStrategy_MissingAttribute_Throws()
    {
        var world = TestFactory.CreateSndWorld();

        Assert.Throws<InvalidOperationException>(() =>
            world.RegisterStrategy(() => new UnannotatedObserver()));
    }

    // ── Mount / Unmount lifecycle ──────────────────────────────────────

    [Fact]
    public void Mount_TriggersOnMounted_WithCorrectParameters()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());
        MemoryObserver.MountedCalls.Clear();
        MemoryObserver.UnmountedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, MemoryObservedIdx);

        Assert.Single(MemoryObserver.MountedCalls);
        var call = MemoryObserver.MountedCalls[0];
        Assert.Equal(entity.Name, call.Entity.Name);
        Assert.Equal(entity.Name, call.Target.Name);
    }

    [Fact]
    public void Unmount_TriggersOnUnmounted_WithCorrectParameters()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());
        MemoryObserver.MountedCalls.Clear();
        MemoryObserver.UnmountedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, MemoryObservedIdx);
        entity.UnmountObserverStrategy(entity.Name, MemoryObservedIdx);

        Assert.Single(MemoryObserver.UnmountedCalls);
    }

    [Fact]
    public void Unmount_WithoutPriorMount_DoesNotThrow()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());

        var ex = Record.Exception(() =>
            entity.UnmountObserverStrategy(entity.Name, MemoryObservedIdx));

        Assert.Null(ex);
    }

    [Fact]
    public void Mount_Duplicate_DoesNotThrow()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());
        MemoryObserver.MountedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, MemoryObservedIdx);
        entity.MountObserverStrategy(entity.Name, MemoryObservedIdx);

        Assert.Equal(2, MemoryObserver.MountedCalls.Count);
    }

    [Fact]
    public void Unmount_OnlyRemovesOneInstance()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());
        MemoryObserver.UnmountedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, MemoryObservedIdx);
        entity.MountObserverStrategy(entity.Name, MemoryObservedIdx);
        entity.UnmountObserverStrategy(entity.Name, MemoryObservedIdx);

        Assert.Single(MemoryObserver.UnmountedCalls);
    }

    // ── Mount failure cleanup ──────────────────────────────────────────

    [Fact]
    public void Mount_WhenOnMountedThrows_RemovesBindingAndSubscription()
    {
        var (entity, _) = Setup();
        entity.SpawnSingle(CreateMeta());
        ThrowOnMountObserver.DataChangedCalls.Clear();

        Assert.Throws<InvalidOperationException>(() =>
            entity.MountObserverStrategy(entity.Name, ThrowOnMountIdx));

        // Subscription was cleaned up: changing the observed key must not reach
        // the observer whose mount failed.
        entity.SetData("character.hp", 42);
        Assert.Empty(ThrowOnMountObserver.DataChangedCalls);

        // Binding was removed: death teardown must not double-release the
        // strategy that the failed mount already returned to the pool.
        var ex = Record.Exception(() => entity.DeadSingle());
        Assert.Null(ex);
    }

    // ── Data change notification ───────────────────────────────────────

    [Fact]
    public void SetData_TriggersOnDataChanged_ForObservedKey()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());
        SelfWatchObserver.DataChangedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, SelfWatchIdx);
        entity.SetData("character.hp", 50);

        Assert.Single(SelfWatchObserver.DataChangedCalls);
        Assert.Equal("character.hp", SelfWatchObserver.DataChangedCalls[0].DataKey);
    }

    [Fact]
    public void SetData_DoesNotTrigger_ForUnobservedKey()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());
        SelfWatchObserver.DataChangedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, SelfWatchIdx);
        entity.SetData("character.mp", 20);

        Assert.Empty(SelfWatchObserver.DataChangedCalls);
    }

    [Fact]
    public void SetData_DoesNotTrigger_AfterUnmount()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());
        SelfWatchObserver.DataChangedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, SelfWatchIdx);
        entity.UnmountObserverStrategy(entity.Name, SelfWatchIdx);
        entity.SetData("character.hp", 50);

        Assert.Empty(SelfWatchObserver.DataChangedCalls);
    }

    [Fact]
    public void SetData_TriggersForMultipleKeys()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());
        MultiKeyObserver.HpChangedCalls.Clear();
        MultiKeyObserver.MpChangedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, MultiKeyIdx);
        entity.SetData("character.hp", 30);
        entity.SetData("character.mp", 10);

        Assert.Single(MultiKeyObserver.HpChangedCalls);
        Assert.Single(MultiKeyObserver.MpChangedCalls);
    }

    [Fact]
    public void SetData_OldAndNewValuesCorrect()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());
        SelfWatchObserver.DataChangedCalls.Clear();

        entity.SetData("character.hp", 100);
        entity.MountObserverStrategy(entity.Name, SelfWatchIdx);
        entity.SetData("character.hp", 50);

        Assert.Single(SelfWatchObserver.DataChangedCalls);
    }

    // ── Observer strategy without data keys ─────────────────────────────

    [Fact]
    public void NoDataKeyObserver_CanMountAndUnmount()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());

        var ex1 = Record.Exception(() =>
            entity.MountObserverStrategy(entity.Name, NoDataKeyIdx));
        Assert.Null(ex1);

        var ex2 = Record.Exception(() =>
            entity.UnmountObserverStrategy(entity.Name, NoDataKeyIdx));
        Assert.Null(ex2);
    }

    // ── ObserverMetaData serialization ──────────────────────────────────

    [Fact]
    public void BuildMetaData_IncludesObserverBindings()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());
        entity.MountObserverStrategy(entity.Name, SelfWatchIdx);

        var meta = entity.SaveSingle();

        Assert.NotNull(meta.StrategyMetaData);
        Assert.Single(meta.StrategyMetaData.ObserverBindings);
        Assert.Equal(entity.Name, meta.StrategyMetaData.ObserverBindings[0].Target);
        Assert.Contains(SelfWatchIdx, meta.StrategyMetaData.ObserverBindings[0].ObserverIndices);
    }

    [Fact]
    public void BuildMetaData_EmptyBindings_WhenNoObservers()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());

        var meta = entity.SaveSingle();

        Assert.NotNull(meta.StrategyMetaData);
        Assert.Empty(meta.StrategyMetaData.ObserverBindings);
    }

    [Fact]
    public void BuildMetaData_MultipleTargets_GroupedCorrectly()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());
        entity.MountObserverStrategy(entity.Name, SelfWatchIdx);
        entity.MountObserverStrategy(entity.Name, MultiKeyIdx);

        var meta = entity.SaveSingle();

        Assert.Single(meta.StrategyMetaData!.ObserverBindings);
        Assert.Equal(2, meta.StrategyMetaData.ObserverBindings[0].ObserverIndices.Count);
    }

    // ── Lifecycle: observer strategies released on dead ─────────────────

    [Fact]
    public void Dead_ReleasesObserverStrategies()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());
        entity.MountObserverStrategy(entity.Name, SelfWatchIdx);
        SelfWatchObserver.DataChangedCalls.Clear();

        entity.DeadSingle();

        // After dead, no data change notifications should fire
        // (entity is dead, no observer callbacks)
        Assert.Empty(SelfWatchObserver.DataChangedCalls);
    }

    [Fact]
    public void Dead_TriggersOnUnmounted()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());
        MemoryObserver.MountedCalls.Clear();
        MemoryObserver.UnmountedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, MemoryObservedIdx);
        entity.DeadSingle();

        Assert.Single(MemoryObserver.UnmountedCalls);
    }

    // ── ObservedData attribute extraction ───────────────────────────────

    [Fact]
    public void ObserveDataAttribute_ExtractsKeys()
    {
        var keys = ObserverStrategyMetadata.GetDataKeys(typeof(SelfWatchObserver));

        Assert.Single(keys);
        Assert.Contains("character.hp", keys);
    }

    [Fact]
    public void ObserveDataAttribute_MultipleKeys()
    {
        var keys = ObserverStrategyMetadata.GetDataKeys(typeof(MultiKeyObserver));

        Assert.Equal(2, keys.Count);
        Assert.Contains("character.hp", keys);
        Assert.Contains("character.mp", keys);
    }

    [Fact]
    public void ObserveDataAttribute_NoAttributes_ReturnsEmpty()
    {
        var keys = ObserverStrategyMetadata.GetDataKeys(typeof(NoDataKeyObserver));

        Assert.Empty(keys);
    }

    // ── Named target mounting ──────────────────────────────────────────

    [Fact]
    public void MountObserverStrategy_WithSelfTargetName_Succeeds()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta("test_hero"));
        MemoryObserver.MountedCalls.Clear();

        entity.MountObserverStrategy("test_hero", MemoryObservedIdx);

        Assert.Single(MemoryObserver.MountedCalls);
    }

    [Fact]
    public void MountObserverStrategy_WithDifferentTargetName_Throws()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta("observer"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            entity.MountObserverStrategy("other_entity", MemoryObservedIdx));
        Assert.Contains("Cross-entity", ex.Message, StringComparison.Ordinal);
    }

    // ── Quit lifecycle ───────────────────────────────────────────────

    [Fact]
    public void Quit_TriggersOnUnmounted()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());
        MemoryObserver.MountedCalls.Clear();
        MemoryObserver.UnmountedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, MemoryObservedIdx);
        entity.QuitSingle();

        Assert.Single(MemoryObserver.UnmountedCalls);
    }

    // ── DeepClone preserves observer bindings ─────────────────────────

    [Fact]
    public void DeepClone_PreservesObserverBindings()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());
        entity.MountObserverStrategy(entity.Name, SelfWatchIdx);

        var meta = entity.SaveSingle();
        var clone = meta.DeepClone();

        Assert.NotNull(clone.StrategyMetaData);
        Assert.Single(clone.StrategyMetaData.ObserverBindings);
        Assert.Equal(entity.Name, clone.StrategyMetaData.ObserverBindings[0].Target);
    }

    // ── Save/Recover round-trip (via meta rebuild) ────────────────────

    [Fact]
    public void SaveSingle_ThenRecover_PreservesObserverBindings()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());
        entity.MountObserverStrategy(entity.Name, SelfWatchIdx);

        var meta = entity.SaveSingle();

        var (entity2, ctx2) = Setup();
        entity2.SpawnSingle(meta);
        var bindings = meta.StrategyMetaData!.ObserverBindings;
        entity2.RecoverObserverBindings(bindings, n => n == entity2.Name ? entity2 : null);

        SelfWatchObserver.DataChangedCalls.Clear();
        entity2.SetData("character.hp", 75);
        Assert.Single(SelfWatchObserver.DataChangedCalls);
    }

    // ── Mount with null/empty arguments ───────────────────────────────

    [Fact]
    public void Mount_NullTargetName_Throws()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());

        Assert.Throws<InvalidOperationException>(
            () => entity.MountObserverStrategy((string)null!, MemoryObservedIdx));
    }

    [Fact]
    public void Mount_EmptyObserverIndex_Throws()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());

        Assert.Throws<ArgumentException>(
            () => entity.MountObserverStrategy(entity.Name, ""));
    }

    [Fact]
    public void Mount_UnknownObserverIndex_Throws()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());

        Assert.Throws<InvalidOperationException>(
            () => entity.MountObserverStrategy(entity.Name, "nonexistent"));
    }

    // ── RecoverBindings edge cases ───────────────────────────────────

    [Fact]
    public void RecoverBindings_TargetNotFound_Skips()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());

        var bindings = new List<StrategyMetaData.ObserverBinding>
        {
            new() { Target = "ghost", ObserverIndices = new List<string> { SelfWatchIdx } }
        };

        var ex = Record.Exception(() =>
            entity.RecoverObserverBindings(bindings, _ => null));

        Assert.Null(ex);
    }

    [Fact]
    public void RecoverBindings_EmptyList_NoError()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());

        var ex = Record.Exception(() =>
            entity.RecoverObserverBindings(
                Array.Empty<StrategyMetaData.ObserverBinding>(),
                _ => null));

        Assert.Null(ex);
    }

    // ── Has / Remove observer bindings by target ──────────────────────

    [Fact]
    public void HasObserverBindingTargeting_ExistingTarget_ReturnsTrue()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());
        entity.MountObserverStrategy(entity.Name, MemoryObservedIdx);

        Assert.True(entity.HasObserverBindingTargeting(entity.Name));
    }

    [Fact]
    public void HasObserverBindingTargeting_NonexistentTarget_ReturnsFalse()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());

        Assert.False(entity.HasObserverBindingTargeting("ghost"));
    }

    [Fact]
    public void RemoveAllObserverBindingsTargeting_ClearsBindings()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());
        entity.MountObserverStrategy(entity.Name, MemoryObservedIdx);
        entity.MountObserverStrategy(entity.Name, SelfWatchIdx);

        entity.RemoveAllObserverBindingsTargeting(entity.Name);

        Assert.False(entity.HasObserverBindingTargeting(entity.Name));
    }

    // ── TeardownOutgoingObserverBindings ──────────────────────────────

    [Fact]
    public void TeardownOutgoingObserverBindings_TriggersOnUnmounted()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());
        MemoryObserver.UnmountedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, MemoryObservedIdx);
        entity.TeardownOutgoingObserverBindings(n => n == entity.Name ? entity : null);

        Assert.Single(MemoryObserver.UnmountedCalls);
    }

    [Fact]
    public void TeardownOutgoingObserverBindings_TargetNotFound_ReleasesStrategy()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta("alice"));
        entity.MountObserverStrategy("alice", MemoryObservedIdx);

        var ex = Record.Exception(() =>
            entity.TeardownOutgoingObserverBindings(_ => null));

        Assert.Null(ex);
        Assert.False(entity.HasObserverBindingTargeting("alice"));
    }

    // ── KillPendingEntities observer cleanup (integration) ────────────

    [Fact]
    public void KillPendingEntities_CleansUpObserverBindings()
    {
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        world.RegisterStrategy(() => new MemoryObserver());
        host.BindWorld(world);
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial",
            "res://entry/entry.json"));
        host.BindContext(ctx);

        var meta = CreateMeta("alice");
        host.RecoverFromMetaList(new[] { meta });
        foreach (var e in host.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireAfterSpawnHooks();

        var entity = (SndEntity)host.FindByName("alice")!;
        entity.MountObserverStrategy("alice", MemoryObservedIdx);
        MemoryObserver.UnmountedCalls.Clear();

        entity.IsPendingKill = true;
        var sndRuntime = new SndRuntime(world, host);
        sndRuntime.KillPendingEntities();

        Assert.Single(MemoryObserver.UnmountedCalls);
    }

    [Fact]
    public void KillPendingEntities_NoObserverBindings_NoError()
    {
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        host.BindWorld(world);
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial",
            "res://entry/entry.json"));
        host.BindContext(ctx);

        var meta = CreateMeta("bob");
        host.RecoverFromMetaList(new[] { meta });
        foreach (var e in host.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireAfterSpawnHooks();

        var entity = (SndEntity)host.FindByName("bob")!;
        entity.IsPendingKill = true;

        var sndRuntime = new SndRuntime(world, host);
        var ex = Record.Exception(() => sndRuntime.KillPendingEntities());
        Assert.Null(ex);
    }

    // ── ClearAll observer cleanup ─────────────────────────────────────

    [Fact]
    public void ClearAll_TriggersOnUnmounted_ForSelfBoundObservers()
    {
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        world.RegisterStrategy(() => new MemoryObserver());
        host.BindWorld(world);
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial",
            "res://entry/entry.json"));
        host.BindContext(ctx);

        var meta = CreateMeta("alice");
        host.RecoverFromMetaList(new[] { meta });
        foreach (var e in host.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireAfterSpawnHooks();

        var entity = (SndEntity)host.FindByName("alice")!;
        entity.MountObserverStrategy("alice", MemoryObservedIdx);
        MemoryObserver.UnmountedCalls.Clear();

        var sndRuntime = new SndRuntime(world, host);
        sndRuntime.ClearAll();

        Assert.Single(MemoryObserver.UnmountedCalls);
        Assert.Equal("alice", MemoryObserver.UnmountedCalls[0].Entity.Name);
    }

    [Fact]
    public void ClearAll_NoObserverBindings_NoError()
    {
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        host.BindWorld(world);
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial",
            "res://entry/entry.json"));
        host.BindContext(ctx);

        var meta = CreateMeta("bob");
        host.RecoverFromMetaList(new[] { meta });
        foreach (var e in host.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireAfterSpawnHooks();

        var sndRuntime = new SndRuntime(world, host);
        var ex = Record.Exception(() => sndRuntime.ClearAll());
        Assert.Null(ex);
    }

    // ── Data change filtering: multi-entity observer isolation ────────

    [Fact]
    public void DataChange_OnlyTargetEntityNotified()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta("alice"));
        entity.MountObserverStrategy("alice", SelfWatchIdx);
        SelfWatchObserver.DataChangedCalls.Clear();

        entity.SetData("character.hp", 10);

        Assert.Single(SelfWatchObserver.DataChangedCalls);
        Assert.Equal("alice", SelfWatchObserver.DataChangedCalls[0].EntityName);
        Assert.Equal("alice", SelfWatchObserver.DataChangedCalls[0].TargetName);
    }

    [Fact]
    public void BuildObserverBindings_TwoTargets_GroupsCorrectly()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta("self"));
        entity.MountObserverStrategy("self", SelfWatchIdx);
        entity.MountObserverStrategy("self", MultiKeyIdx);

        var meta = entity.SaveSingle();
        var bindings = meta.StrategyMetaData!.ObserverBindings;

        Assert.Single(bindings);
        Assert.Equal("self", bindings[0].Target);
        Assert.Equal(2, bindings[0].ObserverIndices.Count);
        Assert.Contains(SelfWatchIdx, bindings[0].ObserverIndices);
        Assert.Contains(MultiKeyIdx, bindings[0].ObserverIndices);
    }

    [Fact]
    public void OnDataChanged_OldAndNewValues_Correct()
    {
        var (entity, ctx) = Setup();
        entity.SpawnSingle(CreateMeta());
        entity.SetData("character.hp", 100);
        SelfWatchObserver.DataChangedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, SelfWatchIdx);
        entity.SetData("character.hp", 50);

        Assert.Single(SelfWatchObserver.DataChangedCalls);
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static FullMemorySndSceneHost CreateHost(Action<SndWorld> configureWorld)
    {
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        configureWorld(world);
        host.BindWorld(world);
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial",
            "res://entry/entry.json"));
        host.BindContext(ctx);
        return host;
    }

    private static (SndEntity entity, SndContext ctx) Setup()
    {
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        runtime.SndWorld.RegisterStrategy(() => new SelfWatchObserver());
        runtime.SndWorld.RegisterStrategy(() => new MultiKeyObserver());
        runtime.SndWorld.RegisterStrategy(() => new NoDataKeyObserver());
        runtime.SndWorld.RegisterStrategy(() => new NamedTargetObserver());
        runtime.SndWorld.RegisterStrategy(() => new MemoryObserver());
        runtime.SndWorld.RegisterStrategy(() => new ThrowOnMountObserver());
        var fs = new TestFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var nodeFactory = new TestNodeFactory();
        var entity = runtime.SndWorld.CreateEntity(nodeFactory, ctx, logger);
        return (entity, ctx);
    }

    private static SndMetaData CreateMeta(string name = "E")
    {
        return new SndMetaData
        {
            Name = name,
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        };
    }

    // ── Test strategies ────────────────────────────────────────────────

    [StrategyIndex(SelfWatchIdx)]
    [ObserveData("character.hp")]
    private sealed class SelfWatchObserver : ObserverStrategyBase
    {
        public static List<DataCall> DataChangedCalls { get; set; } = new();

        public override void OnDataChanged(ISndEntity entity, ISndContext ctx, ISndEntity target,
            string dataKey, TypedData oldValue, TypedData newValue)
        {
            DataChangedCalls.Add(new DataCall
            {
                EntityName = entity.Name,
                TargetName = target.Name,
                DataKey = dataKey
            });
        }

        public sealed class DataCall
        {
            public string EntityName { get; set; } = string.Empty;
            public string TargetName { get; set; } = string.Empty;
            public string DataKey { get; set; } = string.Empty;
        }
    }

    [StrategyIndex(MultiKeyIdx)]
    [ObserveData("character.hp")]
    [ObserveData("character.mp")]
    private sealed class MultiKeyObserver : ObserverStrategyBase
    {
        public static List<string> HpChangedCalls { get; set; } = new();
        public static List<string> MpChangedCalls { get; set; } = new();

        public override void OnDataChanged(ISndEntity entity, ISndContext ctx, ISndEntity target,
            string dataKey, TypedData oldValue, TypedData newValue)
        {
            if (dataKey == "character.hp")
                HpChangedCalls.Add(entity.Name);
            else if (dataKey == "character.mp")
                MpChangedCalls.Add(entity.Name);
        }
    }

    [StrategyIndex(NoDataKeyIdx)]
    private sealed class NoDataKeyObserver : ObserverStrategyBase
    {
    }

    [StrategyIndex(NamedTargetIdx)]
    private sealed class NamedTargetObserver : ObserverStrategyBase
    {
    }

    [StrategyIndex(MemoryObservedIdx)]
    private sealed class MemoryObserver : ObserverStrategyBase
    {
        public static List<MountCall> MountedCalls { get; set; } = new();
        public static List<MountCall> UnmountedCalls { get; set; } = new();

        public override void OnMounted(ISndEntity entity, ISndContext ctx, ISndEntity target)
        {
            MountedCalls.Add(new MountCall { Entity = entity, Target = target });
        }

        public override void OnUnmounted(ISndEntity entity, ISndContext ctx, ISndEntity target)
        {
            UnmountedCalls.Add(new MountCall { Entity = entity, Target = target });
        }

        public sealed class MountCall
        {
            public ISndEntity Entity { get; set; } = null!;
            public ISndEntity Target { get; set; } = null!;
        }
    }

    private sealed class StatefulObserver : ObserverStrategyBase
    {
#pragma warning disable CS0169
        private int _counter;
#pragma warning restore CS0169
    }

    private sealed class UnannotatedObserver : ObserverStrategyBase
    {
    }

    [StrategyIndex(ThrowOnMountIdx)]
    [ObserveData("character.hp")]
    private sealed class ThrowOnMountObserver : ObserverStrategyBase
    {
        public static List<string> DataChangedCalls { get; set; } = new();

        public override void OnMounted(ISndEntity entity, ISndContext ctx, ISndEntity target)
        {
            throw new InvalidOperationException("OnMounted boom");
        }

        public override void OnDataChanged(ISndEntity entity, ISndContext ctx, ISndEntity target,
            string dataKey, TypedData oldValue, TypedData newValue)
        {
            DataChangedCalls.Add(dataKey);
        }
    }
}
