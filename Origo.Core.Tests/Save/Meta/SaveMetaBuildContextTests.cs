using System;
using System.Collections.Generic;
using Origo.Core.Save.Meta;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

// ── SavePathLayout ─────────────────────────────────────────────────────

public class SaveMetaBuildContextTests
{
    [Fact]
    public void SaveMetaBuildContext_ExposesReadOnlyBlackboardViews()
    {
        var progress = new Blackboard.Blackboard();
        var session = new Blackboard.Blackboard();
        progress.SetValue("progress_key", 1);
        session.SetValue("session_key", "value");
        var host = new TestSndSceneHost();
        var ctx = new SaveMetaBuildContext("s1", "lvl1", progress, session, host);

        Assert.Equal("s1", ctx.SaveId);
        Assert.Equal("lvl1", ctx.CurrentLevelId);
        Assert.Same(host, ctx.SceneAccess);

        Assert.True(ctx.Progress.TryGet<int>("progress_key").found);
        Assert.True(ctx.Session.TryGet<string>("session_key").found);
        Assert.Throws<InvalidOperationException>(() => ctx.Progress.SetValue("x", 1));
        Assert.Throws<InvalidOperationException>(() => ctx.Progress.Clear());
        Assert.Throws<InvalidOperationException>(() => ctx.Session.SetValue("x", 1));
        Assert.Throws<InvalidOperationException>(() => ctx.Session.DeserializeAll(new Dictionary<string, TypedData>()));
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
