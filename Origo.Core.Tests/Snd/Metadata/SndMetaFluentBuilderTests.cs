using System;
using System.Collections.Generic;
using Origo.Core.Snd.Metadata;
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
    public void SetNode_AddsNodePair()
    {
        var meta = new SndMetaFluentBuilder("E")
            .SetNode("scene", "res://test.tscn")
            .Build();

        Assert.NotNull(meta.NodeMetaData);
        Assert.Equal("res://test.tscn", meta.NodeMetaData.Pairs["scene"]);
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
    public void SetInt_StoresCorrectTypedData()
    {
        var meta = new SndMetaFluentBuilder("E")
            .SetInt("score", 42)
            .Build();

        Assert.NotNull(meta.DataMetaData);
        var pair = meta.DataMetaData.Pairs["score"];
        Assert.Equal(typeof(int), pair.DataType);
        Assert.Equal(42, pair.Data);
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
        Assert.Equal(3.14f, pair.Data);
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
        Assert.Equal("hello", pair.Data);
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
        Assert.Equal(true, pair.Data);
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
        Assert.Equal(0.123, pair.Data);
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
        Assert.Equal(9999999999L, pair.Data);
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
        Assert.Equal(100, meta.DataMetaData.Pairs["hp"].Data);
        Assert.Equal(200f, meta.DataMetaData.Pairs["speed"].Data);
        Assert.Equal("Hero", meta.DataMetaData.Pairs["name"].Data);
        Assert.Equal(true, meta.DataMetaData.Pairs["alive"].Data);
    }
}
