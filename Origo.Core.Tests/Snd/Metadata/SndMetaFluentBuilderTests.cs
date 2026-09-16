using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.DataSource;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Xunit;

namespace Origo.Core.Tests;

public class SndMetaFluentBuilderTests
{
    [Fact]
    public void Build_WithName_SetsName()
    {
        var meta = new SndMetaFluentBuilder("TestEntity").Build();
        Assert.Equal("TestEntity", meta.Name);
    }

    [Fact]
    public void Build_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SndMetaFluentBuilder(""));
        Assert.Throws<ArgumentNullException>(() => new SndMetaFluentBuilder(null!));
    }

    [Fact]
    public void Build_WithoutExplicitNodeOrStrategy_ProducesRecoverableMeta()
    {
        // The builder is the recommended programmatic entry point for entity
        // metadata; its output must be directly recoverable through the real
        // entity recovery path without forcing callers to know that
        // NodeMetaData / StrategyMetaData are implementation requirements.
        var host = CreateBoundHost();
        var meta = new SndMetaFluentBuilder("E").Build();

        var entity = host.CreateEntity(meta);

        Assert.NotNull(entity);
        Assert.Equal("E", entity.Name);
    }

    private static FullMemorySndSceneHost CreateBoundHost()
    {
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        host.BindWorld(world);

        var fs = new TestMemoryFileSystem();
        fs.SeedFile("res://entry/entry.json",
            "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]");
        var runtime = TestFactory.CreateRuntime(logger, host);
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(
            runtime, io, metaAccess, pathResolver,
            "root", "res://initial", "res://entry/entry.json"));
        host.BindContext(ctx);
        return host;
    }

    [Fact]
    public void SetNode_AddsNodePair()
    {
        var meta = new SndMetaFluentBuilder("E")
            .SetNode("scene", "res://test.tscn")
            .Build();

        Assert.NotNull(meta.NodeMetaData);
        Assert.Equal("res://test.tscn", meta.NodeMetaData.Pairs["scene"]);
    }

    [Fact]
    public void SetNode_WhitespaceKey_Throws()
    {
        // A blank logical node name cannot be retrieved later (GetNode
        // rejects blank names), so accepting it here would defer the
        // failure to spawn/serialization time.
        Assert.Throws<ArgumentException>(
            () => new SndMetaFluentBuilder("E").SetNode("   ", "res://test.tscn"));
    }

    [Fact]
    public void SetNode_NullValue_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new SndMetaFluentBuilder("E").SetNode("scene", null!));
    }

    [Fact]
    public void SetNode_WhitespaceValue_Throws()
    {
        // A blank resource id is rejected by scene-alias resolution during
        // entity recovery; fail here instead of deferring the error.
        Assert.Throws<ArgumentException>(
            () => new SndMetaFluentBuilder("E").SetNode("scene", "   "));
    }

    [Fact]
    public void AddLifecycleStrategy_StoresIndex()
    {
        var meta = new SndMetaFluentBuilder("E")
            .AddLifecycleStrategy("game.move")
            .AddLifecycleStrategy("game.render")
            .Build();

        Assert.NotNull(meta.StrategyMetaData);
        Assert.Equal(2, meta.StrategyMetaData.LifecycleIndices.Count);
        Assert.Contains("game.move", meta.StrategyMetaData.LifecycleIndices);
        Assert.Contains("game.render", meta.StrategyMetaData.LifecycleIndices);
    }

    [Fact]
    public void AddActiveStrategy_StoresIndex()
    {
        var meta = new SndMetaFluentBuilder("E")
            .AddActiveStrategy("food.find_nearest")
            .Build();

        Assert.NotNull(meta.StrategyMetaData);
        Assert.Single(meta.StrategyMetaData.ActiveIndices);
        Assert.Contains("food.find_nearest", meta.StrategyMetaData.ActiveIndices);
    }

    [Fact]
    public void AddObserverBinding_StoresTopologyShape()
    {
        var meta = new SndMetaFluentBuilder("Watcher")
            .AddObserverBinding("Player", "watch.hp")
            .AddObserverBinding("Player", "watch.energy")
            .AddObserverBinding("Goblin", "watch.threat")
            .Build();

        Assert.NotNull(meta.StrategyMetaData);
        Assert.Equal(2, meta.StrategyMetaData.ObserverIndices.Count);

        var playerBinding = meta.StrategyMetaData.ObserverIndices
            .Single(b => b.Target == "Player");
        Assert.Equal(["watch.hp", "watch.energy"], playerBinding.ObserverIndices);

        var goblinBinding = meta.StrategyMetaData.ObserverIndices
            .Single(b => b.Target == "Goblin");
        Assert.Equal(["watch.threat"], goblinBinding.ObserverIndices);
    }

    [Fact]
    public void AddObserverBinding_BlankTargetOrIndex_Throws()
    {
        var builder = new SndMetaFluentBuilder("Watcher");

        Assert.Throws<ArgumentException>(
            () => builder.AddObserverBinding("", "watch.hp"));
        Assert.Throws<ArgumentException>(
            () => builder.AddObserverBinding("Player", ""));
        Assert.Throws<ArgumentException>(
            () => builder.AddObserverBinding("Player"));
    }

    [Fact]
    public void AddObserverBinding_NullIndices_Throws()
    {
        var builder = new SndMetaFluentBuilder("Watcher");

        Assert.Throws<ArgumentNullException>(
            () => builder.AddObserverBinding("Player", null!));
    }

    [Fact]
    public void AddObserverBinding_DuplicateIndex_Throws()
    {
        var builder = new SndMetaFluentBuilder("Watcher")
            .AddObserverBinding("Player", "watch.hp");

        Assert.Throws<ArgumentException>(
            () => builder.AddObserverBinding("Player", "watch.hp"));
    }

    [Fact]
    public void SetInt_StoresCorrectTypedData()
    {
        var meta = new SndMetaFluentBuilder("E")
            .SetInt("score", 42)
            .Build();

        Assert.NotNull(meta.DataMetaData);
        var pair = meta.DataMetaData.Pairs["score"];
        Assert.Equal(typeof(int), pair.DataType);
        Assert.Equal(42, TypedDataObjectConverter.ToObject(pair));
    }

    [Fact]
    public void SetFloat_StoresCorrectTypedData()
    {
        var meta = new SndMetaFluentBuilder("E")
            .SetFloat("speed", 3.14f)
            .Build();

        Assert.NotNull(meta.DataMetaData);
        var pair = meta.DataMetaData.Pairs["speed"];
        Assert.Equal(typeof(float), pair.DataType);
        Assert.Equal(3.14f, TypedDataObjectConverter.ToObject(pair));
    }

    [Fact]
    public void SetString_StoresCorrectTypedData()
    {
        var meta = new SndMetaFluentBuilder("E")
            .SetString("label", "hello")
            .Build();

        Assert.NotNull(meta.DataMetaData);
        var pair = meta.DataMetaData.Pairs["label"];
        Assert.Equal(typeof(string), pair.DataType);
        Assert.Equal("hello", TypedDataObjectConverter.ToObject(pair));
    }

    [Fact]
    public void SetBool_StoresCorrectTypedData()
    {
        var meta = new SndMetaFluentBuilder("E")
            .SetBool("active", true)
            .Build();

        Assert.NotNull(meta.DataMetaData);
        var pair = meta.DataMetaData.Pairs["active"];
        Assert.Equal(typeof(bool), pair.DataType);
        Assert.Equal(true, TypedDataObjectConverter.ToObject(pair));
    }

    [Fact]
    public void SetDouble_StoresCorrectTypedData()
    {
        var meta = new SndMetaFluentBuilder("E")
            .SetDouble("precise", 0.123)
            .Build();

        Assert.NotNull(meta.DataMetaData);
        var pair = meta.DataMetaData.Pairs["precise"];
        Assert.Equal(typeof(double), pair.DataType);
        Assert.Equal(0.123, TypedDataObjectConverter.ToObject(pair));
    }

    [Fact]
    public void SetLong_StoresCorrectTypedData()
    {
        var meta = new SndMetaFluentBuilder("E")
            .SetLong("big", 9999999999L)
            .Build();

        Assert.NotNull(meta.DataMetaData);
        var pair = meta.DataMetaData.Pairs["big"];
        Assert.Equal(typeof(long), pair.DataType);
        Assert.Equal(9999999999L, TypedDataObjectConverter.ToObject(pair));
    }

    [Fact]
    public void ChainedCalls_AllStored()
    {
        var meta = new SndMetaFluentBuilder("Player")
            .SetNode("sprite", "player_sprite")
            .AddLifecycleStrategy("game.player_move")
            .AddActiveStrategy("ui.select")
            .SetInt("hp", 100)
            .SetFloat("speed", 200f)
            .SetString("name", "Hero")
            .SetBool("alive", true)
            .Build();

        Assert.Equal("Player", meta.Name);
        Assert.NotNull(meta.NodeMetaData);
        Assert.Equal("player_sprite", meta.NodeMetaData.Pairs["sprite"]);
        Assert.NotNull(meta.StrategyMetaData);
        Assert.Contains("game.player_move", meta.StrategyMetaData.LifecycleIndices);
        Assert.Contains("ui.select", meta.StrategyMetaData.ActiveIndices);
        Assert.NotNull(meta.DataMetaData);
        Assert.Equal(100, TypedDataObjectConverter.ToObject(meta.DataMetaData.Pairs["hp"]));
        Assert.Equal(200f, TypedDataObjectConverter.ToObject(meta.DataMetaData.Pairs["speed"]));
        Assert.Equal("Hero", TypedDataObjectConverter.ToObject(meta.DataMetaData.Pairs["name"]));
        Assert.Equal(true, TypedDataObjectConverter.ToObject(meta.DataMetaData.Pairs["alive"]));
    }

    [Fact]
    public void SetString_NullValue_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new SndMetaFluentBuilder("E").SetString("name", null!));
    }

    [Fact]
    public void SetBytes_NullValue_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new SndMetaFluentBuilder("E").SetBytes("raw", null!));
    }
}
