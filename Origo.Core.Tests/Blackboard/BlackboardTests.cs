using System;
using Xunit;

namespace Origo.Core.Tests;

// ── BlackboardSerializer ───────────────────────────────────────────

public class BlackboardTests
{
    [Fact]
    public void Blackboard_Set_And_TryGet_Int()
    {
        var bb = new Blackboard.Blackboard();
        bb.Set("score", 100);
        var (found, value) = bb.TryGet<int>("score");
        Assert.True(found);
        Assert.Equal(100, value);
    }

    [Fact]
    public void Blackboard_Set_And_TryGet_String()
    {
        var bb = new Blackboard.Blackboard();
        bb.Set("name", "player");
        var (found, value) = bb.TryGet<string>("name");
        Assert.True(found);
        Assert.Equal("player", value);
    }

    [Fact]
    public void Blackboard_TryGet_MissingKey_ReturnsFalse()
    {
        var bb = new Blackboard.Blackboard();
        var (found, _) = bb.TryGet<int>("missing");
        Assert.False(found);
    }

    [Fact]
    public void Blackboard_TryGet_WrongType_ReturnsFalse()
    {
        var bb = new Blackboard.Blackboard();
        bb.Set("key", 42);
        var (found, _) = bb.TryGet<string>("key");
        Assert.False(found);
    }

    [Fact]
    public void Blackboard_Clear_RemovesAll()
    {
        var bb = new Blackboard.Blackboard();
        bb.Set("a", 1);
        bb.Set("b", 2);
        bb.Clear();
        Assert.Empty(bb.GetKeys());
    }

    [Fact]
    public void Blackboard_GetKeys_ReturnsAllKeys()
    {
        var bb = new Blackboard.Blackboard();
        bb.Set("a", 1);
        bb.Set("b", "x");
        var keys = bb.GetKeys();
        Assert.Equal(2, keys.Count);
        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
    }

    [Fact]
    public void Blackboard_SerializeAll_And_DeserializeAll_RoundTrip()
    {
        var bb = new Blackboard.Blackboard();
        bb.Set("key1", 42);
        bb.Set("key2", "val");

        var data = bb.SerializeAll();
        var bb2 = new Blackboard.Blackboard();
        bb2.DeserializeAll(data);

        Assert.Equal(42, bb2.TryGet<int>("key1").value);
        Assert.Equal("val", bb2.TryGet<string>("key2").value);
    }

    [Fact]
    public void Blackboard_Set_OverwriteExisting()
    {
        var bb = new Blackboard.Blackboard();
        bb.Set("key", 1);
        bb.Set("key", 2);
        Assert.Equal(2, bb.TryGet<int>("key").value);
    }

    [Fact]
    public void Blackboard_Set_ThrowsOnNullKey()
    {
        var bb = new Blackboard.Blackboard();
        Assert.Throws<ArgumentException>(() => bb.Set(null!, 1));
    }

    [Fact]
    public void Blackboard_TryGet_ThrowsOnNullKey()
    {
        var bb = new Blackboard.Blackboard();
        Assert.Throws<ArgumentException>(() => bb.TryGet<int>(null!));
    }

    [Fact]
    public void Blackboard_Set_ThrowsOnWhitespaceKey()
    {
        var bb = new Blackboard.Blackboard();
        Assert.Throws<ArgumentException>(() => bb.Set("  ", 1));
    }
}

// ── DataObserverManager ────────────────────────────────────────────────
