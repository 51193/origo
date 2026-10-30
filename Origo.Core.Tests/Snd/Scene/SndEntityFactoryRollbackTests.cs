using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    private const string MountSiblingIdx = "spawn_rollback.mount_sibling";
    private const string RecordingObserverIdx = "spawn_rollback.recording_observer";
    private const string MountRemountSiblingIdx = "spawn_rollback.mount_remount_sibling";
    private const string RemountObserverIdx = "spawn_rollback.remount_observer";
    private const string MountOutgoingSiblingIdx = "spawn_rollback.mount_outgoing_sibling";
    private const string OutgoingRemountObserverIdx = "spawn_rollback.outgoing_remount_observer";

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
    public void Spawn_DuplicateName_ThrowsBeforeCreating()
    {
        var (_, host, _) = CreateContext();

        SndEntityFactory.Spawn(host, CreateMeta("E", NormalIdx));

        var ex = Assert.Throws<InvalidOperationException>(
            () => SndEntityFactory.Spawn(host, CreateMeta("E", NormalTwoIdx)));
        Assert.Contains("already exists", ex.Message, StringComparison.Ordinal);
        Assert.Single(host.GetEntities());
    }

    [Fact]
    public void SpawnMany_DuplicateNameInBatch_ThrowsBeforeStagingAnyEntity()
    {
        var (_, host, _) = CreateContext();

        Assert.Throws<InvalidOperationException>(() => SndEntityFactory.SpawnMany(host,
            CreateMeta("E1", NormalIdx),
            CreateMeta("E1", NormalTwoIdx)));

        Assert.Empty(host.GetEntities());
        Assert.Null(host.FindByName("E1"));
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

    [Fact]
    public void SpawnMany_CreateEntityThrows_RollsBackAlreadyStagedEntities()
    {
        var (ctx, host, logger) = CreateContext();

        // E2 references a strategy index that has no registered factory, so
        // its creation fails inside the staging loop. The already-created
        // entities (E1) must be rolled back too: a spawn batch that fails
        // mid-creation must not leave half-staged entities behind that never
        // receive their AfterSpawn hooks.
        Assert.Throws<InvalidOperationException>(() => SndEntityFactory.SpawnMany(host,
            CreateMeta("E1", NormalIdx),
            CreateMeta("E2", "spawn_rollback.missing"),
            CreateMeta("E3", NormalTwoIdx)));

        Assert.Empty(host.GetEntities());
        Assert.Null(host.FindByName("E1"));
        Assert.Null(host.FindByName("E2"));
        Assert.Null(host.FindByName("E3"));

        // E1's strategy reference acquired during staging must be returned.
        ctx.Runtime.SndWorld.StrategyPool.LogPoolLeaks();
        Assert.DoesNotContain(logger.Warnings, w => w.Contains("refCount"));
    }

    [Fact]
    public void Spawn_AfterSpawnHookThrows_WhenRollbackStepAlsoThrows_PropagatesOriginalAndCompletesRollback()
    {
        // A throwing rollback step (e.g. an OnUnmounted hook inside
        // TeardownObserverBindings) must neither mask the original AfterSpawn
        // failure nor skip the remaining rollback steps: the entity must
        // still end up fully released and removed from the host.
        var host = new ThrowingRollbackHost();

        var ex = Assert.Throws<InvalidOperationException>(
            () => SndEntityFactory.Spawn(host, new SndMetaData { Name = "E" }));
        Assert.Contains("Intentional AfterSpawn failure", ex.Message, StringComparison.Ordinal);

        Assert.Equal(
            ["ReleaseStrategiesOnly", "TeardownOnly", "RemoveEntity"],
            host.Steps);
        Assert.Empty(host.GetEntities());
    }

    [Fact]
    public void Spawn_AfterSpawnHookThrows_TopologyDiscoveryThrows_PropagatesOriginalAndStillRollsBack()
    {
        // An adapter scene host whose observer topology is unavailable throws
        // from IObserverTopologyHost.ObserverTopology. Incoming-binding
        // discovery is best-effort: the original AfterSpawn failure must
        // propagate unchanged and the remaining teardown steps must still run.
        var host = new UnboundTopologyRollbackHost();

        var ex = Assert.Throws<InvalidOperationException>(
            () => SndEntityFactory.Spawn(host, new SndMetaData { Name = "E" }));
        Assert.Contains("Intentional AfterSpawn failure", ex.Message, StringComparison.Ordinal);

        Assert.Equal(
            ["ReleaseStrategiesOnly", "TeardownOnly", "RemoveEntity"],
            host.Steps);
        Assert.Empty(host.GetEntities());
    }

    [Fact]
    public void SpawnMany_AfterSpawnHookThrows_RemovesIncomingObserverBindingsFromSurvivors()
    {
        // A survivor whose AfterSpawn ran before the failure may already have
        // mounted an observer strategy onto a later staged entity (two-phase
        // staging deliberately makes siblings resolvable). When that later
        // entity is rolled back, the survivor's incoming binding must be torn
        // down too: a persisted binding to a missing entity fails the strict
        // load ("target ... does not exist") and makes the save unloadable.
        RecordingObserver.UnmountedTargets.Clear();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new MountSiblingObserverStrategy())
            .WithStrategy(() => new ThrowingAfterSpawnStrategy())
            .WithStrategy(() => new RecordingObserver())
            .Build();

        var session = harness.GameSession;
        var ex = Assert.Throws<InvalidOperationException>(() => session.SpawnMany(
            CreateMeta("E1", MountSiblingIdx),
            CreateMeta("E2", ThrowingIdx)));
        Assert.Contains("Intentional AfterSpawn failure", ex.Message, StringComparison.Ordinal);

        // E1 was fully spawned before E2's hook threw and must survive; E2 is
        // rolled back.
        var survivor = session.FindByName("E1");
        Assert.NotNull(survivor);
        Assert.Null(session.FindByName("E2"));

        // The incoming binding was cleaned up with its OnUnmounted hook fired.
        Assert.Contains("E2", RecordingObserver.UnmountedTargets);

        // No dangling edge remains in the survivor's serialized metadata.
        var survivorMeta = ((IEntityLifecycle)survivor!).BuildMetaData();
        Assert.NotNull(survivorMeta.StrategyMetaData);
        Assert.Empty(survivorMeta.StrategyMetaData!.ObserverIndices);

        // Save + full reload must succeed and the reloaded survivor must still
        // have no binding to the rolled-back entity.
        harness.SaveAndReload("rollback_slot");

        var reloadedSession = harness.Runtime.SessionManager.TryGet("game");
        Assert.NotNull(reloadedSession);
        var reloadedSurvivor = reloadedSession!.FindByName("E1");
        Assert.NotNull(reloadedSurvivor);
        var reloadedMeta = ((IEntityLifecycle)reloadedSurvivor!).BuildMetaData();
        Assert.NotNull(reloadedMeta.StrategyMetaData);
        Assert.Empty(reloadedMeta.StrategyMetaData!.ObserverIndices);
    }

    [Fact]
    public void SpawnMany_AfterSpawnHookThrows_RetriesIncomingBindingCreatedByOnUnmounted()
    {
        // A user OnUnmounted hook may mount a fresh observer binding onto the
        // dying target while its previous binding is being removed. The rollback
        // retries the incoming-binding cleanup a bounded number of times, so the
        // hook's replacement binding is removed too instead of dangling in the
        // survivor's persisted metadata.
        RemountOnUnmountObserver.Reset();
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new MountRemountSiblingObserverStrategy())
            .WithStrategy(() => new ThrowingAfterSpawnStrategy())
            .WithStrategy(() => new RemountOnUnmountObserver())
            .Build();

        var session = harness.GameSession;
        var ex = Assert.Throws<InvalidOperationException>(() => session.SpawnMany(
            CreateMeta("E1", MountRemountSiblingIdx),
            CreateMeta("E2", ThrowingIdx)));
        Assert.Contains("Intentional AfterSpawn failure", ex.Message, StringComparison.Ordinal);

        var survivor = session.FindByName("E1");
        Assert.NotNull(survivor);
        Assert.Null(session.FindByName("E2"));

        // The remounting hook ran twice (initial binding + its replacement) and
        // the retry removed both; the survivor persists no dangling edge.
        Assert.Equal(2, RemountOnUnmountObserver.UnmountCount);
        Assert.Equal(1, RemountOnUnmountObserver.RemountCount);
        var survivorMeta = ((IEntityLifecycle)survivor!).BuildMetaData();
        Assert.NotNull(survivorMeta.StrategyMetaData);
        Assert.Empty(survivorMeta.StrategyMetaData!.ObserverIndices);
    }

    [Fact]
    public void SpawnMany_AfterSpawnHookThrows_RemovesBindingCreatedByOutgoingOnUnmounted()
    {
        // The doomed entity also holds an outgoing observer binding. Its teardown
        // fires OnUnmounted, which mounts a fresh survivor -> dying binding. The
        // post-teardown incoming cleanup pass must remove that binding too,
        // otherwise the survivor persists a dangling edge that strict recovery
        // rejects on the next load.
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new NormalStrategy())
            .WithStrategy(() => new MountOutgoingSiblingObserverStrategy())
            .WithStrategy(() => new OutgoingRemountOnUnmountedObserver())
            .WithStrategy(() => new RecordingObserver())
            .Build();

        var session = harness.GameSession;
        var ex = Assert.Throws<InvalidOperationException>(() => session.SpawnMany(
            CreateMeta("E1", NormalIdx),
            CreateMeta("E2", MountOutgoingSiblingIdx)));
        Assert.Contains("Intentional AfterSpawn failure", ex.Message, StringComparison.Ordinal);

        var survivor = session.FindByName("E1");
        Assert.NotNull(survivor);
        Assert.Null(session.FindByName("E2"));

        var survivorMeta = ((IEntityLifecycle)survivor!).BuildMetaData();
        Assert.NotNull(survivorMeta.StrategyMetaData);
        Assert.Empty(survivorMeta.StrategyMetaData!.ObserverIndices);

        // Strict read would reject a survivor -> missing-entity binding.
        _ = harness.SaveAndReload("outgoing_remount_slot");
        var reloaded = harness.Context.Runtime.SessionManager.TryGet("game");
        Assert.NotNull(reloaded);
        Assert.NotNull(reloaded!.FindByName("E1"));
        Assert.Empty(((IEntityLifecycle)reloaded.FindByName("E1")!).BuildMetaData()
            .StrategyMetaData!.ObserverIndices);
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

    // ── Cross-entity observer binding made from an earlier sibling's hook ──

    /// <summary>
    ///     Survivor strategy: mounts an observer binding onto the later staged
    ///     sibling "E2" during AfterSpawn. SpawnMany stages every entity before
    ///     firing hooks, so the sibling is resolvable here.
    /// </summary>
    [StrategyIndex(MountSiblingIdx)]
    private sealed class MountSiblingObserverStrategy : LifecycleStrategyBase
    {
        public override void AfterSpawn(ISndEntity entity, ISndContext ctx)
        {
            var target = entity.OwningSession.FindByName("E2")
                         ?? throw new InvalidOperationException("Test setup: E2 is not staged.");
            entity.MountObserverStrategy(target, RecordingObserverIdx);
        }
    }

    /// <summary>Observer whose unmount target names are recorded for assertions.</summary>
    [StrategyIndex(RecordingObserverIdx)]
    [ObserveData("hp")]
    private sealed class RecordingObserver : ObserverStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _unmountedTargets = new();

        public static List<string> UnmountedTargets => _unmountedTargets.Value ??= [];

        public override void OnUnmounted(ISndEntity entity, ISndContext ctx, ISndEntity target) =>
            UnmountedTargets.Add(target.Name);
    }

    // ── Observer binding remounted from OnUnmounted during rollback cleanup ──

    /// <summary>
    ///     Survivor strategy: mounts the remounting observer onto the later
    ///     staged sibling "E2" during AfterSpawn.
    /// </summary>
    [StrategyIndex(MountRemountSiblingIdx)]
    private sealed class MountRemountSiblingObserverStrategy : LifecycleStrategyBase
    {
        public override void AfterSpawn(ISndEntity entity, ISndContext ctx)
        {
            var target = entity.OwningSession.FindByName("E2")
                         ?? throw new InvalidOperationException("Test setup: E2 is not staged.");
            entity.MountObserverStrategy(target, RemountObserverIdx);
        }
    }

    /// <summary>
    ///     Observer whose OnUnmounted hook mounts a replacement binding onto the
    ///     dying target once, exercising the rollback's bounded incoming-binding
    ///     cleanup retry.
    /// </summary>
    [StrategyIndex(RemountObserverIdx)]
    private sealed class RemountOnUnmountObserver : ObserverStrategyBase
    {
        private static readonly AsyncLocal<int> _unmountCount = new();
        private static readonly AsyncLocal<int> _remountCount = new();
        private static readonly AsyncLocal<int> _remountLimit = new();

        public static int UnmountCount => _unmountCount.Value;
        public static int RemountCount => _remountCount.Value;

        /// <summary>How many times this hook may remount; unset or zero means once.</summary>
        public static int RemountLimit
        {
            get => _remountLimit.Value == 0 ? 1 : _remountLimit.Value;
            set => _remountLimit.Value = value;
        }

        public override void OnUnmounted(ISndEntity entity, ISndContext ctx, ISndEntity target)
        {
            _unmountCount.Value++;
            if (_remountCount.Value >= RemountLimit)
                return;

            _remountCount.Value++;
            entity.MountObserverStrategy(target, RemountObserverIdx);
        }

        public static void Reset()
        {
            _unmountCount.Value = 0;
            _remountCount.Value = 0;
            _remountLimit.Value = 0;
        }
    }

    // ── Survivor -> dying binding created from an outgoing OnUnmounted hook ──

    /// <summary>
    ///     Doomed strategy: mounts an outgoing observer binding from the doomed
    ///     entity onto the surviving sibling "E1", then throws so the entity is
    ///     rolled back.
    /// </summary>
    [StrategyIndex(MountOutgoingSiblingIdx)]
    private sealed class MountOutgoingSiblingObserverStrategy : LifecycleStrategyBase
    {
        public override void AfterSpawn(ISndEntity entity, ISndContext ctx)
        {
            var target = entity.OwningSession.FindByName("E1")
                         ?? throw new InvalidOperationException("Test setup: E1 is not staged.");
            entity.MountObserverStrategy(target, OutgoingRemountObserverIdx);
            throw new InvalidOperationException("Intentional AfterSpawn failure.");
        }
    }

    /// <summary>
    ///     Observer whose OnUnmounted hook (fired while the doomed entity's
    ///     outgoing bindings are torn down) mounts a survivor -> dying binding.
    /// </summary>
    [StrategyIndex(OutgoingRemountObserverIdx)]
    private sealed class OutgoingRemountOnUnmountedObserver : ObserverStrategyBase
    {
        public override void OnUnmounted(ISndEntity entity, ISndContext ctx, ISndEntity target) =>
            target.MountObserverStrategy(entity, RecordingObserverIdx);
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

    // ── Host whose rollback teardown step throws (OnUnmounted hook semantics) ──

    /// <summary>
    ///     Mimics an entity whose observer-binding teardown throws during
    ///     rollback (e.g. a throwing OnUnmounted hook inside
    ///     TeardownObserverBindings) while the remaining steps succeed.
    /// </summary>
    private sealed class ThrowingRollbackHost : ISndSceneHost
    {
        private readonly List<ISndEntity> _entities = [];

        public List<string> Steps { get; } = [];

        public ISndEntity CreateEntity(SndMetaData metaData)
        {
            var entity = new RollbackStepEntity(Steps);
            _entities.Add(entity);
            return entity;
        }

        public IReadOnlyCollection<ISndEntity> GetEntities() => [.. _entities];

        public ISndEntity? FindByName(string name) =>
            _entities.FirstOrDefault(e => e.Name == name);

        public IReadOnlyList<SndMetaData> BuildMetaList() => [];

        public void RecoverFromMetaList(IEnumerable<SndMetaData> metaList)
        {
        }

        public void RemoveAllEntities() => _entities.Clear();

        public void RemoveEntity(string name)
        {
            Steps.Add("RemoveEntity");
            var entity = _entities.FirstOrDefault(e => e.Name == name)
                         ?? throw new InvalidOperationException($"No entity with name '{name}'.");
            _entities.Remove(entity);
        }

        public void RequestKillEntity(string name)
        {
        }

        public void ProcessAll(double delta)
        {
        }
    }

    // ── Host whose observer topology is unavailable (unbound adapter host) ──

    /// <summary>
    ///     Mimics an adapter scene host whose <see cref="IObserverTopologyHost" />
    ///     topology accessor throws because the host was never bound. The
    ///     rollback's incoming-binding discovery must treat that as best-effort
    ///     and still run the teardown steps.
    /// </summary>
    private sealed class UnboundTopologyRollbackHost : ISndSceneHost, IObserverTopologyHost
    {
        private readonly List<ISndEntity> _entities = [];

        public List<string> Steps { get; } = [];

        public ObserverTopology ObserverTopology =>
            throw new InvalidOperationException("observer topology is not bound");

        public ISndEntity CreateEntity(SndMetaData metaData)
        {
            var entity = new RollbackStepEntity(Steps);
            _entities.Add(entity);
            return entity;
        }

        public IReadOnlyCollection<ISndEntity> GetEntities() => [.. _entities];

        public ISndEntity? FindByName(string name) =>
            _entities.FirstOrDefault(e => e.Name == name);

        public IReadOnlyList<SndMetaData> BuildMetaList() => [];

        public void RecoverFromMetaList(IEnumerable<SndMetaData> metaList)
        {
        }

        public void RemoveAllEntities() => _entities.Clear();

        public void RemoveEntity(string name)
        {
            Steps.Add("RemoveEntity");
            var entity = _entities.FirstOrDefault(e => e.Name == name)
                         ?? throw new InvalidOperationException($"No entity with name '{name}'.");
            _entities.Remove(entity);
        }

        public void RequestKillEntity(string name)
        {
        }

        public void ProcessAll(double delta)
        {
        }
    }

    private sealed class RollbackStepEntity : ISndEntity, IEntityLifecycle
    {
        private readonly List<string> _steps;

        internal RollbackStepEntity(List<string> steps) => _steps = steps;

        public string Name => "E";
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
        public void FireAfterLoadHooks() => throw new NotSupportedException();
        public void FireBeforeSaveHooks() => throw new NotSupportedException();
        public void FireBeforeQuitHooks() => throw new NotSupportedException();
        public void FireBeforeDeadHooks() => throw new NotSupportedException();
        public void ReleaseStrategiesOnly() => _steps.Add("ReleaseStrategiesOnly");
        public void TeardownOnly() => _steps.Add("TeardownOnly");
        public void TeardownObserverBindings() => throw new InvalidOperationException("Intentional teardown failure.");
        public SndMetaData BuildMetaData() => new() { Name = Name };
    }

    [Fact]
    public void SpawnMany_AfterSpawnHookThrows_RemountingHookCleanupIsBounded()
    {
        // A pathological OnUnmounted hook that remounts on every teardown must
        // not make rollback loop forever: incoming-binding cleanup is bounded
        // and the original spawn failure still propagates.
        RemountOnUnmountObserver.Reset();
        RemountOnUnmountObserver.RemountLimit = int.MaxValue;
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new MountRemountSiblingObserverStrategy())
            .WithStrategy(() => new ThrowingAfterSpawnStrategy())
            .WithStrategy(() => new RemountOnUnmountObserver())
            .Build();

        var session = harness.GameSession;
        var ex = Assert.Throws<InvalidOperationException>(() => session.SpawnMany(
            CreateMeta("E1", MountRemountSiblingIdx),
            CreateMeta("E2", ThrowingIdx)));
        Assert.Contains("Intentional AfterSpawn failure", ex.Message, StringComparison.Ordinal);

        // Two bounded cleanup passes (before and after the outgoing teardown)
        // each give up after three attempts because the hook remounts forever.
        Assert.Equal(6, RemountOnUnmountObserver.UnmountCount);
        Assert.Equal(6, RemountOnUnmountObserver.RemountCount);
        Assert.Null(session.FindByName("E2"));

        // Documented give-up outcome: the survivor keeps one live binding to the
        // removed entity. Strict save recovery rejects the resulting save, so
        // the limitation is observable rather than silently persisted.
        var survivor = session.FindByName("E1");
        Assert.NotNull(survivor);
        var survivorMeta = ((IEntityLifecycle)survivor!).BuildMetaData();
        Assert.NotNull(survivorMeta.StrategyMetaData);
        Assert.NotEmpty(survivorMeta.StrategyMetaData!.ObserverIndices);
        var reloadFailure = Assert.Throws<InvalidOperationException>(
            () => { _ = harness.SaveAndReload("remount_giveup_slot"); });
        Assert.Contains("no entity with that name exists", reloadFailure.Message, StringComparison.Ordinal);
    }
}
