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
using Origo.Core.Snd.Scene;
using Xunit;

namespace Origo.Core.Tests;

[Collection("StrategyStateTests")]
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
            TestFactory.CreateIoGateway(new TestFileSystem()),
            TestFactory.CreateFileMetaAccess(new TestFileSystem()),
            TestFactory.CreatePathResolver(new TestFileSystem()),
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
            TestFactory.CreateIoGateway(new TestFileSystem()),
            TestFactory.CreateFileMetaAccess(new TestFileSystem()),
            TestFactory.CreatePathResolver(new TestFileSystem()),
            "root", "initial", "entry.json"));
        host.BindContext(ctx);

        var entity = host.CreateEntity(CreateMeta("E"));

        Assert.Throws<InvalidOperationException>(() => entity.OwningSession);
    }

    private static SndMetaData CreateMeta(string name) => new()
    {
        Name = name,
        NodeMetaData = new NodeMetaData(),
        StrategyMetaData = new StrategyMetaData
        {
            LifecycleIndices = new List<string>(),
            ActiveIndices = new List<string>(),
            ObserverIndices = new List<StrategyMetaData.ObserverBinding>()
        },
        DataMetaData = new DataMetaData()
    };

    private sealed class StubSessionRun : ISessionRun
    {
        public IBlackboard SessionBlackboard => throw new NotSupportedException();
        public string LevelId => "test";
        public bool IsFrontSession => true;
        public ISessionManager SessionManager => throw new NotSupportedException();
        public IStateMachineContainer GetSessionStateMachines() => throw new NotSupportedException();
        public ISndEntity? FindByName(string name) => null;
        public IReadOnlyCollection<ISndEntity> GetEntities() => Array.Empty<ISndEntity>();
        public ISndEntity Spawn(SndMetaData meta) => throw new NotSupportedException();
        public void SpawnMany(params SndMetaData[] metaList) => throw new NotSupportedException();
        public void RequestKillEntity(string entityName) => throw new NotSupportedException();
        public void Dispose() { }
    }
}
