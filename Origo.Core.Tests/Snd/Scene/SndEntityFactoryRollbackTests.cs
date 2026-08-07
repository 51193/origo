using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Node;
using Origo.Core.Abstractions.Scene;
using Origo.Core.DataSource;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Regression tests: <see cref="SndEntityFactory" /> must roll back the
///     created entity when its AfterSpawn hook throws, so a failed spawn never
///     leaves a half-initialized entity (or leaked strategy references)
///     behind on the scene host.
/// </summary>
public class SndEntityFactoryRollbackTests
{
    private const string ThrowingIdx = "spawn_rollback.throwing";
    private const string NormalIdx = "spawn_rollback.normal";
    private const string NormalTwoIdx = "spawn_rollback.normal_two";

    [Fact]
    public void Spawn_AfterSpawnHookThrows_RollsBackEntityAndStrategyReferences()
    {
        var (ctx, host, logger) = CreateContext();

        var ex = Assert.Throws<InvalidOperationException>(
            () => SndEntityFactory.Spawn(host, CreateMeta("E", ThrowingIdx)));
        Assert.Contains("Intentional AfterSpawn failure", ex.Message, StringComparison.Ordinal);

        // The half-initialized entity must not remain visible on the host.
        Assert.Empty(host.GetEntities());
        Assert.Null(host.FindByName("E"));

        // The strategy reference acquired during recovery must be returned.
        ctx.Runtime.SndWorld.StrategyPool.LogPoolLeaks();
        Assert.DoesNotContain(logger.Warnings, w => w.Contains("refCount"));
    }

    [Fact]
    public void SpawnMany_AfterSpawnHookThrows_RollsBackUnfiredEntitiesOnly()
    {
        var (ctx, host, logger) = CreateContext();

        Assert.Throws<InvalidOperationException>(() => SndEntityFactory.SpawnMany(host,
            CreateMeta("E1", NormalIdx),
            CreateMeta("E2", ThrowingIdx),
            CreateMeta("E3", NormalTwoIdx)));

        // E1 was fully spawned (hook fired) and stays; E2 (hook threw) and
        // E3 (created but hook never fired) must be rolled back.
        var survivors = host.GetEntities().Select(e => e.Name).ToList();
        Assert.Contains("E1", survivors);
        Assert.DoesNotContain("E2", survivors);
        Assert.DoesNotContain("E3", survivors);
        Assert.Null(host.FindByName("E2"));
        Assert.Null(host.FindByName("E3"));

        // Rolled-back strategy references must be returned; only E1's
        // legitimate reference may remain.
        ctx.Runtime.SndWorld.StrategyPool.LogPoolLeaks();
        Assert.DoesNotContain(logger.Warnings, w => w.Contains(ThrowingIdx));
        Assert.DoesNotContain(logger.Warnings, w => w.Contains(NormalTwoIdx));
    }

    [Fact]
    public void Spawn_AfterSpawnHookThrows_OnDetachInvalidatingHost_PropagatesOriginalException()
    {
        // Adapter hosts (GodotSndEntity) invalidate the entity wrapper when it
        // is removed from the host: lifecycle delegation throws afterwards. The
        // rollback must tear down before removal so the original AfterSpawn
        // exception is not masked by an ObjectDisposedException.
        var host = new DetachInvalidatingHost();

        var ex = Assert.Throws<InvalidOperationException>(
            () => SndEntityFactory.Spawn(host, new SndMetaData { Name = "E" }));
        Assert.Contains("Intentional AfterSpawn failure", ex.Message, StringComparison.Ordinal);
        Assert.Empty(host.GetEntities());
    }

