using System;
using System.Collections.Generic;
using Origo.Core.Save.Meta;
using Xunit;

namespace Origo.Core.Tests;

// ── SavePathLayout ─────────────────────────────────────────────────────

public class DelegateSaveMetaContributorTests
{
    [Fact]
    public void DelegateSaveMetaContributor_Contribute_InvokesDelegate()
    {
        var invoked = false;
        var contributor = new DelegateSaveMetaContributor((ctx, meta) =>
        {
            invoked = true;
            meta["custom_key"] = "custom_value";
        });

        var bb = new Blackboard.Blackboard();
        var host = new TestSndSceneHost();
        var context = new SaveMetaBuildContext("save1", "level1", bb, bb, host);
        var dict = new Dictionary<string, string>();

        contributor.Contribute(context, dict);
        Assert.True(invoked);
        Assert.Equal("custom_value", dict["custom_key"]);
    }

    [Fact]
    public void DelegateSaveMetaContributor_Constructor_ThrowsOnNull() =>
        Assert.Throws<ArgumentNullException>(() => new DelegateSaveMetaContributor(null!));
}

// ── SaveContext ─────────────────────────────────────────────────────────
