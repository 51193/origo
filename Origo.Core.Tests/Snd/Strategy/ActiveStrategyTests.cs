using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.DataSource;
using Origo.Core.Snd;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class ActiveStrategyTests
{
    private const string _queryHpIndex = "active.query.hp";
    private const string _cmdDamageIndex = "active.cmd.damage";
    private const string _entityOnlyIndex = "test.entity.only";

    // ── Invoke basics ──────────────────────────────────────────────────

    [Fact]
    public void Invoke_ReturnsResult()
    {
        var (entity, _, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMetaWithActive([_queryHpIndex])); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        var result = entity.InvokeStrategy(_queryHpIndex);

        Assert.IsType<int>(result);
        Assert.Equal(100, (int)result!);
    }

    [Fact]
    public void Invoke_EntityPassedCorrectly()
    {
        var (entity, _, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMetaWithActive([_queryHpIndex])); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        var name = (string?)entity.InvokeStrategy(_queryHpIndex, "get_name");

        Assert.Equal("E", name);
    }

    [Fact]
    public void Invoke_InputPassedCorrectly()
    {
        var (entity, _, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMetaWithActive([_cmdDamageIndex])); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        var result = entity.InvokeStrategy(_cmdDamageIndex, 42);

        Assert.IsType<string>(result);
        Assert.Equal("dealt 42 damage", (string)result!);
    }

    [Fact]
    public void Invoke_UnregisteredIndex_Throws()
    {
        var (entity, _, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta([])); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            entity.InvokeStrategy("not.exist"));
        Assert.Contains("not.exist", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Invoke_LifecycleStrategyIndex_Throws()
    {
        var (entity, _, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta([_entityOnlyIndex])); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            entity.InvokeStrategy(_entityOnlyIndex));
        Assert.Contains(_entityOnlyIndex, ex.Message, StringComparison.Ordinal);
    }

    // ── Lifecycle ──────────────────────────────────────────────────────

    [Fact]
    public void Spawn_RecoversActiveStrategies()
    {
        var (entity, _, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMetaWithActive([_queryHpIndex])); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        var result = entity.InvokeStrategy(_queryHpIndex);

        Assert.IsType<int>(result);
    }

    [Fact]
    public void Load_RecoversActiveStrategies()
    {
        var (entity, _, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMetaWithActive([_queryHpIndex])); ((IEntityLifecycle)entity).FireAfterLoadHooks();

        var result = entity.InvokeStrategy(_queryHpIndex);

        Assert.IsType<int>(result);
    }

    [Fact]
    public void Quit_ReleasesAllActiveStrategies()
    {
        var (entity, _, _, topology) = SetupWithTopology();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMetaWithActive([_queryHpIndex])); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        DestroySingleEntity(entity, topology, quit: true);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            entity.InvokeStrategy(_queryHpIndex));
        Assert.Contains(_queryHpIndex, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Dead_ReleasesAllActiveStrategies()
    {
        var (entity, _, _, topology) = SetupWithTopology();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMetaWithActive([_queryHpIndex])); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        DestroySingleEntity(entity, topology, quit: false);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            entity.InvokeStrategy(_queryHpIndex));
        Assert.Contains(_queryHpIndex, ex.Message, StringComparison.Ordinal);
    }

    // ── Recover fail-fast (non-active type in ActiveIndices) ───────────

    [Fact]
    public void Load_ActiveIndexWithNonActiveType_Throws()
    {
        var (entity, _, _) = Setup();
        var meta = CreateMeta([], [_entityOnlyIndex]);

        var ex = Assert.Throws<InvalidOperationException>(() => { ((IEntityLifecycle)entity).RecoverForLifecycle(meta); ((IEntityLifecycle)entity).FireAfterLoadHooks(); });

        Assert.Contains(_entityOnlyIndex, ex.Message, StringComparison.Ordinal);
        Assert.Contains("ActiveStrategyBase", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_ActiveIndexWithNonActiveType_RollsBackAcquiredActives()
    {
        var (entity, _, _) = Setup();
        // _queryHpIndex is acquired before the invalid _entityOnlyIndex; recovery must
        // roll it back so no active strategy remains attached after the failure.
        var meta = CreateMeta([], [_queryHpIndex, _entityOnlyIndex]);

        Assert.Throws<InvalidOperationException>(() => { ((IEntityLifecycle)entity).RecoverForLifecycle(meta); ((IEntityLifecycle)entity).FireAfterLoadHooks(); });

        var ex = Assert.Throws<InvalidOperationException>(() => entity.InvokeStrategy(_queryHpIndex));
        Assert.Contains(_queryHpIndex, ex.Message, StringComparison.Ordinal);
    }

    // ── Dynamic Add / Remove ──────────────────────────────────────────

    [Fact]
    public void AddActiveStrategy_Then_Invoke_Works()
    {
        var (entity, _, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta([])); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        entity.AddActiveStrategy(_queryHpIndex);

        var result = entity.InvokeStrategy(_queryHpIndex);

        Assert.Equal(100, (int)result!);
    }

    [Fact]
    public void AddActiveStrategy_Duplicate_Throws()
    {
        var (entity, _, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMetaWithActive([_queryHpIndex])); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            entity.AddActiveStrategy(_queryHpIndex));
        Assert.Contains("already attached", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddActiveStrategy_NonActiveType_Throws()
    {
        var (entity, _, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta([])); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            entity.AddActiveStrategy(_entityOnlyIndex));
        Assert.Contains(_entityOnlyIndex, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddActiveStrategy_NullOrWhitespace_Throws()
    {
        var (entity, _, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta([])); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        Assert.Throws<ArgumentNullException>(() => entity.AddActiveStrategy(null!));
        Assert.Throws<ArgumentException>(() => entity.AddActiveStrategy("  "));
    }

    [Fact]
    public void RemoveActiveStrategy_Then_Invoke_Throws()
    {
        var (entity, _, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMetaWithActive([_queryHpIndex])); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        entity.RemoveActiveStrategy(_queryHpIndex);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            entity.InvokeStrategy(_queryHpIndex));
        Assert.Contains(_queryHpIndex, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RemoveActiveStrategy_NotExists_Throws()
    {
        var (entity, _, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta([])); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        Assert.Throws<InvalidOperationException>(() => entity.RemoveActiveStrategy("not.exist"));
    }

    // ── Serialization ──────────────────────────────────────────────────

    [Fact]
    public void SerializeMetaData_IncludesActiveIndices()
    {
        var (entity, _, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMetaWithActive([_queryHpIndex, _cmdDamageIndex])); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        ((IEntityLifecycle)entity).FireBeforeSaveHooks();
        var meta = ((IEntityLifecycle)entity).BuildMetaData();
        var activeIndices = meta.StrategyMetaData!.ActiveIndices;

        Assert.Contains(_queryHpIndex, activeIndices);
        Assert.Contains(_cmdDamageIndex, activeIndices);
    }

    [Fact]
    public void SerializeMetaData_EntityAndActive_Separated()
    {
        var (entity, _, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta([_entityOnlyIndex], [_queryHpIndex])); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        ((IEntityLifecycle)entity).FireBeforeSaveHooks();
        var meta = ((IEntityLifecycle)entity).BuildMetaData();

        Assert.Contains(_entityOnlyIndex, meta.StrategyMetaData!.LifecycleIndices);
        Assert.DoesNotContain(_queryHpIndex, meta.StrategyMetaData!.LifecycleIndices);
        Assert.Contains(_queryHpIndex, meta.StrategyMetaData!.ActiveIndices);
        Assert.DoesNotContain(_entityOnlyIndex, meta.StrategyMetaData!.ActiveIndices);
    }

    [Fact]
    public void SerializeMetaData_DynamicAdd_Then_Serialized()
    {
        var (entity, _, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta([])); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        entity.AddActiveStrategy(_queryHpIndex);

        ((IEntityLifecycle)entity).FireBeforeSaveHooks();
        var meta = ((IEntityLifecycle)entity).BuildMetaData();

        Assert.Contains(_queryHpIndex, meta.StrategyMetaData!.ActiveIndices);
    }

    [Fact]
    public void SerializeMetaData_DynamicRemove_NotSerialized()
    {
        var (entity, _, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMetaWithActive([_queryHpIndex])); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        entity.RemoveActiveStrategy(_queryHpIndex);

        ((IEntityLifecycle)entity).FireBeforeSaveHooks();
        var meta = ((IEntityLifecycle)entity).BuildMetaData();

        Assert.Empty(meta.StrategyMetaData!.ActiveIndices);
    }

    // ── Mixed strategy type on same entity ─────────────────────────────

    [Fact]
    public void SameEntity_HasBothTypeStrategies()
    {
        var (entity, _, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta([_entityOnlyIndex], [_queryHpIndex])); ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        var processEx = Record.Exception(() => entity.Process(0.016));
        Assert.Null(processEx);

        var result = entity.InvokeStrategy(_queryHpIndex);
        Assert.Equal(100, (int)result!);
    }

    [Fact]
    public void RemoveLifecycleStrategy_LeavesActiveStrategy()
    {
        var (entity, _, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta([_entityOnlyIndex], [_queryHpIndex])); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        entity.RemoveStrategy(_entityOnlyIndex);

        var result = entity.InvokeStrategy(_queryHpIndex);
        Assert.Equal(100, (int)result!);
    }

    [Fact]
    public void RemoveActiveStrategy_LeavesLifecycleStrategy()
    {
        var (entity, _, _) = Setup();
        ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta([_entityOnlyIndex], [_queryHpIndex])); ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        entity.RemoveActiveStrategy(_queryHpIndex);

        var ex = Record.Exception(() => entity.Process(0.016));
        Assert.Null(ex);
    }

    // ── Registration enforcement ───────────────────────────────────────

    [Fact]
    public void ActiveStrategy_StatelessnessEnforced()
    {
        var world = TestFactory.CreateSndWorld();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            world.RegisterStrategy(() => new StatefulActiveStrategy()));
        Assert.Contains("invalid instance members", ex.Message, StringComparison.Ordinal);
        Assert.Contains("_counter", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveStrategy_AutoDiscovered()
    {
        var world = TestFactory.CreateSndWorld();
        // Register via the pool the same way OrigoAutoInitializer would.
        world.RegisterStrategy(() => new StatelessActiveStrategy());

        Assert.Contains(StatelessActiveStrategy.IndexConst, world.GetRegisteredStrategyIndices());
    }

    [Fact]
    public void ActiveStrategy_MissingAttribute_Throws()
    {
        var world = TestFactory.CreateSndWorld();

        Assert.Throws<InvalidOperationException>(() =>
            world.RegisterStrategy(() => new UnannotatedActiveStrategy()));
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static (SndEntity entity, ISndContext ctx, TestLogger logger) Setup()
    {
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        runtime.SndWorld.RegisterStrategy(() => new QueryHpStrategy());
        runtime.SndWorld.RegisterStrategy(() => new CmdDamageStrategy());
        runtime.SndWorld.RegisterStrategy(() => new EntityOnlyStrategy());
        var fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var nodeFactory = new TestNodeFactory();
        var observerTopology = new ObserverTopology(runtime.SndWorld.StrategyPool, logger);
        observerTopology.BindContext(ctx);
        var entity = runtime.SndWorld.CreateEntity(nodeFactory, ctx, logger, observerTopology);
        return (entity, ctx, logger);
    }

    private static (SndEntity entity, ISndContext ctx, TestLogger logger, ObserverTopology topology)
        SetupWithTopology()
    {
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        runtime.SndWorld.RegisterStrategy(() => new QueryHpStrategy());
        runtime.SndWorld.RegisterStrategy(() => new CmdDamageStrategy());
        runtime.SndWorld.RegisterStrategy(() => new EntityOnlyStrategy());
        var fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var nodeFactory = new TestNodeFactory();
        var observerTopology = new ObserverTopology(runtime.SndWorld.StrategyPool, logger);
        observerTopology.BindContext(ctx);
        var entity = runtime.SndWorld.CreateEntity(nodeFactory, ctx, logger, observerTopology);
        return (entity, ctx, logger, observerTopology);
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

    private static SndMetaData CreateMeta(string[] lifecycleIndices,
        string[]? activeIndices = null)
    {
        return new SndMetaData
        {
            Name = "E",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData
            {
                LifecycleIndices = [.. lifecycleIndices],
                ActiveIndices = [.. activeIndices ?? []]
            },
            DataMetaData = new DataMetaData()
        };
    }

    private static SndMetaData CreateMetaWithActive(string[] activeIndices) =>
        CreateMeta([], activeIndices);

    // ── Test strategies ────────────────────────────────────────────────

    [StrategyIndex(_queryHpIndex)]
    private sealed class QueryHpStrategy : ActiveStrategyBase
    {
        public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input)
        {
            if (input is string s && s == "get_name")
                return entity.Name;
            return 100;
        }
    }

    [StrategyIndex(_cmdDamageIndex)]
    private sealed class CmdDamageStrategy : ActiveStrategyBase
    {
        public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input)
        {
            var dmg = input is int i ? i : 0;
            return $"dealt {dmg} damage";
        }
    }

    [StrategyIndex(_entityOnlyIndex)]
    private sealed class EntityOnlyStrategy : LifecycleStrategyBase
    {
    }

    [StrategyIndex("active.stateful.test")]
    private sealed class StatefulActiveStrategy : ActiveStrategyBase
    {
        private int _counter;

        public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input) => _counter++;
    }

    [StrategyIndex(IndexConst)]
    private sealed class StatelessActiveStrategy : ActiveStrategyBase
    {
        public const string IndexConst = "active.stateless.auto";

        public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input) => null;
    }

    private sealed class UnannotatedActiveStrategy : ActiveStrategyBase
    {
        public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input) => null;
    }
}
