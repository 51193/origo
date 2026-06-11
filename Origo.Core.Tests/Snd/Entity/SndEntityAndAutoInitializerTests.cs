using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Runtime;
using Origo.Core.DataSource;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class SndEntityAndAutoInitializerTests
{
    private const string LifecycleStrategyIndex = "test.lifecycle";

    [Fact]
    public void SndEntity_SpawnDataNodeStrategyLifecycle_WorksAsExpected()
    {
        var logger = new TestLogger();
        var context = CreateContext(logger);
        var nodeFactory = new TestNodeFactory();
        var events = new List<string>();
        LifecycleStrategy.Bind(events);
        context.Runtime.SndWorld.RegisterStrategy(() => new LifecycleStrategy());

        var entity = context.Runtime.SndWorld.CreateEntity(nodeFactory, context, logger);
        var meta = new SndMetaData
        {
            Name = "Player",
            NodeMetaData = new NodeMetaData
                { Pairs = new Dictionary<string, string> { ["root"] = "res://player.tscn" } },
            StrategyMetaData = new StrategyMetaData { EntityIndices = new List<string> { LifecycleStrategyIndex } },
            DataMetaData = new DataMetaData
            {
                Pairs = new Dictionary<string, TypedData> { ["hp"] = new(typeof(int), 10) }
            }
        };

        var callbackCount = 0;
        entity.Subscribe("hp", (_, __, oldValue, newValue) =>
        {
            callbackCount++;
            Assert.Equal(10, oldValue.AsInt32());
            Assert.Equal(20, newValue.AsInt32());
        });

        entity.SpawnSingle(meta);
        entity.SetData("hp", 20);
        entity.SetData("hp", 20);
        entity.AddStrategy(LifecycleStrategyIndex);
        entity.RemoveStrategy(LifecycleStrategyIndex);
        _ = entity.SaveSingle();
        entity.QuitSingle();

        Assert.Equal(1, callbackCount);
        Assert.Contains("AfterSpawn", events);
        Assert.Contains("AfterAdd", events);
        Assert.Contains("BeforeRemove", events);
        Assert.Contains("BeforeSave", events);
        Assert.Contains("BeforeQuit", events);
        Assert.Single(nodeFactory.CreatedHandles);
        Assert.Equal(1, nodeFactory.CreatedHandles[0].FreeCount);
    }

    [Fact]
    public void SndEntity_GetNodeNamesAndGetNode_ReturnExpectedHandles()
    {
        var logger = new TestLogger();
        var context = CreateContext(logger);
        var nodeFactory = new TestNodeFactory();
        var entity = context.Runtime.SndWorld.CreateEntity(nodeFactory, context, logger);

        entity.SpawnSingle(new SndMetaData
        {
            Name = "E",
            NodeMetaData = new NodeMetaData { Pairs = new Dictionary<string, string> { ["root"] = "res://e.tscn" } },
            StrategyMetaData = new StrategyMetaData { EntityIndices = new List<string>() },
            DataMetaData = new DataMetaData()
        });

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
        LifecycleStrategy.Bind(new List<string>());
        context.Runtime.SndWorld.RegisterStrategy(() => new LifecycleStrategy());

        var entity = context.Runtime.SndWorld.CreateEntity(nodeFactory, context, logger);
        entity.SpawnSingle(new SndMetaData
            { Name = "E", NodeMetaData = new NodeMetaData(), StrategyMetaData = new StrategyMetaData() });

        entity.AddStrategy(LifecycleStrategyIndex);
        Assert.Contains(LifecycleStrategyIndex, entity.SaveSingle().StrategyMetaData!.EntityIndices);

        entity.RemoveStrategy(LifecycleStrategyIndex);
        Assert.DoesNotContain(LifecycleStrategyIndex, entity.SaveSingle().StrategyMetaData!.EntityIndices);

        // Removing a missing strategy should not throw.
        entity.RemoveStrategy(LifecycleStrategyIndex);
    }

    [Fact]
    public void SndEntity_GetData_MissingKey_ThrowsInvalidOperation()
    {
        var logger = new TestLogger();
        var context = CreateContext(logger);
        var nodeFactory = new TestNodeFactory();
        var entity = context.Runtime.SndWorld.CreateEntity(nodeFactory, context, logger);
        entity.SpawnSingle(new SndMetaData
            { Name = "E", NodeMetaData = new NodeMetaData(), StrategyMetaData = new StrategyMetaData() });

        Assert.Throws<InvalidOperationException>(() => entity.GetData<int>("missing"));
    }

    [Fact]
    public void OrigoAutoInitializer_LoadAndSpawnFromFile_LoadsInlineMetaArray()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        fs.SeedFile("config/entry.json",
            """
            [
              {
                "name": "BootEntity",
                "node": { "pairs": { "root": "res://boot.tscn" } },
                "strategy": { "entity_indices": [] },
                "data": { "pairs": { "ready": { "type": "Boolean", "data": true } } }
              }
            ]
            """);

        var loaded = OrigoAutoInitializer.LoadAndSpawnFromFile("config/entry.json", runtime.Snd, io, logger);

        Assert.Equal(1, loaded);
        Assert.Single(runtime.Snd.BuildMetaList());
        Assert.Equal("BootEntity", runtime.Snd.BuildMetaList()[0].Name);
    }

    [Fact]
    public void OrigoAutoInitializer_LoadAndSpawnFromFile_EmptyPath_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        Assert.Throws<ArgumentException>(() =>
            OrigoAutoInitializer.LoadAndSpawnFromFile("  ", runtime.Snd, io, logger));
        Assert.True(logger.Errors.Exists(e => e.Contains("Invalid config path", StringComparison.Ordinal)));
    }

    [Fact]
    public void OrigoAutoInitializer_LoadAndSpawnFromFile_MissingFile_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        Assert.Throws<InvalidOperationException>(() =>
            OrigoAutoInitializer.LoadAndSpawnFromFile("missing.json", runtime.Snd, io, logger));
    }

    [Fact]
    public void OrigoAutoInitializer_LoadAndSpawnFromFile_EmptyFile_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        fs.SeedFile("empty.json", "   ");
        Assert.ThrowsAny<Exception>(() =>
            OrigoAutoInitializer.LoadAndSpawnFromFile("empty.json", runtime.Snd, io, logger));
    }

    [Fact]
    public void OrigoAutoInitializer_LoadAndSpawnFromFile_NotArrayRoot_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        fs.SeedFile("obj.json", """{"not":"array"}""");
        Assert.Throws<InvalidOperationException>(() =>
            OrigoAutoInitializer.LoadAndSpawnFromFile("obj.json", runtime.Snd, io, logger));
    }

    private static SndContext CreateContext(TestLogger logger)
    {
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        return new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "user://saveRoot", "res://initial",
            "res://entry/entry.json"));
    }

    [StrategyIndex(LifecycleStrategyIndex)]
    private sealed class LifecycleStrategy : EntityStrategyBase
    {
        private static List<string>? EventSink { get; set; }

        public static void Bind(List<string> events) => EventSink = events;

        public override void AfterSpawn(ISndEntity entity, ISndContext ctx) => EventSink?.Add("AfterSpawn");

        public override void AfterAdd(ISndEntity entity, ISndContext ctx) => EventSink?.Add("AfterAdd");

        public override void BeforeRemove(ISndEntity entity, ISndContext ctx) => EventSink?.Add("BeforeRemove");

        public override void BeforeSave(ISndEntity entity, ISndContext ctx) => EventSink?.Add("BeforeSave");

        public override void BeforeQuit(ISndEntity entity, ISndContext ctx) => EventSink?.Add("BeforeQuit");
    }
}

[StrategyIndex(IndexConst)]
public sealed class AutoInitStrategyA : EntityStrategyBase
{
    public const string IndexConst = "auto.init.a";
}

[StrategyIndex(IndexConst)]
public sealed class AutoInitStrategyB : EntityStrategyBase
{
    public const string IndexConst = "auto.init.b";
}

[StrategyIndex(IndexConst)]
public abstract class StatefulAutoInitStrategy : EntityStrategyBase
{
    public const string IndexConst = "auto.init.stateful";
    private int _counter;

    public override void Process(ISndEntity entity, double delta, ISndContext ctx) => _counter++;
}
