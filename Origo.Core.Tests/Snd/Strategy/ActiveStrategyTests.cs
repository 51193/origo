using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class ActiveStrategyTests
{
    private const string QueryHpIndex = "active.query.hp";
    private const string CmdDamageIndex = "active.cmd.damage";
    private const string EntityOnlyIndex = "test.entity.only";

    // ── Invoke basics ──────────────────────────────────────────────────

    [Fact]
    public void Invoke_ReturnsResult()
    {
        var (entity, ctx, _) = Setup();
        entity.SpawnSingle(CreateMetaWithActive(new[] { QueryHpIndex }));

        var result = entity.InvokeStrategy(QueryHpIndex);

        Assert.IsType<int>(result);
        Assert.Equal(100, (int)result!);
    }

    [Fact]
    public void Invoke_EntityPassedCorrectly()
    {
        var (entity, ctx, _) = Setup();
        entity.SpawnSingle(CreateMetaWithActive(new[] { QueryHpIndex }));

        var name = (string?)entity.InvokeStrategy(QueryHpIndex, "get_name");

        Assert.Equal("E", name);
    }

    [Fact]
    public void Invoke_InputPassedCorrectly()
    {
        var (entity, ctx, _) = Setup();
        entity.SpawnSingle(CreateMetaWithActive(new[] { CmdDamageIndex }));

        var result = entity.InvokeStrategy(CmdDamageIndex, 42);

        Assert.IsType<string>(result);
        Assert.Equal("dealt 42 damage", (string)result!);
    }

    [Fact]
    public void Invoke_UnregisteredIndex_Throws()
    {
        var (entity, _, _) = Setup();
        entity.SpawnSingle(CreateMeta(Array.Empty<string>()));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            entity.InvokeStrategy("not.exist"));
        Assert.Contains("not.exist", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Invoke_EntityStrategyIndex_Throws()
    {
        var (entity, ctx, _) = Setup();
        entity.SpawnSingle(CreateMeta(new[] { EntityOnlyIndex }));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            entity.InvokeStrategy(EntityOnlyIndex));
        Assert.Contains(EntityOnlyIndex, ex.Message, StringComparison.Ordinal);
    }

    // ── Lifecycle ──────────────────────────────────────────────────────

    [Fact]
    public void Spawn_RecoversActiveStrategies()
    {
        var (entity, ctx, _) = Setup();
        entity.SpawnSingle(CreateMetaWithActive(new[] { QueryHpIndex }));

        var result = entity.InvokeStrategy(QueryHpIndex);

        Assert.IsType<int>(result);
    }

    [Fact]
    public void Load_RecoversActiveStrategies()
    {
        var (entity, ctx, _) = Setup();
        entity.LoadSingle(CreateMetaWithActive(new[] { QueryHpIndex }));

        var result = entity.InvokeStrategy(QueryHpIndex);

        Assert.IsType<int>(result);
    }

    [Fact]
    public void Quit_ReleasesAllActiveStrategies()
    {
        var (entity, ctx, _) = Setup();
        entity.SpawnSingle(CreateMetaWithActive(new[] { QueryHpIndex }));
        entity.QuitSingle();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            entity.InvokeStrategy(QueryHpIndex));
        Assert.Contains(QueryHpIndex, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Dead_ReleasesAllActiveStrategies()
    {
        var (entity, ctx, _) = Setup();
        entity.SpawnSingle(CreateMetaWithActive(new[] { QueryHpIndex }));
        entity.DeadSingle();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            entity.InvokeStrategy(QueryHpIndex));
        Assert.Contains(QueryHpIndex, ex.Message, StringComparison.Ordinal);
    }

    // ── Dynamic Add / Remove ──────────────────────────────────────────

    [Fact]
    public void AddActiveStrategy_Then_Invoke_Works()
    {
        var (entity, _, _) = Setup();
        entity.SpawnSingle(CreateMeta(Array.Empty<string>()));
        entity.AddActiveStrategy(QueryHpIndex);

        var result = entity.InvokeStrategy(QueryHpIndex);

        Assert.Equal(100, (int)result!);
    }

    [Fact]
    public void AddActiveStrategy_Duplicate_Throws()
    {
        var (entity, _, _) = Setup();
        entity.SpawnSingle(CreateMetaWithActive(new[] { QueryHpIndex }));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            entity.AddActiveStrategy(QueryHpIndex));
        Assert.Contains("already attached", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddActiveStrategy_NonActiveType_Throws()
    {
        var (entity, _, _) = Setup();
        entity.SpawnSingle(CreateMeta(Array.Empty<string>()));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            entity.AddActiveStrategy(EntityOnlyIndex));
        Assert.Contains(EntityOnlyIndex, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddActiveStrategy_NullOrWhitespace_Throws()
    {
        var (entity, _, _) = Setup();
        entity.SpawnSingle(CreateMeta(Array.Empty<string>()));

        Assert.Throws<ArgumentException>(() => entity.AddActiveStrategy(null!));
        Assert.Throws<ArgumentException>(() => entity.AddActiveStrategy("  "));
    }

    [Fact]
    public void RemoveActiveStrategy_Then_Invoke_Throws()
    {
        var (entity, _, _) = Setup();
        entity.SpawnSingle(CreateMetaWithActive(new[] { QueryHpIndex }));
        entity.RemoveActiveStrategy(QueryHpIndex);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            entity.InvokeStrategy(QueryHpIndex));
        Assert.Contains(QueryHpIndex, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RemoveActiveStrategy_NotExists_Noop()
    {
        var (entity, _, _) = Setup();
        entity.SpawnSingle(CreateMeta(Array.Empty<string>()));

        var ex = Record.Exception(() => entity.RemoveActiveStrategy("not.exist"));
        Assert.Null(ex);
    }

    // ── Serialization ──────────────────────────────────────────────────

    [Fact]
    public void SerializeMetaData_IncludesActiveIndices()
    {
        var (entity, _, _) = Setup();
        entity.SpawnSingle(CreateMetaWithActive(new[] { QueryHpIndex, CmdDamageIndex }));

        var meta = entity.SaveSingle();
        var activeIndices = meta.StrategyMetaData!.ActiveIndices;

        Assert.Contains(QueryHpIndex, activeIndices);
        Assert.Contains(CmdDamageIndex, activeIndices);
    }

    [Fact]
    public void SerializeMetaData_EntityAndActive_Separated()
    {
        var (entity, _, _) = Setup();
        entity.SpawnSingle(CreateMeta(new[] { EntityOnlyIndex }, new[] { QueryHpIndex }));

        var meta = entity.SaveSingle();

        Assert.Contains(EntityOnlyIndex, meta.StrategyMetaData!.EntityIndices);
        Assert.DoesNotContain(QueryHpIndex, meta.StrategyMetaData!.EntityIndices);
        Assert.Contains(QueryHpIndex, meta.StrategyMetaData!.ActiveIndices);
        Assert.DoesNotContain(EntityOnlyIndex, meta.StrategyMetaData!.ActiveIndices);
    }

    [Fact]
    public void SerializeMetaData_DynamicAdd_Then_Serialized()
    {
        var (entity, _, _) = Setup();
        entity.SpawnSingle(CreateMeta(Array.Empty<string>()));
        entity.AddActiveStrategy(QueryHpIndex);

        var meta = entity.SaveSingle();

        Assert.Contains(QueryHpIndex, meta.StrategyMetaData!.ActiveIndices);
    }

    [Fact]
    public void SerializeMetaData_DynamicRemove_NotSerialized()
    {
        var (entity, _, _) = Setup();
        entity.SpawnSingle(CreateMetaWithActive(new[] { QueryHpIndex }));
        entity.RemoveActiveStrategy(QueryHpIndex);

        var meta = entity.SaveSingle();

        Assert.Empty(meta.StrategyMetaData!.ActiveIndices);
    }

    // ── Mixed strategy type on same entity ─────────────────────────────

    [Fact]
    public void SameEntity_HasBothTypeStrategies()
    {
        var (entity, _, _) = Setup();
        entity.SpawnSingle(CreateMeta(new[] { EntityOnlyIndex }, new[] { QueryHpIndex }));

        var processEx = Record.Exception(() => entity.Process(0.016));
        Assert.Null(processEx);

        var result = entity.InvokeStrategy(QueryHpIndex);
        Assert.Equal(100, (int)result!);
    }

    [Fact]
    public void RemoveEntityStrategy_LeavesActiveStrategy()
    {
        var (entity, _, _) = Setup();
        entity.SpawnSingle(CreateMeta(new[] { EntityOnlyIndex }, new[] { QueryHpIndex }));
        entity.RemoveStrategy(EntityOnlyIndex);

        var result = entity.InvokeStrategy(QueryHpIndex);
        Assert.Equal(100, (int)result!);
    }

    [Fact]
    public void RemoveActiveStrategy_LeavesEntityStrategy()
    {
        var (entity, _, _) = Setup();
        entity.SpawnSingle(CreateMeta(new[] { EntityOnlyIndex }, new[] { QueryHpIndex }));
        entity.RemoveActiveStrategy(QueryHpIndex);

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
        var fs = new TestFileSystem();
        var ctx = new SndContext(new SndContextParameters(runtime, fs, "root", "initial", "entry.json"));
        var nodeFactory = new TestNodeFactory();
        var entity = runtime.SndWorld.CreateEntity(nodeFactory, ctx, logger);
        return (entity, ctx, logger);
    }

    private static SndMetaData CreateMeta(string[] entityIndices,
        string[]? activeIndices = null)
    {
        return new SndMetaData
        {
            Name = "E",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData
            {
                EntityIndices = new List<string>(entityIndices),
                ActiveIndices = new List<string>(activeIndices ?? Array.Empty<string>())
            },
            DataMetaData = new DataMetaData()
        };
    }

    private static SndMetaData CreateMetaWithActive(string[] activeIndices) =>
        CreateMeta(Array.Empty<string>(), activeIndices);

    // ── Test strategies ────────────────────────────────────────────────

    [StrategyIndex(QueryHpIndex)]
    private sealed class QueryHpStrategy : ActiveStrategyBase
    {
        public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input)
        {
            if (input is string s && s == "get_name")
                return entity.Name;
            return 100;
        }
    }

    [StrategyIndex(CmdDamageIndex)]
    private sealed class CmdDamageStrategy : ActiveStrategyBase
    {
        public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input)
        {
            var dmg = input is int i ? i : 0;
            return $"dealt {dmg} damage";
        }
    }

    [StrategyIndex(EntityOnlyIndex)]
    private sealed class EntityOnlyStrategy : EntityStrategyBase
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
