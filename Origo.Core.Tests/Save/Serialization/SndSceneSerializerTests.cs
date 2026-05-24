using System;
using Origo.Core.Save.Serialization;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

// ── BlackboardSerializer ───────────────────────────────────────────

public class SndSceneSerializerTests
{
    private static SndWorld CreateWorld() => TestFactory.CreateSndWorld();

    [Fact]
    public void SndSceneSerializer_Serialize_EmptyScene()
    {
        var world = CreateWorld();
        var serializer = new SndSceneSerializer(world);
        var host = new TestSndSceneHost();

        using var node = serializer.Serialize(host);
        var json = TestFactory.JsonFromNode(node);
        Assert.NotNull(node);
        Assert.Contains("[", json);
    }

    [Fact]
    public void SndSceneSerializer_RoundTrip_PreservesMetaList()
    {
        var world = CreateWorld();
        var serializer = new SndSceneSerializer(world);

        var host1 = new TestSndSceneHost();
        host1.Spawn(new SndMetaData { Name = "entity1" });

        using var node = serializer.Serialize(host1);

        var host2 = new TestSndSceneHost();
        serializer.DeserializeInto(host2, node, true);

        var metaList = host2.SerializeMetaList();
        Assert.Single(metaList);
        Assert.Equal("entity1", metaList[0].Name);
    }

    [Fact]
    public void SndSceneSerializer_DeserializeInto_ClearsBeforeLoad()
    {
        var world = CreateWorld();
        var serializer = new SndSceneSerializer(world);
        var host = new TestSndSceneHost();
        host.Spawn(new SndMetaData { Name = "existing" });

        using var node = TestFactory.NodeFromJson("[]");
        serializer.DeserializeInto(host, node, true);
        Assert.Equal(1, host.ClearAllCount);
    }

    [Fact]
    public void SndSceneSerializer_DeserializeInto_NoClearWhenFalse()
    {
        var world = CreateWorld();
        var serializer = new SndSceneSerializer(world);
        var host = new TestSndSceneHost();

        using var node = TestFactory.NodeFromJson("[]");
        serializer.DeserializeInto(host, node, false);
        Assert.Equal(0, host.ClearAllCount);
    }

    [Fact]
    public void SndSceneSerializer_DeserializeInto_InvalidJson_Throws()
    {
        var world = CreateWorld();
        var serializer = new SndSceneSerializer(world);
        var host = new TestSndSceneHost();

        using var node = TestFactory.NodeFromJson("{}");
        Assert.ThrowsAny<Exception>(() => serializer.DeserializeInto(host, node, true));
    }

    [Fact]
    public void SndSceneSerializer_Constructor_ThrowsOnNullWorld() =>
        Assert.Throws<ArgumentNullException>(() => new SndSceneSerializer(null!));
}

// ── TypeStringMapping additional tests ─────────────────────────────────
