using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.DataSource;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Origo.Core.Snd.Scene;
using Xunit;

namespace Origo.Core.Tests;

public class SndEntityOwningSessionTests
{
    [Fact]
    public void CreateEntity_WithOwningSession_BindsOwningSessionToEntity()
    {
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        host.BindWorld(world);
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var ctx = new SndContext(new SndContextParameters(runtime,
            TestFactory.CreateIoGateway(new TestMemoryFileSystem()),
            TestFactory.CreateFileMetaAccess(new TestMemoryFileSystem()),
            TestFactory.CreatePathResolver(new TestMemoryFileSystem()),
            "root", "initial", "entry.json"));
        host.BindContext(ctx);

        var session = new StubSessionRun();
        ((IOwningSessionBindable)host).SetOwningSession(session);

        var entity = host.CreateEntity(CreateMeta("E"));

        Assert.NotNull(entity.OwningSession);
        Assert.Same(session, entity.OwningSession);
    }

    [Fact]
    public void CreateEntity_WithoutOwningSession_OwningSessionThrows()
    {
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        host.BindWorld(world);
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var ctx = new SndContext(new SndContextParameters(runtime,
            TestFactory.CreateIoGateway(new TestMemoryFileSystem()),
            TestFactory.CreateFileMetaAccess(new TestMemoryFileSystem()),
            TestFactory.CreatePathResolver(new TestMemoryFileSystem()),
            "root", "initial", "entry.json"));
        host.BindContext(ctx);

        var entity = host.CreateEntity(CreateMeta("E"));

        Assert.Throws<InvalidOperationException>(() => entity.OwningSession);
    }

    [Fact]
    public void SndEntityFactory_Spawn_CreatesEntityAndFiresAfterSpawnHooks()
    {
        var events = new List<string>();
        var host = CreateHost(w =>
        {
            w.RegisterStrategy(() => new TrackingStrategy(events));
        });

        SndEntityFactory.Spawn(host, CreateMetaWithStrategy("E", _trackingIdx));

        Assert.NotNull(host.FindByName("E"));
        Assert.Contains("AfterSpawn:E", events);
    }

    [Fact]
    public void SndEntityFactory_SpawnMany_CreatesMultipleEntitiesAndFiresHooks()
    {
        var events = new List<string>();
        var host = CreateHost(w =>
        {
            w.RegisterStrategy(() => new TrackingStrategy(events));
        });

        SndEntityFactory.SpawnMany(host,
            CreateMetaWithStrategy("A", _trackingIdx),
            CreateMetaWithStrategy("B", _trackingIdx),
            CreateMetaWithStrategy("C", _trackingIdx));

        Assert.Equal(3, host.GetEntities().Count);
        Assert.Contains("AfterSpawn:A", events);
        Assert.Contains("AfterSpawn:B", events);
        Assert.Contains("AfterSpawn:C", events);
    }

    private static FullMemorySndSceneHost CreateHost(Action<SndWorld> configureWorld)
    {
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        configureWorld(world);
        host.BindWorld(world);
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var ctx = new SndContext(new SndContextParameters(runtime,
            TestFactory.CreateIoGateway(new TestMemoryFileSystem()),
            TestFactory.CreateFileMetaAccess(new TestMemoryFileSystem()),
            TestFactory.CreatePathResolver(new TestMemoryFileSystem()),
            "root", "initial", "entry.json"));
        host.BindContext(ctx);
        return host;
    }

    private static SndMetaData CreateMeta(string name) => new()
    {
        Name = name,
        NodeMetaData = new NodeMetaData(),
        StrategyMetaData = new StrategyMetaData
        {
            LifecycleIndices = [],
            ActiveIndices = [],
            ObserverIndices = []
        },
        DataMetaData = new DataMetaData()
    };

    private static SndMetaData CreateMetaWithStrategy(string name, string idx) => new()
    {
        Name = name,
        NodeMetaData = new NodeMetaData(),
        StrategyMetaData = new StrategyMetaData
        {
            LifecycleIndices = [idx],
            ActiveIndices = [],
            ObserverIndices = []
        },
        DataMetaData = new DataMetaData()
    };

    private const string _trackingIdx = "owntest.tracking";

    [StrategyIndex(_trackingIdx)]
    private sealed class TrackingStrategy(List<string> events) : LifecycleStrategyBase
    {
        private readonly List<string>? _events = events;

        public override void AfterSpawn(ISndEntity entity, ISndContext ctx) => _events?.Add($"AfterSpawn:{entity.Name}");
    }

    private sealed class StubSessionRun : ISessionRun, IDisposable
    {
        public IBlackboard SessionBlackboard => throw new NotSupportedException();
        public string LevelId => "test";
        public bool IsFrontSession => true;
        public ISessionManager SessionManager => throw new NotSupportedException();
        public IStateMachineContainer GetSessionStateMachines() => throw new NotSupportedException();
        public ISndEntity? FindByName(string name) => null;
        public IReadOnlyCollection<ISndEntity> GetEntities() => [];
        public ISndEntity Spawn(SndMetaData meta) => throw new NotSupportedException();
        public void SpawnMany(params SndMetaData[] metaList) => throw new NotSupportedException();
        public void RequestKillEntity(string entityName) => throw new NotSupportedException();
        public void Dispose() { }
    }
}
