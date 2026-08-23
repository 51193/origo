using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Scene;
using Origo.Core.DataSource;
using Origo.Core.Snd;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class ObserverStrategyTests : IDisposable
{
    private const string _selfWatchIdx = "observer.test.self_watch";
    private const string _multiKeyIdx = "observer.test.multi_key";
    private const string _noDataKeyIdx = "observer.test.no_data_key";
    private const string _memoryObservedIdx = "observer.test.memory";
    private const string _throwOnMountIdx = "observer.test.throw_on_mount";
    private const string _throwOnUnmountIdx = "observer.test.throw_on_unmount";

    // ── Registration ───────────────────────────────────────────────────

    [Fact]
    public void ObserverStrategy_CanBeRegistered()
    {
        var world = TestFactory.CreateSndWorld();
        world.RegisterStrategy(() => new SelfWatchObserver());

        Assert.Contains(_selfWatchIdx, world.GetRegisteredStrategyIndices());
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
        var (entity, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        MemoryObserver.MountedCalls.Clear();
        MemoryObserver.UnmountedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, _memoryObservedIdx);

        Assert.Single(MemoryObserver.MountedCalls);
        var call = MemoryObserver.MountedCalls[0];
        Assert.Equal(entity.Name, call.Entity.Name);
        Assert.Equal(entity.Name, call.Target.Name);
    }

    [Fact]
    public void Unmount_TriggersOnUnmounted_WithCorrectParameters()
    {
        var (entity, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        MemoryObserver.MountedCalls.Clear();
        MemoryObserver.UnmountedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, _memoryObservedIdx);
        entity.UnmountObserverStrategy(entity.Name, _memoryObservedIdx);

        Assert.Single(MemoryObserver.UnmountedCalls);
    }

    [Fact]
    public void Mount_Duplicate_Throws()
    {
        var (entity, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        entity.MountObserverStrategy(entity.Name, _memoryObservedIdx);

        // Mounting the same (observer, target, index) twice would double the
        // subscription and the pool reference; it is rejected like duplicate
        // strategy mounts.
        Assert.Throws<InvalidOperationException>(() =>
            entity.MountObserverStrategy(entity.Name, _memoryObservedIdx));
    }

    [Fact]
    public void Unmount_NotMounted_Throws()
    {
        var (entity, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        entity.MountObserverStrategy(entity.Name, _memoryObservedIdx);
        entity.UnmountObserverStrategy(entity.Name, _memoryObservedIdx);

        // Unmounting a pair that is no longer mounted fails fast (consistent
        // with RemoveStrategy on a non-mounted strategy index).
        Assert.Throws<InvalidOperationException>(() =>
            entity.UnmountObserverStrategy(entity.Name, _memoryObservedIdx));
    }

    // ── Mount failure cleanup ──────────────────────────────────────────

    [Fact]
    public void Mount_WhenOnMountedThrows_RollsBackAndReturnsToPool()
    {
        var (entity, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        ThrowOnMountObserver.DataChangedCalls.Clear();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            entity.MountObserverStrategy(entity.Name, _throwOnMountIdx));
        Assert.Contains("OnMounted boom", ex.Message);

        // After failure, data subscription must be rolled back
        entity.SetData("character.hp", 10);
        Assert.Empty(ThrowOnMountObserver.DataChangedCalls);
    }

    [Fact]
    public void Mount_WhenGetStrategyThrows_PropagatesOriginalError()
    {
        var (entity, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            entity.MountObserverStrategy(entity.Name, "observer.nonexistent"));

        Assert.Contains("observer.nonexistent", ex.Message);
    }

    [Fact]
    public void Unmount_WhenOnUnmountedThrows_PoolReferenceStillReleased()
    {
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        runtime.SndWorld.RegisterStrategy(() => new ThrowOnUnmountObserver());
        var fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var topology = new ObserverTopology(runtime.SndWorld.StrategyPool, logger);
        topology.BindContext(ctx);
        var entity = runtime.SndWorld.CreateEntity(new TestNodeFactory(), ctx, logger, topology);

        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        entity.MountObserverStrategy(entity.Name, _throwOnUnmountIdx);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            entity.UnmountObserverStrategy(entity.Name, _throwOnUnmountIdx));
        Assert.Contains("OnUnmounted boom", ex.Message);

        // The failed Unmount must still return the strategy to the pool;
        // otherwise the reference count stays non-zero and LogPoolLeaks
        // reports a leak at teardown.
        ((IEntityLifecycle)entity).ReleaseStrategiesOnly();
        runtime.SndWorld.StrategyPool.LogPoolLeaks();
        Assert.DoesNotContain(logger.Warnings, w => w.Contains("leak", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SessionDestroy_WhenOnUnmountedThrows_PoolReferenceStillReleased()
    {
        var logger = new TestLogger();
        var fs = new TestMemoryFileSystem();
        var host = new FullMemorySndSceneHost(logger);
        var runtime = TestFactory.CreateRuntime(
            logger,
            host,
            new TypeStringMapping(),
            new Blackboard.Blackboard(),
            DataSourceFactory.CreateDefaultIoGateway(fs));
        host.BindWorld(runtime.SndWorld);
        runtime.SndWorld.RegisterStrategy(() => new ThrowOnUnmountObserver());

        var io = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(
            runtime, io, metaAccess, pathResolver, "root", "res://initial", "entry.json")
        {
            AutoDiscoverStrategies = false
        });
        host.BindContext(ctx);
        fs.SeedFile("entry.json",
            "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]");

        ctx.Bootstrap();
        ctx.FlushFrame();

        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        var observer = session.Spawn(CreateMeta("session_observer"));
        var target = session.Spawn(CreateMeta("session_target"));
        observer.MountObserverStrategy(target, _throwOnUnmountIdx);

        // Session destruction is the real FullCleanup path: ReleaseAllEntities
        // removes each binding and calls TeardownObserverBindings, where the
        // throwing OnUnmounted hook must not skip the pool release.
        var ex = Assert.Throws<InvalidOperationException>(() =>
            session.SessionManager.DestroySession(ISessionManager.ForegroundKey));
        Assert.Contains("OnUnmounted boom", ex.Message);

        // The binding was removed before OnUnmounted ran, so only a
        // guaranteed release inside FullCleanup can prevent this leak.
        ctx.Runtime.SndWorld.StrategyPool.LogPoolLeaks();
        Assert.DoesNotContain(
            logger.Warnings,
            w => w.Contains(_throwOnUnmountIdx, StringComparison.Ordinal)
                 && w.Contains("refCount", StringComparison.Ordinal));
    }

    // ── Data change notification ───────────────────────────────────────

    [Fact]
    public void SetData_TriggersOnDataChanged_ForObservedKey()
    {
        var (entity, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        SelfWatchObserver.DataChangedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, _selfWatchIdx);
        entity.SetData("character.hp", 50);

        Assert.Single(SelfWatchObserver.DataChangedCalls);
        Assert.Equal("character.hp", SelfWatchObserver.DataChangedCalls[0].DataKey);
    }

    [Fact]
    public void SetData_DoesNotTrigger_ForUnobservedKey()
    {
        var (entity, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        SelfWatchObserver.DataChangedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, _selfWatchIdx);
        entity.SetData("character.mp", 20);

        Assert.Empty(SelfWatchObserver.DataChangedCalls);
    }

    [Fact]
    public void SetData_DoesNotTrigger_AfterUnmount()
    {
        var (entity, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        SelfWatchObserver.DataChangedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, _selfWatchIdx);
        entity.UnmountObserverStrategy(entity.Name, _selfWatchIdx);
        entity.SetData("character.hp", 50);

        Assert.Empty(SelfWatchObserver.DataChangedCalls);
    }

    [Fact]
    public void SetData_TriggersForMultipleKeys()
    {
        var (entity, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        MultiKeyObserver.HpChangedCalls.Clear();
        MultiKeyObserver.MpChangedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, _multiKeyIdx);
        entity.SetData("character.hp", 30);
        entity.SetData("character.mp", 10);

        Assert.Single(MultiKeyObserver.HpChangedCalls);
        Assert.Single(MultiKeyObserver.MpChangedCalls);
    }

    [Fact]
    public void SetData_OldAndNewValuesCorrect()
    {
        var (entity, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        SelfWatchObserver.DataChangedCalls.Clear();

        entity.SetData("character.hp", 100);
        entity.MountObserverStrategy(entity.Name, _selfWatchIdx);
        entity.SetData("character.hp", 50);

        Assert.Single(SelfWatchObserver.DataChangedCalls);
        var call = SelfWatchObserver.DataChangedCalls[0];
        Assert.Equal(100, call.OldValue.AsInt32());
        Assert.Equal(50, call.NewValue.AsInt32());
    }

    // ── Observer strategy without data keys ─────────────────────────────

    [Fact]
    public void NoDataKeyObserver_CanMountAndUnmount()
    {
        var (entity, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        var ex1 = Record.Exception(() =>
            entity.MountObserverStrategy(entity.Name, _noDataKeyIdx));
        Assert.Null(ex1);

        var ex2 = Record.Exception(() =>
            entity.UnmountObserverStrategy(entity.Name, _noDataKeyIdx));
        Assert.Null(ex2);
    }

    // ── ObserverMetaData serialization ──────────────────────────────────

    [Fact]
    public void BuildMetaData_IncludesObserverBindings()
    {
        var (entity, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        entity.MountObserverStrategy(entity.Name, _selfWatchIdx);

        ((IEntityLifecycle)entity).FireBeforeSaveHooks();
        var meta = ((IEntityLifecycle)entity).BuildMetaData();

        Assert.NotNull(meta.StrategyMetaData);
        Assert.Single(meta.StrategyMetaData.ObserverIndices);
        Assert.Equal(entity.Name, meta.StrategyMetaData.ObserverIndices[0].Target);
        Assert.Contains(_selfWatchIdx, meta.StrategyMetaData.ObserverIndices[0].ObserverIndices);
    }

    [Fact]
    public void BuildMetaData_EmptyBindings_WhenNoObservers()
    {
        var (entity, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        ((IEntityLifecycle)entity).FireBeforeSaveHooks();
        var meta = ((IEntityLifecycle)entity).BuildMetaData();

        Assert.NotNull(meta.StrategyMetaData);
        Assert.Empty(meta.StrategyMetaData.ObserverIndices);
    }

    [Fact]
    public void BuildMetaData_MultipleTargets_GroupedCorrectly()
    {
        var (entity, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        entity.MountObserverStrategy(entity.Name, _selfWatchIdx);
        entity.MountObserverStrategy(entity.Name, _multiKeyIdx);

        ((IEntityLifecycle)entity).FireBeforeSaveHooks();
        var meta = ((IEntityLifecycle)entity).BuildMetaData();

        Assert.Single(meta.StrategyMetaData!.ObserverIndices);
        Assert.Equal(2, meta.StrategyMetaData.ObserverIndices[0].ObserverIndices.Count);
    }

    // ── Lifecycle: observer strategies released on dead ─────────────────

    [Fact]
    public void Dead_ReleasesObserverStrategies()
    {
        var (entity, _, topology) = SetupWithTopology();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        entity.MountObserverStrategy(entity.Name, _selfWatchIdx);
        SelfWatchObserver.DataChangedCalls.Clear();

        DestroySingleEntity(entity, topology, quit: false);

        // After dead, no data change notifications should fire
        // (entity is dead, no observer callbacks)
        Assert.Empty(SelfWatchObserver.DataChangedCalls);
    }

    [Fact]
    public void Dead_TriggersOnUnmounted()
    {
        var (entity, _, topology) = SetupWithTopology();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        MemoryObserver.MountedCalls.Clear();
        MemoryObserver.UnmountedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, _memoryObservedIdx);
        DestroySingleEntity(entity, topology, quit: false);

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
        var (entity, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta("test_hero")); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        MemoryObserver.MountedCalls.Clear();

        entity.MountObserverStrategy("test_hero", _memoryObservedIdx);

        Assert.Single(MemoryObserver.MountedCalls);
    }

    [Fact]
    public void MountObserverStrategy_WithDifferentTargetName_Throws()
    {
        var (entity, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta("observer")); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            entity.MountObserverStrategy("other_entity", _memoryObservedIdx));
        Assert.Contains("Cross-entity", ex.Message, StringComparison.Ordinal);
    }

    // ── Quit lifecycle ───────────────────────────────────────────────

    [Fact]
    public void Quit_TriggersOnUnmounted()
    {
        var (entity, _, topology) = SetupWithTopology();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        MemoryObserver.MountedCalls.Clear();
        MemoryObserver.UnmountedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, _memoryObservedIdx);
        DestroySingleEntity(entity, topology, quit: true);

        Assert.Single(MemoryObserver.UnmountedCalls);
    }

    // ── DeepClone preserves observer bindings ─────────────────────────

    [Fact]
    public void DeepClone_PreservesObserverBindings()
    {
        var (entity, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        entity.MountObserverStrategy(entity.Name, _selfWatchIdx);

        ((IEntityLifecycle)entity).FireBeforeSaveHooks();
        var meta = ((IEntityLifecycle)entity).BuildMetaData();
        var clone = meta.DeepClone();

        Assert.NotNull(clone.StrategyMetaData);
        Assert.Single(clone.StrategyMetaData.ObserverIndices);
        Assert.Equal(entity.Name, clone.StrategyMetaData.ObserverIndices[0].Target);
    }

    // ── Save/Recover round-trip (via meta rebuild) ────────────────────

    [Fact]
    public void SaveSingle_ThenRecover_PreservesObserverBindings()
    {
        var (entity, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        entity.MountObserverStrategy(entity.Name, _selfWatchIdx);

        ((IEntityLifecycle)entity).FireBeforeSaveHooks();
        var meta = ((IEntityLifecycle)entity).BuildMetaData();

        var (entity2, _, topology2) = SetupWithTopology();
        ((IEntityLifecycle)entity2).RecoverForLifecycle(meta); ((IEntityLifecycle)entity2).FireAfterSpawnHooks();
        var bindings = meta.StrategyMetaData!.ObserverIndices;
        topology2.RecoverBindingsFor(entity2, bindings, n => n == entity2.Name ? entity2 : null);

        SelfWatchObserver.DataChangedCalls.Clear();
        entity2.SetData("character.hp", 75);
        Assert.Single(SelfWatchObserver.DataChangedCalls);
    }

    // ── Mount with null/empty arguments ───────────────────────────────

    [Fact]
    public void Mount_NullTargetName_Throws()
    {
        var (entity, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        Assert.Throws<ArgumentNullException>(
            () => entity.MountObserverStrategy((string)null!, _memoryObservedIdx));
    }

    [Fact]
    public void Mount_EmptyObserverIndex_Throws()
    {
        var (entity, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        Assert.Throws<ArgumentException>(
            () => entity.MountObserverStrategy(entity.Name, ""));
    }

    [Fact]
    public void Mount_UnknownObserverIndex_Throws()
    {
        var (entity, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        Assert.Throws<InvalidOperationException>(
            () => entity.MountObserverStrategy(entity.Name, "nonexistent"));
    }

    // ── RecoverBindings edge cases ───────────────────────────────────

    [Fact]
    public void RecoverBindings_TargetNotFound_Throws()
    {
        var (entity, _, topology) = SetupWithTopology();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        var bindings = new List<StrategyMetaData.ObserverBinding>
        {
            new() { Target = "ghost", ObserverIndices = [_selfWatchIdx] }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            topology.RecoverBindingsFor(entity, bindings, _ => null));
        Assert.Contains("ghost", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RecoverBindings_EmptyTarget_Throws()
    {
        var (entity, _, topology) = SetupWithTopology();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        var bindings = new List<StrategyMetaData.ObserverBinding>
        {
            new() { Target = "   ", ObserverIndices = [_selfWatchIdx] }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            topology.RecoverBindingsFor(entity, bindings, _ => null));
        Assert.Contains("empty target", ex.Message, StringComparison.Ordinal);
    }

    // ── Has / Remove observer bindings by target ──────────────────────

    [Fact]
    public void GetObserverNamesTargeting_ExistingTarget_ReturnsTrue()
    {
        var (entity, _, topology) = SetupWithTopology();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        entity.MountObserverStrategy(entity.Name, _memoryObservedIdx);

        Assert.Contains(entity.Name, topology.GetObserverNamesTargeting(entity.Name));
    }

    [Fact]
    public void GetObserverNamesTargeting_NonexistentTarget_ReturnsFalse()
    {
        var (entity, _, topology) = SetupWithTopology();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        Assert.Empty(topology.GetObserverNamesTargeting("ghost"));
    }

    [Fact]
    public void RemoveAllObserverBindingsTargeting_ClearsBindings()
    {
        var (entity, _, topology) = SetupWithTopology();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        entity.MountObserverStrategy(entity.Name, _memoryObservedIdx);
        entity.MountObserverStrategy(entity.Name, _selfWatchIdx);

        topology.RemoveBindingsTargetingFor(entity, entity.Name);

        Assert.DoesNotContain(entity.Name, topology.GetObserverNamesTargeting(entity.Name));
    }

    // ── Incoming index (O(1) observer lookup by target) ───────────────

    [Fact]
    public void GetObserverNamesTargeting_MountedObserver_ReturnsObserverName()
    {
        var (entity, _, topology) = SetupWithTopology();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        entity.MountObserverStrategy(entity.Name, _memoryObservedIdx);

        Assert.Equal([entity.Name], topology.GetObserverNamesTargeting(entity.Name));
    }

    [Fact]
    public void GetObserverNamesTargeting_NoBindings_ReturnsEmpty()
    {
        var (entity, _, topology) = SetupWithTopology();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        Assert.Empty(topology.GetObserverNamesTargeting(entity.Name));
        Assert.Empty(topology.GetObserverNamesTargeting("ghost"));
    }

    [Fact]
    public void GetObserverNamesTargeting_AfterUnmount_IndexCleared()
    {
        var (entity, _, topology) = SetupWithTopology();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        entity.MountObserverStrategy(entity.Name, _memoryObservedIdx);

        entity.UnmountObserverStrategy(entity.Name, _memoryObservedIdx);

        Assert.Empty(topology.GetObserverNamesTargeting(entity.Name));
    }

    // ── TeardownOutgoingObserverBindings ──────────────────────────────

    [Fact]
    public void TeardownOutgoingObserverBindings_TriggersOnUnmounted()
    {
        var (entity, _, topology) = SetupWithTopology();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        MemoryObserver.UnmountedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, _memoryObservedIdx);
        topology.TeardownOutgoingFor(entity, n => n == entity.Name ? entity : null);

        Assert.Single(MemoryObserver.UnmountedCalls);
    }

    // ── KillPendingEntities observer cleanup (integration) ────────────


    [Fact]
    public void KillPendingEntities_NoObserverBindings_NoError()
    {
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        host.BindWorld(world);
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("res://entry/entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
        var runtime = TestFactory.CreateRuntime(logger, host);
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial",
            "res://entry/entry.json"));
        host.BindContext(ctx);
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.FlushFrame();

        var session = ctx.Runtime.SessionManager.ForegroundSession!;
        var entity = session.Spawn(CreateMeta("bob"));
        Assert.False(entity.IsPendingKill);

        session.RequestKillEntity("bob");
        Assert.True(entity.IsPendingKill);
        Assert.Single(session.GetEntities());

        ctx.Runtime.SessionManager.KillPendingAllSessions();
        Assert.Empty(session.GetEntities());
    }

    // ── ClearAll observer cleanup ─────────────────────────────────────


    [Fact]
    public void ClearAll_NoObserverBindings_NoError()
    {
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        host.BindWorld(world);
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("res://entry/entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial",
            "res://entry/entry.json"));
        host.BindContext(ctx);

        var meta = CreateMeta("bob");
        host.RecoverFromMetaList([meta]);
        foreach (var e in host.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireAfterSpawnHooks();

        Assert.Single(host.GetEntities());

        host.RemoveAllEntities();
        Assert.Empty(host.GetEntities());
    }

    // ── Data change filtering: multi-entity observer isolation ────────

    [Fact]
    public void DataChange_OnlyTargetEntityNotified()
    {
        var (entity, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta("alice")); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        entity.MountObserverStrategy("alice", _selfWatchIdx);
        SelfWatchObserver.DataChangedCalls.Clear();

        entity.SetData("character.hp", 10);

        Assert.Single(SelfWatchObserver.DataChangedCalls);
        Assert.Equal("alice", SelfWatchObserver.DataChangedCalls[0].EntityName);
        Assert.Equal("alice", SelfWatchObserver.DataChangedCalls[0].TargetName);
    }

    [Fact]
    public void BuildObserverBindings_TwoTargets_GroupsCorrectly()
    {
        var (entity, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta("self")); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        entity.MountObserverStrategy("self", _selfWatchIdx);
        entity.MountObserverStrategy("self", _multiKeyIdx);

        ((IEntityLifecycle)entity).FireBeforeSaveHooks();
        var meta = ((IEntityLifecycle)entity).BuildMetaData();
        var bindings = meta.StrategyMetaData!.ObserverIndices;

        Assert.Single(bindings);
        Assert.Equal("self", bindings[0].Target);
        Assert.Equal(2, bindings[0].ObserverIndices.Count);
        Assert.Contains(_selfWatchIdx, bindings[0].ObserverIndices);
        Assert.Contains(_multiKeyIdx, bindings[0].ObserverIndices);
    }

    [Fact]
    public void OnDataChanged_OldAndNewValues_Correct()
    {
        var (entity, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        entity.SetData("character.hp", 100);
        SelfWatchObserver.DataChangedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, _selfWatchIdx);
        entity.SetData("character.hp", 50);

        Assert.Single(SelfWatchObserver.DataChangedCalls);
        var call = SelfWatchObserver.DataChangedCalls[0];
        Assert.Equal(100, call.OldValue.AsInt32());
        Assert.Equal(50, call.NewValue.AsInt32());
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static FullMemorySndSceneHost CreateHost(Action<SndWorld> configureWorld)
    {
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        configureWorld(world);
        host.BindWorld(world);
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("res://entry/entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
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
        var (entity, ctx, _) = SetupWithTopology();
        return (entity, ctx);
    }

    private static (SndEntity entity, SndContext ctx, ObserverTopology topology) SetupWithTopology()
    {
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        runtime.SndWorld.RegisterStrategy(() => new SelfWatchObserver());
        runtime.SndWorld.RegisterStrategy(() => new MultiKeyObserver());
        runtime.SndWorld.RegisterStrategy(() => new NoDataKeyObserver());
        runtime.SndWorld.RegisterStrategy(() => new MemoryObserver());
        runtime.SndWorld.RegisterStrategy(() => new ThrowOnMountObserver());
        var fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var nodeFactory = new TestNodeFactory();
        var topology = new ObserverTopology(runtime.SndWorld.StrategyPool, logger);
        topology.BindContext(ctx);
        var entity = runtime.SndWorld.CreateEntity(nodeFactory, ctx, logger, topology);
        return (entity, ctx, topology);
    }

    /// <summary>
    ///     Test-side single-entity teardown matching the production
    ///     <c>SessionRun.KillPending</c> sequence for a session-less entity:
    ///     quit/dead hooks → observer unbind → release strategies → teardown.
    /// </summary>
    private static void DestroySingleEntity(SndEntity entity, ObserverTopology topology, bool quit)
    {
        if (quit)
            ((IEntityLifecycle)entity).FireBeforeQuitHooks();
        else
            ((IEntityLifecycle)entity).FireBeforeDeadHooks();
        topology.TeardownAllBindingsFor(entity);
        ((IEntityLifecycle)entity).ReleaseStrategiesOnly();
        ((IEntityLifecycle)entity).TeardownOnly();
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

    [Fact]
    public void FullCleanup_NullTargetEntity_ThrowsInvalidOperation()
    {
        var world = TestFactory.CreateSndWorld();
        world.RegisterStrategy(() => new NoDataKeyObserver());
        var pool = world.StrategyPool;
        var strategy = pool.GetStrategy<ObserverStrategyBase>(_noDataKeyIdx);

        var entry = new ObserverBindingEntry
        {
            ObserverName = "observer",
            TargetName = "target",
            ObserverIndex = _noDataKeyIdx,
            Strategy = strategy,
            DataKeys = [],
            TargetEntity = null
        };

        var ex = Assert.Throws<InvalidOperationException>(
            () => entry.FullCleanup(null!, null!, null!));
        Assert.Contains("TargetEntity", ex.Message, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        SelfWatchObserver.DataChangedCalls.Clear();
        MultiKeyObserver.HpChangedCalls.Clear();
        MultiKeyObserver.MpChangedCalls.Clear();
        MemoryObserver.MountedCalls.Clear();
        MemoryObserver.UnmountedCalls.Clear();
        ThrowOnMountObserver.DataChangedCalls.Clear();

        GC.SuppressFinalize(this);
    }

    // ── Test strategies ────────────────────────────────────────────────

    [StrategyIndex(_selfWatchIdx)]
    [ObserveData("character.hp")]
    private sealed class SelfWatchObserver : ObserverStrategyBase
    {
        private static readonly AsyncLocal<List<DataCall>> _dataChangedCalls = new();
        public static List<DataCall> DataChangedCalls => _dataChangedCalls.Value ??= [];

        public override void OnDataChanged(ISndEntity entity, ISndContext ctx, ISndEntity target,
            string dataKey, TypedData oldValue, TypedData newValue)
        {
            DataChangedCalls.Add(new DataCall
            {
                EntityName = entity.Name,
                TargetName = target.Name,
                DataKey = dataKey,
                OldValue = oldValue,
                NewValue = newValue
            });
        }

        public sealed class DataCall
        {
            public string EntityName { get; set; } = string.Empty;
            public string TargetName { get; set; } = string.Empty;
            public string DataKey { get; set; } = string.Empty;
            public TypedData OldValue { get; set; }
            public TypedData NewValue { get; set; }
        }
    }

    [StrategyIndex(_multiKeyIdx)]
    [ObserveData("character.hp")]
    [ObserveData("character.mp")]
    private sealed class MultiKeyObserver : ObserverStrategyBase
    {
        private static readonly AsyncLocal<List<string>> _hpChangedCalls = new();
        private static readonly AsyncLocal<List<string>> _mpChangedCalls = new();
        public static List<string> HpChangedCalls => _hpChangedCalls.Value ??= [];
        public static List<string> MpChangedCalls => _mpChangedCalls.Value ??= [];

        public override void OnDataChanged(ISndEntity entity, ISndContext ctx, ISndEntity target,
            string dataKey, TypedData oldValue, TypedData newValue)
        {
            if (dataKey == "character.hp")
                HpChangedCalls.Add(entity.Name);
            else if (dataKey == "character.mp")
                MpChangedCalls.Add(entity.Name);
        }
    }

    [StrategyIndex(_noDataKeyIdx)]
    private sealed class NoDataKeyObserver : ObserverStrategyBase
    {
    }

    [StrategyIndex(_memoryObservedIdx)]
    private sealed class MemoryObserver : ObserverStrategyBase
    {
        private static readonly AsyncLocal<List<MountCall>> _mountedCalls = new();
        private static readonly AsyncLocal<List<MountCall>> _unmountedCalls = new();
        public static List<MountCall> MountedCalls => _mountedCalls.Value ??= [];
        public static List<MountCall> UnmountedCalls => _unmountedCalls.Value ??= [];

        public override void OnMounted(ISndEntity entity, ISndContext ctx, ISndEntity target) => MountedCalls.Add(new MountCall(entity, target));

        public override void OnUnmounted(ISndEntity entity, ISndContext ctx, ISndEntity target) => UnmountedCalls.Add(new MountCall(entity, target));

        public sealed class MountCall(ISndEntity entity, ISndEntity target)
        {
            public ISndEntity Entity { get; } = entity;
            public ISndEntity Target { get; } = target;
        }
    }

    private sealed class StatefulObserver : ObserverStrategyBase
    {
        // _counter 实例字段有意保留：验证注册有实例字段的观察者被 SndStrategyPool 拒绝
#pragma warning disable CS0169, IDE0044
        private int _counter;
#pragma warning restore CS0169, IDE0044
    }

    private sealed class UnannotatedObserver : ObserverStrategyBase
    {
    }

    [StrategyIndex(_throwOnMountIdx)]
    [ObserveData("character.hp")]
    private sealed class ThrowOnMountObserver : ObserverStrategyBase
    {
        private static readonly AsyncLocal<List<string>> _dataChangedCalls = new();
        public static List<string> DataChangedCalls => _dataChangedCalls.Value ??= [];

        public override void OnMounted(ISndEntity entity, ISndContext ctx, ISndEntity target) => throw new InvalidOperationException("OnMounted boom");

        public override void OnDataChanged(ISndEntity entity, ISndContext ctx, ISndEntity target,
            string dataKey, TypedData oldValue, TypedData newValue) => DataChangedCalls.Add(dataKey);
    }

    [StrategyIndex(_throwOnUnmountIdx)]
    [ObserveData("character.hp")]
    private sealed class ThrowOnUnmountObserver : ObserverStrategyBase
    {
        public override void OnUnmounted(ISndEntity entity, ISndContext ctx, ISndEntity target) =>
            throw new InvalidOperationException("OnUnmounted boom");
    }

    // ── Cross-entity observer (ISndEntity overload) ────────────────────

    [Fact]
    public void MountObserverStrategy_ByEntityOverload_Works()
    {
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        world.RegisterStrategy(() => new SelfWatchObserver());
        world.RegisterStrategy(() => new MultiKeyObserver());
        host.BindWorld(world);
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("res://entry/entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial",
            "res://entry/entry.json"));
        host.BindContext(ctx);

        var metaAlice = CreateMeta("alice");
        var metaBob = CreateMeta("bob");
        host.RecoverFromMetaList([metaAlice, metaBob]);
        foreach (var e in host.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireAfterSpawnHooks();

        var alice = host.FindByName("alice")!;
        var bob = host.FindByName("bob")!;

        SelfWatchObserver.DataChangedCalls.Clear();
        alice.MountObserverStrategy(bob, _selfWatchIdx);

        bob.SetData("character.hp", 50);

        Assert.Single(SelfWatchObserver.DataChangedCalls);
        Assert.Equal("alice", SelfWatchObserver.DataChangedCalls[0].EntityName);
        Assert.Equal("bob", SelfWatchObserver.DataChangedCalls[0].TargetName);
    }

    [Fact]
    public void MountObserverStrategy_ByEntityOverload_NullTarget_Throws()
    {
        var (entity, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta()); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        Assert.Throws<ArgumentNullException>(() =>
            entity.MountObserverStrategy((ISndEntity)null!, _memoryObservedIdx));
    }
}
