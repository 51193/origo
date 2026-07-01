using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    private const string _selfWatchIdx = "observer.test.self_watch";
    private const string _multiKeyIdx = "observer.test.multi_key";
    private const string _noDataKeyIdx = "observer.test.no_data_key";
    private const string _memoryObservedIdx = "observer.test.memory";
    private const string _throwOnMountIdx = "observer.test.throw_on_mount";

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
        entity.SpawnSingle(CreateMeta());
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
        entity.SpawnSingle(CreateMeta());
        MemoryObserver.MountedCalls.Clear();
        MemoryObserver.UnmountedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, _memoryObservedIdx);
        entity.UnmountObserverStrategy(entity.Name, _memoryObservedIdx);

        Assert.Single(MemoryObserver.UnmountedCalls);
    }

    [Fact]
    public void Mount_Duplicate_DoesNotThrow()
    {
        var (entity, _) = Setup();
        entity.SpawnSingle(CreateMeta());
        MemoryObserver.MountedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, _memoryObservedIdx);
        entity.MountObserverStrategy(entity.Name, _memoryObservedIdx);

        Assert.Equal(2, MemoryObserver.MountedCalls.Count);
    }

    [Fact]
    public void Unmount_OnlyRemovesOneInstance()
    {
        var (entity, _) = Setup();
        entity.SpawnSingle(CreateMeta());
        MemoryObserver.UnmountedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, _memoryObservedIdx);
        entity.MountObserverStrategy(entity.Name, _memoryObservedIdx);
        entity.UnmountObserverStrategy(entity.Name, _memoryObservedIdx);

        Assert.Single(MemoryObserver.UnmountedCalls);
    }

    // ── Mount failure cleanup ──────────────────────────────────────────

    [Fact]
    public void Mount_WhenOnMountedThrows_RollsBackAndReturnsToPool()
    {
        var (entity, _) = Setup();
        entity.SpawnSingle(CreateMeta());
        ThrowOnMountObserver.DataChangedCalls.Clear();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            entity.MountObserverStrategy(entity.Name, _throwOnMountIdx));
        Assert.Contains("OnMounted boom", ex.Message);

        // After failure, data subscription must be rolled back
        entity.SetData("character.hp", 10);
        Assert.Empty(ThrowOnMountObserver.DataChangedCalls);
    }

    // ── Data change notification ───────────────────────────────────────

    [Fact]
    public void SetData_TriggersOnDataChanged_ForObservedKey()
    {
        var (entity, _) = Setup();
        entity.SpawnSingle(CreateMeta());
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
        entity.SpawnSingle(CreateMeta());
        SelfWatchObserver.DataChangedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, _selfWatchIdx);
        entity.SetData("character.mp", 20);

        Assert.Empty(SelfWatchObserver.DataChangedCalls);
    }

    [Fact]
    public void SetData_DoesNotTrigger_AfterUnmount()
    {
        var (entity, _) = Setup();
        entity.SpawnSingle(CreateMeta());
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
        entity.SpawnSingle(CreateMeta());
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
        entity.SpawnSingle(CreateMeta());
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
        entity.SpawnSingle(CreateMeta());

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
        entity.SpawnSingle(CreateMeta());
        entity.MountObserverStrategy(entity.Name, _selfWatchIdx);

        var meta = entity.SaveSingle();

        Assert.NotNull(meta.StrategyMetaData);
        Assert.Single(meta.StrategyMetaData.ObserverIndices);
        Assert.Equal(entity.Name, meta.StrategyMetaData.ObserverIndices[0].Target);
        Assert.Contains(_selfWatchIdx, meta.StrategyMetaData.ObserverIndices[0].ObserverIndices);
    }

    [Fact]
    public void BuildMetaData_EmptyBindings_WhenNoObservers()
    {
        var (entity, _) = Setup();
        entity.SpawnSingle(CreateMeta());

        var meta = entity.SaveSingle();

        Assert.NotNull(meta.StrategyMetaData);
        Assert.Empty(meta.StrategyMetaData.ObserverIndices);
    }

    [Fact]
    public void BuildMetaData_MultipleTargets_GroupedCorrectly()
    {
        var (entity, _) = Setup();
        entity.SpawnSingle(CreateMeta());
        entity.MountObserverStrategy(entity.Name, _selfWatchIdx);
        entity.MountObserverStrategy(entity.Name, _multiKeyIdx);

        var meta = entity.SaveSingle();

        Assert.Single(meta.StrategyMetaData!.ObserverIndices);
        Assert.Equal(2, meta.StrategyMetaData.ObserverIndices[0].ObserverIndices.Count);
    }

    // ── Lifecycle: observer strategies released on dead ─────────────────

    [Fact]
    public void Dead_ReleasesObserverStrategies()
    {
        var (entity, _) = Setup();
        entity.SpawnSingle(CreateMeta());
        entity.MountObserverStrategy(entity.Name, _selfWatchIdx);
        SelfWatchObserver.DataChangedCalls.Clear();

        entity.DeadSingle();

        // After dead, no data change notifications should fire
        // (entity is dead, no observer callbacks)
        Assert.Empty(SelfWatchObserver.DataChangedCalls);
    }

    [Fact]
    public void Dead_TriggersOnUnmounted()
    {
        var (entity, _) = Setup();
        entity.SpawnSingle(CreateMeta());
        MemoryObserver.MountedCalls.Clear();
        MemoryObserver.UnmountedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, _memoryObservedIdx);
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
        var (entity, _) = Setup();
        entity.SpawnSingle(CreateMeta("test_hero"));
        MemoryObserver.MountedCalls.Clear();

        entity.MountObserverStrategy("test_hero", _memoryObservedIdx);

        Assert.Single(MemoryObserver.MountedCalls);
    }

    [Fact]
    public void MountObserverStrategy_WithDifferentTargetName_Throws()
    {
        var (entity, _) = Setup();
        entity.SpawnSingle(CreateMeta("observer"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            entity.MountObserverStrategy("other_entity", _memoryObservedIdx));
        Assert.Contains("Cross-entity", ex.Message, StringComparison.Ordinal);
    }

    // ── Quit lifecycle ───────────────────────────────────────────────

    [Fact]
    public void Quit_TriggersOnUnmounted()
    {
        var (entity, _) = Setup();
        entity.SpawnSingle(CreateMeta());
        MemoryObserver.MountedCalls.Clear();
        MemoryObserver.UnmountedCalls.Clear();

        entity.MountObserverStrategy(entity.Name, _memoryObservedIdx);
        entity.QuitSingle();

        Assert.Single(MemoryObserver.UnmountedCalls);
    }

    // ── DeepClone preserves observer bindings ─────────────────────────

    [Fact]
    public void DeepClone_PreservesObserverBindings()
    {
        var (entity, _) = Setup();
        entity.SpawnSingle(CreateMeta());
        entity.MountObserverStrategy(entity.Name, _selfWatchIdx);

        var meta = entity.SaveSingle();
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
        entity.SpawnSingle(CreateMeta());
        entity.MountObserverStrategy(entity.Name, _selfWatchIdx);

        var meta = entity.SaveSingle();

        var (entity2, _, topology2) = SetupWithTopology();
        entity2.SpawnSingle(meta);
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
        entity.SpawnSingle(CreateMeta());

        Assert.Throws<InvalidOperationException>(
            () => entity.MountObserverStrategy((string)null!, _memoryObservedIdx));
    }

    [Fact]
    public void Mount_EmptyObserverIndex_Throws()
    {
        var (entity, _) = Setup();
        entity.SpawnSingle(CreateMeta());

        Assert.Throws<ArgumentException>(
            () => entity.MountObserverStrategy(entity.Name, ""));
    }

    [Fact]
    public void Mount_UnknownObserverIndex_Throws()
    {
        var (entity, _) = Setup();
        entity.SpawnSingle(CreateMeta());

        Assert.Throws<InvalidOperationException>(
            () => entity.MountObserverStrategy(entity.Name, "nonexistent"));
    }

    // ── RecoverBindings edge cases ───────────────────────────────────

    [Fact]
    public void RecoverBindings_TargetNotFound_Skips()
    {
        var (entity, _, topology) = SetupWithTopology();
        entity.SpawnSingle(CreateMeta());

        var bindings = new List<StrategyMetaData.ObserverBinding>
        {
            new() { Target = "ghost", ObserverIndices = [_selfWatchIdx] }
        };

        var ex = Record.Exception(() =>
            topology.RecoverBindingsFor(entity, bindings, _ => null));

        Assert.Null(ex);
    }

    // ── Has / Remove observer bindings by target ──────────────────────

    [Fact]
    public void HasObserverBindingTargeting_ExistingTarget_ReturnsTrue()
    {
        var (entity, _, topology) = SetupWithTopology();
        entity.SpawnSingle(CreateMeta());
        entity.MountObserverStrategy(entity.Name, _memoryObservedIdx);

        Assert.True(topology.HasBindingTargetingFrom(entity.Name, entity.Name));
    }

    [Fact]
    public void HasObserverBindingTargeting_NonexistentTarget_ReturnsFalse()
    {
        var (entity, _, topology) = SetupWithTopology();
        entity.SpawnSingle(CreateMeta());

        Assert.False(topology.HasBindingTargetingFrom(entity.Name, "ghost"));
    }

    [Fact]
    public void RemoveAllObserverBindingsTargeting_ClearsBindings()
    {
        var (entity, _, topology) = SetupWithTopology();
        entity.SpawnSingle(CreateMeta());
        entity.MountObserverStrategy(entity.Name, _memoryObservedIdx);
        entity.MountObserverStrategy(entity.Name, _selfWatchIdx);

        topology.RemoveBindingsTargetingFor(entity, entity.Name);

        Assert.False(topology.HasBindingTargetingFrom(entity.Name, entity.Name));
    }

    // ── TeardownOutgoingObserverBindings ──────────────────────────────

    [Fact]
    public void TeardownOutgoingObserverBindings_TriggersOnUnmounted()
    {
        var (entity, _, topology) = SetupWithTopology();
        entity.SpawnSingle(CreateMeta());
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
        host.RecoverFromMetaList([meta]);
        foreach (var e in host.GetEntities())
            if (e is IEntityLifecycle lc)
                lc.FireAfterSpawnHooks();

        var entity = host.FindByName("bob")!;
        Assert.False(entity.IsPendingKill);

        host.RequestKillEntity("bob");
        Assert.True(entity.IsPendingKill);
        Assert.Single(host.GetEntities());

        host.RemoveEntity("bob");
        Assert.Empty(host.GetEntities());
    }

    // ── ClearAll observer cleanup ─────────────────────────────────────


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
        entity.SpawnSingle(CreateMeta("alice"));
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
        entity.SpawnSingle(CreateMeta("self"));
        entity.MountObserverStrategy("self", _selfWatchIdx);
        entity.MountObserverStrategy("self", _multiKeyIdx);

        var meta = entity.SaveSingle();
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
        entity.SpawnSingle(CreateMeta());
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
        var fs = new TestFileSystem();
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
}
