using System.Collections.Generic;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

// ── BlackboardSerializer ───────────────────────────────────────────

public class SndMetaDataTests
{
    [Fact]
    public void SndMetaData_DeepClone_CopiesName()
    {
        var meta = new SndMetaData { Name = "test" };
        var clone = meta.DeepClone();
        Assert.Equal("test", clone.Name);
    }

    [Fact]
    public void SndMetaData_DeepClone_CopiesNodeMetaData()
    {
        var meta = new SndMetaData
        {
            Name = "entity",
            NodeMetaData = new NodeMetaData { Pairs = new Dictionary<string, string> { ["res"] = "sprite.png" } }
        };
        var clone = meta.DeepClone();
        Assert.NotSame(meta.NodeMetaData, clone.NodeMetaData);
        Assert.Equal("sprite.png", clone.NodeMetaData!.Pairs["res"]);
    }

    [Fact]
    public void SndMetaData_DeepClone_CopiesStrategyMetaData()
    {
        var meta = new SndMetaData
        {
            Name = "entity",
            StrategyMetaData = new StrategyMetaData { EntityIndices = new List<string> { "strat1", "strat2" } }
        };
        var clone = meta.DeepClone();
        Assert.NotSame(meta.StrategyMetaData!.EntityIndices, clone.StrategyMetaData!.EntityIndices);
        Assert.Equal(2, clone.StrategyMetaData.EntityIndices.Count);
    }

    [Fact]
    public void SndMetaData_DeepClone_NullNodeMetaData_RemainsNull()
    {
        var meta = new SndMetaData { Name = "e", NodeMetaData = null };
        var clone = meta.DeepClone();
        Assert.Null(clone.NodeMetaData);
    }

    [Fact]
    public void SndMetaData_DefaultValues()
    {
        var meta = new SndMetaData();
        Assert.Equal(string.Empty, meta.Name);
        Assert.Null(meta.NodeMetaData);
        Assert.Null(meta.StrategyMetaData);
        Assert.NotNull(meta.DataMetaData);
    }

    [Fact]
    public void SndMetaData_DeepClone_CopiesDataMetaData()
    {
        var meta = new SndMetaData
        {
            Name = "entity",
            DataMetaData = new DataMetaData
            {
                Pairs = new Dictionary<string, TypedData>
                {
                    ["hp"] = new(typeof(int), 100),
                    ["name"] = new(typeof(string), "hero")
                }
            }
        };
        var clone = meta.DeepClone();
        Assert.NotSame(meta.DataMetaData, clone.DataMetaData);
        Assert.NotNull(clone.DataMetaData);
        Assert.Equal(100, clone.DataMetaData!.Pairs["hp"].Data);
        Assert.Equal("hero", clone.DataMetaData.Pairs["name"].Data);
    }

    [Fact]
    public void SndMetaData_DeepClone_ModifyCloneDoesNotAffectOriginal()
    {
        var meta = new SndMetaData
        {
            Name = "original",
            StrategyMetaData = new StrategyMetaData { EntityIndices = new List<string> { "strategy_a" } }
        };
        var clone = meta.DeepClone();
        clone.Name = "modified";
        clone.StrategyMetaData!.EntityIndices.Add("strategy_b");

        Assert.Equal("original", meta.Name);
        Assert.Single(meta.StrategyMetaData.EntityIndices);
        Assert.Equal("strategy_a", meta.StrategyMetaData.EntityIndices[0]);
    }

    [Fact]
    public void SndMetaData_WithActiveStrategyIndices_DeepClones()
    {
        var meta = new SndMetaData
        {
            Name = "e",
            StrategyMetaData = new StrategyMetaData
            {
                EntityIndices = new List<string> { "idle" },
                ActiveIndices = new List<string> { "invoke_handler" }
            }
        };
        var clone = meta.DeepClone();
        Assert.NotSame(meta.StrategyMetaData!.ActiveIndices, clone.StrategyMetaData!.ActiveIndices);
        Assert.Single(clone.StrategyMetaData.ActiveIndices!);
        Assert.Equal("invoke_handler", clone.StrategyMetaData.ActiveIndices![0]);
    }

    [Fact]
    public void SndMetaData_DeepClone_EmptyNodePairs_CopiesCorrectly()
    {
        var meta = new SndMetaData
        {
            Name = "e",
            NodeMetaData = new NodeMetaData { Pairs = new Dictionary<string, string>() }
        };
        var clone = meta.DeepClone();
        Assert.NotNull(clone.NodeMetaData);
        Assert.Empty(clone.NodeMetaData!.Pairs);
    }

    [Fact]
    public void SndMetaData_DeepClone_EmptyDataPairs_CopiesCorrectly()
    {
        var meta = new SndMetaData
        {
            Name = "e",
            DataMetaData = new DataMetaData { Pairs = new Dictionary<string, TypedData>() }
        };
        var clone = meta.DeepClone();
        Assert.NotNull(clone.DataMetaData);
        Assert.Empty(clone.DataMetaData!.Pairs);
    }
}

// ── OrigoRuntime integration ───────────────────────────────────────────
