using System;
using System.Collections.Generic;
using Origo.Core.Save;
using Origo.Core.Save.Serialization;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

// ── SavePathLayout ─────────────────────────────────────────────────────

public class SaveContextTests
{
    private static SndWorld CreateWorld() => TestFactory.CreateSndWorld();

    [Fact]
    public void SaveContext_SerializeProgress_And_DeserializeProgress_RoundTrip()
    {
        var world = CreateWorld();
        var progress = new Blackboard.Blackboard();
        var session = new Blackboard.Blackboard();
        progress.Set("stage", 3);

        var ctx = new SaveContext(progress, session, world);
        using var node = ctx.SerializeProgress();

        var progress2 = new Blackboard.Blackboard();
        var ctx2 = new SaveContext(progress2, new Blackboard.Blackboard(), world);
        ctx2.DeserializeProgress(node);

        Assert.Equal(3, progress2.TryGet<int>("stage").value);
    }

    [Fact]
    public void SaveContext_SerializeSession_And_DeserializeSession_RoundTrip()
    {
        var world = CreateWorld();
        var progress = new Blackboard.Blackboard();
        var session = new Blackboard.Blackboard();
        session.Set("hp", 100);

        var ctx = new SaveContext(progress, session, world);
        using var node = ctx.SerializeSession();

        var session2 = new Blackboard.Blackboard();
        var ctx2 = new SaveContext(new Blackboard.Blackboard(), session2, world);
        ctx2.DeserializeSession(node);

        Assert.Equal(100, session2.TryGet<int>("hp").value);
    }

    [Fact]
    public void SaveContext_SerializeSndScene_ReturnsJson()
    {
        var world = CreateWorld();
        var ctx = new SaveContext(new Blackboard.Blackboard(), new Blackboard.Blackboard(), world);
        var host = new TestSndSceneHost();

        using var node = ctx.SerializeSndScene(host);
        Assert.NotNull(node);
    }

    [Fact]
    public void SaveContext_DeserializeSndScene_ClearsAndLoads()
    {
        var world = CreateWorld();
        var ctx = new SaveContext(new Blackboard.Blackboard(), new Blackboard.Blackboard(), world);
        var host = new TestSndSceneHost();
        host.Spawn(new SndMetaData { Name = "old" });

        using var node = TestFactory.NodeFromJson("[]");
        ctx.DeserializeSndScene(host, node);
        Assert.Equal(1, host.ClearAllCount);
    }

    [Fact]
    public void SaveContext_SaveGame_CreatesSaveGamePayload()
    {
        var world = CreateWorld();
        var progress = new Blackboard.Blackboard();
        var session = new Blackboard.Blackboard();
        progress.Set("gold", 500);
        progress.Set(WellKnownKeys.SessionTopology, "__foreground__=level1=false");

        var ctx = new SaveContext(progress, session, world);
        var host = new TestSndSceneHost();

        using var progressSmNode = TestFactory.NodeFromJson("{}");
        using var sessionSmNode = TestFactory.NodeFromJson("{}");
        var payload = ctx.SaveGame(host, "slot1", "level1", null, progressSmNode, sessionSmNode);

        Assert.Equal("slot1", payload.SaveId);
        Assert.Equal("level1", payload.ActiveLevelId);
        Assert.NotNull(payload.ProgressNode);
        Assert.Contains("level1", payload.Levels.Keys);
    }

    [Fact]
    public void SaveContext_SaveGame_WithCustomMeta()
    {
        var world = CreateWorld();
        var progress = new Blackboard.Blackboard();
        progress.Set(WellKnownKeys.SessionTopology, "__foreground__=level1=false");
        var ctx = new SaveContext(progress, new Blackboard.Blackboard(), world);
        var host = new TestSndSceneHost();
        var meta = new Dictionary<string, string> { ["display"] = "Save 1" };

        using var progressSmNode = TestFactory.NodeFromJson("{}");
        using var sessionSmNode = TestFactory.NodeFromJson("{}");
        var payload = ctx.SaveGame(host, "slot1", "level1", meta, progressSmNode, sessionSmNode);

        Assert.NotNull(payload.CustomMeta);
        Assert.Equal("Save 1", payload.CustomMeta!["display"]);
    }

    [Fact]
    public void SaveContext_Constructor_ThrowsOnNullArgs()
    {
        var world = CreateWorld();
        var bb = new Blackboard.Blackboard();
        Assert.Throws<ArgumentNullException>(() => new SaveContext(null!, bb, world));
        Assert.Throws<ArgumentNullException>(() => new SaveContext(bb, null!, world));
        Assert.Throws<ArgumentNullException>(() => new SaveContext(bb, bb, null!));
    }

    [Fact]
    public void SaveContext_Properties_ExposeBlackboards()
    {
        var world = CreateWorld();
        var progress = new Blackboard.Blackboard();
        var session = new Blackboard.Blackboard();
        var ctx = new SaveContext(progress, session, world);

        Assert.Same(progress, ctx.Progress);
        Assert.Same(session, ctx.Session);
        Assert.Same(world, ctx.SndWorld);
    }
}

// ── SaveMetaBuildContext ────────────────────────────────────────────────
