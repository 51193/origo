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
        var contributor = new DelegateSaveMetaContributor(ctx =>
        {
            invoked = true;
            return new Dictionary<string, string> { ["custom_key"] = "custom_value" };
        });

        var bb = new Blackboard.Blackboard();
        var host = new TestSndSceneHost();
        var context = new SaveMetaBuildContext("save1", "level1", bb, bb, host);

        var result = contributor.Contribute(context);
        Assert.True(invoked);
        Assert.Equal("custom_value", result["custom_key"]);
    }

    [Fact]
    public void DelegateSaveMetaContributor_Constructor_ThrowsOnNull() =>
        Assert.Throws<ArgumentNullException>(() => new DelegateSaveMetaContributor(null!));
}

// ── SaveContext ─────────────────────────────────────────────────────────
