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
}

// ── OrigoRuntime integration ───────────────────────────────────────────
