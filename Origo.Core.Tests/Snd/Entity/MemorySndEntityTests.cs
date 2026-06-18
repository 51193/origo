using System;
using System.Collections.Generic;
using Origo.Core.Snd.Scene;
using Xunit;

namespace Origo.Core.Tests;

public class StubSndEntityTests
{
    [Fact]
    public void Constructor_ThrowsOnNullName() =>
        Assert.Throws<ArgumentNullException>(() => new StubSndEntity(null!));

    [Fact]
    public void Name_ReturnsConstructedName()
    {
        var entity = new StubSndEntity("hero");
        Assert.Equal("hero", entity.Name);
    }

    [Fact]
    public void SetData_GetData_RoundTrip()
    {
        var entity = new StubSndEntity("e");
        entity.SetData("hp", 100);
        Assert.Equal(100, entity.GetData<int>("hp"));
    }

    [Fact]
    public void GetData_ThrowsKeyNotFound_WhenMissing()
    {
        var entity = new StubSndEntity("e");
        Assert.Throws<KeyNotFoundException>(() => entity.GetData<int>("missing"));
    }

    [Fact]
    public void GetData_ThrowsInvalidCast_OnTypeMismatch()
    {
        var entity = new StubSndEntity("e");
        entity.SetData("val", "string_value");
        Assert.Throws<InvalidCastException>(() => entity.GetData<int>("val"));
    }

    [Fact]
    public void TryGetData_ReturnsTrueWhenFound()
    {
        var entity = new StubSndEntity("e");
        entity.SetData("score", 42);
        var (found, value) = entity.TryGetData<int>("score");
        Assert.True(found);
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryGetData_ReturnsFalseWhenMissing()
    {
        var entity = new StubSndEntity("e");
        var (found, _) = entity.TryGetData<int>("nope");
        Assert.False(found);
    }

    [Fact]
    public void TryGetData_ReturnsFalseForTypeMismatch()
    {
        var entity = new StubSndEntity("e");
        entity.SetData("val", "string_value");
        var (found, _) = entity.TryGetData<int>("val");
        Assert.False(found);
    }

    [Fact]
    public void GetNode_ThrowsInvalidOperation()
    {
        var entity = new StubSndEntity("e");
        Assert.Throws<InvalidOperationException>(() => entity.GetNode("node1"));
    }

    [Fact]
    public void GetNodeNames_ReturnsEmpty()
    {
        var entity = new StubSndEntity("e");
        Assert.Empty(entity.GetNodeNames());
    }

    [Fact]
    public void InitialNameData_IsSetInDictionary()
    {
        var entity = new StubSndEntity("test_name");
        Assert.Equal("test_name", entity.GetData<string>("name"));
    }

    [Fact]
    public void AddRemoveStrategy_DoesNotThrow()
    {
        var entity = new StubSndEntity("e");
        entity.SetData("hp", 10);

        var ex = Record.Exception(() =>
        {
            entity.AddStrategy("idx1");
            entity.RemoveStrategy("idx1");
        });

        Assert.Null(ex);
        Assert.Equal(10, entity.GetData<int>("hp"));
        Assert.Empty(entity.GetNodeNames());
    }
}
