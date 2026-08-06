using System;
using Origo.Core.Logging;
using Origo.Core.Snd.Entity;
using Origo.TestSupport;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Guard-contract tests for <see cref="SndNodeManager" />. The resolver
///     is required (the <c>SndEntity</c> constructor validates it before
///     calling <see cref="SndNodeManager.SetSceneAliasResolver" />), so
///     passing null here is a contract violation that must fail fast.
/// </summary>
public class SndNodeManagerTests
{
    [Fact]
    public void SetSceneAliasResolver_Null_ThrowsArgumentNullException()
    {
        var manager = new SndNodeManager(new TestNodeFactory(), new TestLogger());

        Assert.Throws<ArgumentNullException>(() => manager.SetSceneAliasResolver(null!));
    }
}
