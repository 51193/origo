using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Node;
using Origo.Core.Logging;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;
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

    [Fact]
    public void Release_WhenOneNodeFreeThrows_StillReleasesOthersAndRethrows()
    {
        var factory = new ThrowingOnFreeNodeFactory();
        var manager = new SndNodeManager(factory, new TestLogger());
        manager.SetSceneAliasResolver(id => id);
        manager.Recover(new NodeMetaData
        {
            Pairs = new Dictionary<string, string>
            {
                ["a"] = "res_a",
                ["b"] = "res_b",
                ["c"] = "res_c"
            }
        });

        var ex = Assert.Throws<InvalidOperationException>(() => manager.Release());
        Assert.Contains("free failure", ex.Message, StringComparison.Ordinal);

        Assert.True(factory.FreedCount == 3, "Every node must still be released (the failing one included).");
        Assert.Empty(manager.GetNodeNames());
        Assert.Empty(manager.SerializeMetaData().Pairs);
    }

    private sealed class ThrowingOnFreeNodeFactory : INodeFactory
    {
        public int FreedCount { get; private set; }

        public INodeHandle Create(string logicalName, string resourceId) =>
            new ThrowingOnFreeHandle(logicalName, () => FreedCount++);
    }

    private sealed class ThrowingOnFreeHandle(string name, Action onFreed) : INodeHandle
    {
        public string Name { get; } = name;

        public void Free()
        {
            onFreed();
            if (string.Equals(Name, "b", StringComparison.Ordinal))
                throw new InvalidOperationException("free failure");
        }

        public void SetVisible(bool visible)
        {
        }
    }
}
