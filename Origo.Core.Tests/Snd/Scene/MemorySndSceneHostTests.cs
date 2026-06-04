using System;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Xunit;

namespace Origo.Core.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// 1. SndContext save / load / continue workflows
// ─────────────────────────────────────────────────────────────────────────────

public class MemorySndSceneHostTests
{
    [Fact]
    public void Spawn_AddsEntityAndMeta()
    {
        var host = new MemorySndSceneHost();
        var meta = MakeMeta("e1");
        var entity = host.SpawnEntity(meta);

        Assert.Equal("e1", entity.Name);
        Assert.Single(host.GetEntities());
        Assert.Single(host.BuildMetaList());
    }

    [Fact]
    public void Spawn_ThrowsOnNull()
    {
        var host = new MemorySndSceneHost();
        Assert.Throws<ArgumentNullException>(() => host.SpawnEntity(null!));
    }

    [Fact]
    public void FindByName_ReturnsEntity()
    {
        var host = new MemorySndSceneHost();
        host.SpawnEntity(MakeMeta("abc"));
        Assert.NotNull(host.FindByName("abc"));
        Assert.Null(host.FindByName("nonexistent"));
    }

    [Fact]
    public void LoadFromMetaList_ReplacesExisting()
    {
        var host = new MemorySndSceneHost();
        host.SpawnEntity(MakeMeta("old"));
        Assert.Single(host.GetEntities());

        host.RecoverFromMetaList(new[] { MakeMeta("new1"), MakeMeta("new2") });
        Assert.Equal(2, host.GetEntities().Count);
        Assert.Null(host.FindByName("old"));
        Assert.NotNull(host.FindByName("new1"));
        Assert.NotNull(host.FindByName("new2"));
    }

    [Fact]
    public void LoadFromMetaList_ThrowsOnNull()
    {
        var host = new MemorySndSceneHost();
        Assert.Throws<ArgumentNullException>(() => host.RecoverFromMetaList(null!));
    }

    [Fact]
    public void ClearAll_RemovesEntitiesAndMeta()
    {
        var host = new MemorySndSceneHost();
        host.SpawnEntity(MakeMeta("x"));
        host.RemoveAllEntities();
        Assert.Empty(host.GetEntities());
        Assert.Empty(host.BuildMetaList());
    }

    [Fact]
    public void SerializeMetaList_ReturnsCorrectData()
    {
        var host = new MemorySndSceneHost();
        host.SpawnEntity(MakeMeta("a"));
        host.SpawnEntity(MakeMeta("b"));
        var list = host.BuildMetaList();
        Assert.Equal(2, list.Count);
    }

    private static SndMetaData MakeMeta(string name)
    {
        return new SndMetaData
        {
            Name = name,
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        };
    }
}
