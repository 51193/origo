using System;
using Origo.Core.Save.Meta;
using Xunit;

namespace Origo.Core.Tests;

// ── SavePathLayout ─────────────────────────────────────────────────────

public class SaveMetaBuildContextTests
{
    [Fact]
    public void SaveMetaBuildContext_StoresAllProperties()
    {
        var progress = new Blackboard.Blackboard();
        var session = new Blackboard.Blackboard();
        var host = new TestSndSceneHost();
        var ctx = new SaveMetaBuildContext("s1", "lvl1", progress, session, host);

        Assert.Equal("s1", ctx.SaveId);
        Assert.Equal("lvl1", ctx.CurrentLevelId);
        Assert.Same(progress, ctx.Progress);
        Assert.Same(session, ctx.Session);
        Assert.Same(host, ctx.SceneAccess);
    }

    [Fact]
    public void SaveMetaBuildContext_ThrowsOnNullArgs()
    {
        var bb = new Blackboard.Blackboard();
        var host = new TestSndSceneHost();

        Assert.Throws<ArgumentNullException>(() => new SaveMetaBuildContext(null!, "l", bb, bb, host));
        Assert.Throws<ArgumentNullException>(() => new SaveMetaBuildContext("s", null!, bb, bb, host));
        Assert.Throws<ArgumentNullException>(() => new SaveMetaBuildContext("s", "l", null!, bb, host));
        Assert.Throws<ArgumentNullException>(() => new SaveMetaBuildContext("s", "l", bb, null!, host));
        Assert.Throws<ArgumentNullException>(() => new SaveMetaBuildContext("s", "l", bb, bb, null!));
    }
}

// ── SaveGamePayload ────────────────────────────────────────────────────
