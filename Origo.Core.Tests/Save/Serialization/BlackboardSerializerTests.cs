using Origo.Core.Save.Serialization;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

// ── BlackboardSerializer ───────────────────────────────────────────

public class BlackboardSerializerTests
{
    private static SndWorld CreateWorld() => TestFactory.CreateSndWorld();

    [Fact]
    public void BlackboardSerializer_RoundTrip_PreservesData()
    {
        var world = CreateWorld();
        var serializer = new BlackboardSerializer(world.ConverterRegistry);
        var bb = new Blackboard.Blackboard();
        bb.Set("intKey", 42);
        bb.Set("strKey", "hello");

        using var node = serializer.Serialize(bb);
        var bb2 = new Blackboard.Blackboard();
        serializer.DeserializeInto(bb2, node);

        Assert.Equal(42, bb2.TryGet<int>("intKey").value);
        Assert.Equal("hello", bb2.TryGet<string>("strKey").value);
    }

    [Fact]
    public void BlackboardSerializer_Serialize_EmptyBlackboard_ReturnsValidJson()
    {
        var world = CreateWorld();
        var serializer = new BlackboardSerializer(world.ConverterRegistry);
        var bb = new Blackboard.Blackboard();

        using var node = serializer.Serialize(bb);
        var json = TestFactory.JsonFromNode(node);
        Assert.NotNull(json);
        Assert.Contains("{", json);
    }

    [Fact]
    public void BlackboardSerializer_DeserializeInto_OverwritesExisting()
    {
        var world = CreateWorld();
        var serializer = new BlackboardSerializer(world.ConverterRegistry);
        var bb = new Blackboard.Blackboard();
        bb.Set("key1", "old");
        bb.Set("key2", "keep");

        var source = new Blackboard.Blackboard();
        source.Set("key1", "new");
        using var node = serializer.Serialize(source);
        serializer.DeserializeInto(bb, node);

        Assert.Equal("new", bb.TryGet<string>("key1").value);
        // DeserializeAll replaces all data
        Assert.False(bb.TryGet<string>("key2").found);
    }
}

// ── Blackboard ─────────────────────────────────────────────────────────
