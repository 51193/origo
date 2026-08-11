using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Snd.Scene;
using Origo.Core.Abstractions.StateMachine;
using System;
using System.Collections.Generic;
using System.Threading;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Runtime;
using Origo.Core.DataSource;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Xunit;
using System.Text.Json;

namespace Origo.Core.Tests;

public class SndEntityAndAutoInitializerTests
{
    private const string _lifecycleStrategyIndex = "test.lifecycle";

    [Fact]
    public void SndEntity_GetNodeNamesAndGetNode_ReturnExpectedHandles()
    {
        var logger = new TestLogger();
        var context = CreateContext(logger);
        var nodeFactory = new TestNodeFactory();
        var observerTopology = new ObserverTopology(context.Runtime.SndWorld.StrategyPool, logger);
        observerTopology.BindContext(context);
        var entity = context.Runtime.SndWorld.CreateEntity(nodeFactory, context, logger, observerTopology);

        ((IEntityLifecycle)entity).RecoverForLifecycle(new SndMetaData
        {
            Name = "E",
            NodeMetaData = new NodeMetaData { Pairs = new Dictionary<string, string> { ["root"] = "res://e.tscn" } },
            StrategyMetaData = new StrategyMetaData { LifecycleIndices = [] },
            DataMetaData = new DataMetaData()
        });
        ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        Assert.Contains("root", entity.GetNodeNames());
        var handle = entity.GetNode("root");
        Assert.NotNull(handle);
        Assert.Equal("root", handle!.Name);
    }

    [Fact]
    public void SndEntity_AddRemoveStrategy_UpdatesExportedIndices()
    {
        var logger = new TestLogger();
        var context = CreateContext(logger);
        var nodeFactory = new TestNodeFactory();
        LifecycleStrategy.Bind([]);
        context.Runtime.SndWorld.RegisterStrategy(() => new LifecycleStrategy());

        var observerTopology = new ObserverTopology(context.Runtime.SndWorld.StrategyPool, logger);
        observerTopology.BindContext(context);
        var entity = context.Runtime.SndWorld.CreateEntity(nodeFactory, context, logger, observerTopology);
        ((IEntityLifecycle)entity).RecoverForLifecycle(new SndMetaData
        { Name = "E", NodeMetaData = new NodeMetaData(), StrategyMetaData = new StrategyMetaData() });
        ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        entity.AddStrategy(_lifecycleStrategyIndex);
        Assert.Contains(_lifecycleStrategyIndex, ((IEntityLifecycle)entity).BuildMetaData().StrategyMetaData!.LifecycleIndices);

        entity.RemoveStrategy(_lifecycleStrategyIndex);
        Assert.DoesNotContain(_lifecycleStrategyIndex, ((IEntityLifecycle)entity).BuildMetaData().StrategyMetaData!.LifecycleIndices);

        // Removing a strategy that is no longer mounted throws (fail-fast).
        Assert.Throws<InvalidOperationException>(() => entity.RemoveStrategy(_lifecycleStrategyIndex));
    }

    [Fact]
    public void SndEntity_GetData_MissingKey_ThrowsInvalidOperation()
    {
        var logger = new TestLogger();
        var context = CreateContext(logger);
        var nodeFactory = new TestNodeFactory();
        var observerTopology = new ObserverTopology(context.Runtime.SndWorld.StrategyPool, logger);
        observerTopology.BindContext(context);
        var entity = context.Runtime.SndWorld.CreateEntity(nodeFactory, context, logger, observerTopology);
        ((IEntityLifecycle)entity).RecoverForLifecycle(new SndMetaData
        { Name = "E", NodeMetaData = new NodeMetaData(), StrategyMetaData = new StrategyMetaData() });
        ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        Assert.Throws<InvalidOperationException>(() => entity.GetData<int>("missing"));
    }

    [Fact]
    public void SetData_NullOrWhitespaceName_ThrowsArgumentException()
    {
        var logger = new TestLogger();
        var context = CreateContext(logger);
        var nodeFactory = new TestNodeFactory();
        var observerTopology = new ObserverTopology(context.Runtime.SndWorld.StrategyPool, logger);
        observerTopology.BindContext(context);
        var entity = context.Runtime.SndWorld.CreateEntity(nodeFactory, context, logger, observerTopology);
        ((IEntityLifecycle)entity).RecoverForLifecycle(new SndMetaData
        { Name = "E", NodeMetaData = new NodeMetaData(), StrategyMetaData = new StrategyMetaData() });
        ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        Assert.Throws<ArgumentNullException>(() => entity.SetData(null!, 42));
        Assert.Throws<ArgumentException>(() => entity.SetData("", 42));
        Assert.Throws<ArgumentException>(() => entity.SetData("  ", 42));
    }

