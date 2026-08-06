using System;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Xunit;

namespace Origo.Core.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// 1. SndContext save / load / continue workflows
// ─────────────────────────────────────────────────────────────────────────────

public class StubSndSceneHostTests
{
    [Fact]
    public void Spawn_AddsEntityAndMeta()
    {
        var host = new StubSndSceneHost();
        var meta = MakeMeta("e1");
        var entity = host.CreateEntity(meta);

        Assert.Equal("e1", entity.Name);
        Assert.Single(host.GetEntities());
        Assert.Single(host.BuildMetaList());
    }

    [Fact]
    public void Spawn_ThrowsOnNull()
    {
        var host = new StubSndSceneHost();
        Assert.Throws<ArgumentNullException>(() => host.CreateEntity(null!));
    }

    [Fact]
    public void FindByName_ReturnsEntity()
    {
        var host = new StubSndSceneHost();
        host.CreateEntity(MakeMeta("abc"));
        Assert.NotNull(host.FindByName("abc"));
        Assert.Null(host.FindByName("nonexistent"));
    }

    [Fact]
    public void LoadFromMetaList_DoesNotClearExisting()
    {
        var host = new StubSndSceneHost();
        host.CreateEntity(MakeMeta("old"));
        Assert.Single(host.GetEntities());

        host.RecoverFromMetaList([MakeMeta("new1"), MakeMeta("new2")]);
        Assert.Equal(3, host.GetEntities().Count);
        Assert.NotNull(host.FindByName("old"));
        Assert.NotNull(host.FindByName("new1"));
        Assert.NotNull(host.FindByName("new2"));
    }

    [Fact]
    public void RemoveEntity_Missing_Throws()
    {
        var host = new StubSndSceneHost();
        host.CreateEntity(MakeMeta("x"));
        Assert.Throws<InvalidOperationException>(() => host.RemoveEntity("missing"));
    }

    [Fact]
    public void LoadFromMetaList_ThrowsOnNull()
    {
        var host = new StubSndSceneHost();
        Assert.Throws<ArgumentNullException>(() => host.RecoverFromMetaList(null!));
    }

    [Fact]
    public void ClearAll_RemovesEntitiesAndMeta()
    {
        var host = new StubSndSceneHost();
        host.CreateEntity(MakeMeta("x"));
        host.RemoveAllEntities();
        Assert.Empty(host.GetEntities());
        Assert.Empty(host.BuildMetaList());
    }

    [Fact]
    public void SerializeMetaList_ReturnsCorrectData()
    {
        var host = new StubSndSceneHost();
        host.CreateEntity(MakeMeta("a"));
        host.CreateEntity(MakeMeta("b"));
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
