using System;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Scene;
using Xunit;

namespace Origo.Core.Tests;

public class EntityStrategyExtensionsTests
{
    private const string ImplKey = "test.path_impl";
    private const string DefaultIndex = "test.path.default";
    private const string OverrideIndex = "test.path.custom";

    [Fact]
    public void EnsureReplaceableStrategy_NoConfig_UsesDefault()
    {
        var entity = new StubSndEntity("e");

        var result = entity.EnsureReplaceableStrategy(ImplKey, DefaultIndex);

        Assert.True(result);

        var (found, value) = entity.TryGetData<string>(ImplKey);
        Assert.True(found);
        Assert.Equal(DefaultIndex, value);
    }

    [Fact]
    public void EnsureReplaceableStrategy_CalledAgain_ReturnsFalse()
    {
        var entity = new StubSndEntity("e");

        entity.EnsureReplaceableStrategy(ImplKey, DefaultIndex);
        var result = entity.EnsureReplaceableStrategy(ImplKey, DefaultIndex);

        Assert.False(result);
    }

    [Fact]
    public void EnsureReplaceableStrategy_ConfiguredOverride_UsesOverride()
    {
        var entity = new StubSndEntity("e");
        entity.SetData(ImplKey, OverrideIndex);

        var result = entity.EnsureReplaceableStrategy(ImplKey, DefaultIndex);

        // Already set, so EnsureStrategy inside will see value and return false
        Assert.False(result);
    }

    [Fact]
    public void EnsureReplaceableStrategy_EmptyOverride_UsesDefault()
    {
        var entity = new StubSndEntity("e");
        entity.SetData(ImplKey, "");

        var result = entity.EnsureReplaceableStrategy(ImplKey, DefaultIndex);

        Assert.True(result);

        var (found, value) = entity.TryGetData<string>(ImplKey);
        Assert.True(found);
        Assert.Equal(DefaultIndex, value);
    }

    [Fact]
    public void EnsureReplaceableStrategy_DifferentDefault_CalledAgain_ReturnsFalse()
    {
        var entity = new StubSndEntity("e");

        entity.EnsureReplaceableStrategy(ImplKey, DefaultIndex);
        var result = entity.EnsureReplaceableStrategy(ImplKey, "test.path.other");

        // Already ensured, so returns false even with different default
        Assert.False(result);

        var (found, value) = entity.TryGetData<string>(ImplKey);
        Assert.True(found);
        Assert.Equal(DefaultIndex, value);
    }

    [Fact]
    public void EnsureReplaceableStrategy_NullEntity_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            EntityStrategyExtensions.EnsureReplaceableStrategy(null!, ImplKey, DefaultIndex));
    }

    [Fact]
    public void EnsureReplaceableStrategy_NullImplKey_Throws()
    {
        var entity = new StubSndEntity("e");
        Assert.Throws<ArgumentNullException>(() =>
            entity.EnsureReplaceableStrategy(null!, DefaultIndex));
    }

    [Fact]
    public void EnsureReplaceableStrategy_NullDefault_Throws()
    {
        var entity = new StubSndEntity("e");
        Assert.Throws<ArgumentNullException>(() =>
            entity.EnsureReplaceableStrategy(ImplKey, null!));
    }
}