    [Fact]
    public void OrigoAutoInitializer_LoadAndSpawnFromFile_LoadsInlineMetaArray()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var session = new StubSessionRun(host);
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        fs.SeedFile("config/entry.json",
            """
            [
              {
                "name": "BootEntity",
                "node": { "pairs": { "root": "res://boot.tscn" } },
                "strategy": { "lifecycle_indices": [] },
                "data": { "pairs": { "ready": { "type": "Boolean", "data": true } } }
              }
            ]
            """);

        var loaded = OrigoAutoInitializer.LoadAndSpawnFromFile("config/entry.json", runtime.SndWorld, session, io, logger);

        Assert.Equal(1, loaded);
        Assert.Single(host.BuildMetaList());
        Assert.Equal("BootEntity", host.BuildMetaList()[0].Name);
    }

    [Fact]
    public void OrigoAutoInitializer_LoadAndSpawnFromFile_EmptyPath_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var session = new StubSessionRun(host);
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        Assert.Throws<ArgumentException>(() =>
            OrigoAutoInitializer.LoadAndSpawnFromFile("  ", runtime.SndWorld, session, io, logger));
        Assert.NotEmpty(logger.Errors);
    }

    [Fact]
    public void OrigoAutoInitializer_LoadAndSpawnFromFile_MissingFile_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var session = new StubSessionRun(host);
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        Assert.Throws<InvalidOperationException>(() =>
            OrigoAutoInitializer.LoadAndSpawnFromFile("missing.json", runtime.SndWorld, session, io, logger));
    }

    [Fact]
    public void OrigoAutoInitializer_LoadAndSpawnFromFile_EmptyFile_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var session = new StubSessionRun(host);
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        fs.SeedFile("empty.json", "   ");
        Assert.ThrowsAny<JsonException>(() =>
            OrigoAutoInitializer.LoadAndSpawnFromFile("empty.json", runtime.SndWorld, session, io, logger));
    }

    [Fact]
    public void OrigoAutoInitializer_LoadAndSpawnFromFile_NotArrayRoot_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var session = new StubSessionRun(host);
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        fs.SeedFile("obj.json", """{"not":"array"}""");
        Assert.Throws<InvalidOperationException>(() =>
            OrigoAutoInitializer.LoadAndSpawnFromFile("obj.json", runtime.SndWorld, session, io, logger));
    }

    private static SndContext CreateContext(TestLogger logger)
    {
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        return new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "user://saveRoot", "res://initial",
            "res://entry/entry.json"));
    }

    [StrategyIndex(_lifecycleStrategyIndex)]
    private sealed class LifecycleStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _eventSink = new();
        private static List<string>? EventSink => _eventSink.Value;

        public static void Bind(List<string> events) => _eventSink.Value = events;

        public override void AfterSpawn(ISndEntity entity, ISndContext ctx) => EventSink?.Add("AfterSpawn");

        public override void AfterAdd(ISndEntity entity, ISndContext ctx) => EventSink?.Add("AfterAdd");

        public override void BeforeRemove(ISndEntity entity, ISndContext ctx) => EventSink?.Add("BeforeRemove");

        public override void BeforeSave(ISndEntity entity, ISndContext ctx) => EventSink?.Add("BeforeSave");

        public override void BeforeQuit(ISndEntity entity, ISndContext ctx) => EventSink?.Add("BeforeQuit");
    }
}

[StrategyIndex(IndexConst)]
public sealed class AutoInitStrategyA : LifecycleStrategyBase
{
    public const string IndexConst = "auto.init.a";
}

[StrategyIndex(IndexConst)]
public sealed class AutoInitStrategyB : LifecycleStrategyBase
{
    public const string IndexConst = "auto.init.b";
}

[StrategyIndex(IndexConst)]
public abstract class StatefulAutoInitStrategy : LifecycleStrategyBase
{
    public const string IndexConst = "auto.init.stateful";
    private int _counter;

    public override void Process(ISndEntity entity, double delta, ISndContext ctx) => _counter++;
}

internal sealed class StubSessionRun : ISessionRun, IDisposable
{
    private readonly ISndSceneHost _host;
    public StubSessionRun(ISndSceneHost host) { ArgumentNullException.ThrowIfNull(host); _host = host; }
    public IBlackboard SessionBlackboard => throw new NotSupportedException();
    public string LevelId => "test";
    public bool IsFrontSession => true;
    public ISessionManager SessionManager => throw new NotSupportedException();
    public IStateMachineContainer GetSessionStateMachines() => throw new NotSupportedException();
    public ISndEntity? FindByName(string name) => _host.FindByName(name);
    public IReadOnlyCollection<ISndEntity> GetEntities() => _host.GetEntities();
    public ISndEntity Spawn(SndMetaData meta) => SndEntityFactory.Spawn(_host, meta);
    public void SpawnMany(params SndMetaData[] metaList) => SndEntityFactory.SpawnMany(_host, metaList);
    public void RequestKillEntity(string entityName) => _host.RequestKillEntity(entityName);
    public void Dispose() { }
}