    private static (SndContext ctx, FullMemorySndSceneHost host, TestLogger logger) CreateContext()
    {
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        runtime.SndWorld.RegisterStrategy(() => new ThrowingAfterSpawnStrategy());
        runtime.SndWorld.RegisterStrategy(() => new NormalStrategy());
        runtime.SndWorld.RegisterStrategy(() => new NormalTwoStrategy());

        var fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver,
            "root", "initial", "entry.json"));
        host.BindWorld(runtime.SndWorld);
        host.BindContext(ctx);

        return (ctx, host, logger);
    }

    private static SndMetaData CreateMeta(string name, string lifecycleIndex) =>
        new()
        {
            Name = name,
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData { LifecycleIndices = [lifecycleIndex] },
            DataMetaData = new DataMetaData()
        };

    [StrategyIndex(ThrowingIdx)]
    private sealed class ThrowingAfterSpawnStrategy : LifecycleStrategyBase
    {
        public override void AfterSpawn(ISndEntity entity, ISndContext ctx) =>
            throw new InvalidOperationException("Intentional AfterSpawn failure.");
    }

    [StrategyIndex(NormalIdx)]
    private sealed class NormalStrategy : LifecycleStrategyBase
    {
    }

    [StrategyIndex(NormalTwoIdx)]
    private sealed class NormalTwoStrategy : LifecycleStrategyBase
    {
    }

    // ── Host that invalidates removed entities (Godot wrapper semantics) ──

    /// <summary>
    ///     Mimics adapter hosts whose entity wrappers become unusable after
    ///     removal (GodotSndEntity detaches its backing entity on removal):
    ///     lifecycle delegation after removal throws ObjectDisposedException.
    /// </summary>
    private sealed class DetachInvalidatingHost : ISndSceneHost
    {
        private readonly List<DetachAwareEntity> _entities = [];

        public ISndEntity CreateEntity(SndMetaData metaData)
        {
            var entity = new DetachAwareEntity(metaData.Name);
            _entities.Add(entity);
            return entity;
        }

        public IReadOnlyCollection<ISndEntity> GetEntities() => [.. _entities];

        public ISndEntity? FindByName(string name) =>
            _entities.FirstOrDefault(e => e.Name == name);

        public IReadOnlyList<SndMetaData> BuildMetaList() =>
            [.. _entities.Select(e => e.BuildMetaData())];

        public void RecoverFromMetaList(IEnumerable<SndMetaData> metaList)
        {
            foreach (var meta in metaList)
                CreateEntity(meta);
        }

        public void RemoveAllEntities() => _entities.Clear();

        public void RemoveEntity(string name)
        {
            var entity = _entities.FirstOrDefault(e => e.Name == name)
                         ?? throw new InvalidOperationException($"No entity with name '{name}'.");
            _entities.Remove(entity);
            entity.Detach();
        }

        public void RequestKillEntity(string name)
        {
        }

        public void ProcessAll(double delta)
        {
        }
    }

    private sealed class DetachAwareEntity : ISndEntity, IEntityLifecycle
    {
        private bool _detached;

        internal DetachAwareEntity(string name) => Name = name;

        internal void Detach() => _detached = true;

        private void ThrowIfDetached() => ObjectDisposedException.ThrowIf(_detached, this);

        public string Name { get; }
        public ISessionRun OwningSession { get; set; } = null!;
        public bool IsPendingKill { get; set; }

        public void SetData<T>(string name, T value) => throw new NotSupportedException();
        public T GetData<T>(string name) where T : notnull => throw new NotSupportedException();
        public (bool found, T? value) TryGetData<T>(string name) => throw new NotSupportedException();
        public bool TryGetData<T>(string name, out T? value) => throw new NotSupportedException();
        public void MountObserverStrategy(string targetName, string observerIndex) => throw new NotSupportedException();
        public void UnmountObserverStrategy(string targetName, string observerIndex) => throw new NotSupportedException();
        public void MountObserverStrategy(ISndEntity target, string observerIndex) => throw new NotSupportedException();
        public void UnmountObserverStrategy(ISndEntity target, string observerIndex) => throw new NotSupportedException();
        public INodeHandle GetNode(string name) => throw new NotSupportedException();
        public IReadOnlyCollection<string> GetNodeNames() => [];
        public void AddStrategy(string index) => throw new NotSupportedException();
        public void RemoveStrategy(string index) => throw new NotSupportedException();
        public void AddActiveStrategy(string index) => throw new NotSupportedException();
        public void RemoveActiveStrategy(string index) => throw new NotSupportedException();
        public object? InvokeStrategy(string strategyIndex, object? input = null) => throw new NotSupportedException();

        public void RecoverForLifecycle(SndMetaData metaData) => throw new NotSupportedException();
        public void FireAfterSpawnHooks() => throw new InvalidOperationException("Intentional AfterSpawn failure.");
        public void FireAfterLoadHooks() => ThrowIfDetached();
        public void FireBeforeSaveHooks() => ThrowIfDetached();
        public void FireBeforeQuitHooks() => ThrowIfDetached();
        public void FireBeforeDeadHooks() => ThrowIfDetached();
        public void ReleaseStrategiesOnly() => ThrowIfDetached();
        public void TeardownOnly() => ThrowIfDetached();
        public void TeardownObserverBindings() => ThrowIfDetached();
        public SndMetaData BuildMetaData() => new() { Name = Name };
    }
}
