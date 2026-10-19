using System;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class EntityStrategyExtensionsTests
{
    private const string _implKey = "test.path_impl";
    private const string _defaultIndex = "test.path.default";
    private const string _overrideIndex = "test.path.custom";

    [Fact]
    public void EnsureReplaceableStrategy_NoConfig_UsesDefault()
    {
        var entity = new StubSndEntity("e");

        var result = entity.EnsureReplaceableStrategy(_implKey, _defaultIndex);

        Assert.True(result);

        var (found, value) = entity.TryGetData<string>(_implKey);
        Assert.True(found);
        Assert.Equal(_defaultIndex, value);
    }

    [Fact]
    public void EnsureReplaceableStrategy_CalledAgain_ReturnsFalse()
    {
        var entity = new StubSndEntity("e");

        entity.EnsureReplaceableStrategy(_implKey, _defaultIndex);
        var result = entity.EnsureReplaceableStrategy(_implKey, _defaultIndex);

        Assert.False(result);
    }

    [Fact]
    public void EnsureReplaceableStrategy_ConfiguredOverride_PreservesIndex()
    {
        var entity = new StubSndEntity("e");
        entity.SetData(_implKey, _overrideIndex);

        var result = entity.EnsureReplaceableStrategy(_implKey, _defaultIndex);

        // The configured index is owned by whoever set it; this call neither
        // mounts nor rewrites it.
        Assert.False(result);
        var (found, value) = entity.TryGetData<string>(_implKey);
        Assert.True(found);
        Assert.Equal(_overrideIndex, value);
    }

    [Fact]
    public void EnsureReplaceableStrategy_EmptyOverride_UsesDefault()
    {
        var entity = new StubSndEntity("e");
        entity.SetData(_implKey, "");

        var result = entity.EnsureReplaceableStrategy(_implKey, _defaultIndex);

        Assert.True(result);

        var (found, value) = entity.TryGetData<string>(_implKey);
        Assert.True(found);
        Assert.Equal(_defaultIndex, value);
    }

    [Fact]
    public void EnsureReplaceableStrategy_DifferentDefault_CalledAgain_ReturnsFalse()
    {
        var entity = new StubSndEntity("e");

        entity.EnsureReplaceableStrategy(_implKey, _defaultIndex);
        var result = entity.EnsureReplaceableStrategy(_implKey, "test.path.other");

        // Already ensured, so returns false even with different default
        Assert.False(result);

        var (found, value) = entity.TryGetData<string>(_implKey);
        Assert.True(found);
        Assert.Equal(_defaultIndex, value);
    }

    [Fact]
    public void EnsureReplaceableStrategy_NullEntity_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            EntityStrategyExtensions.EnsureReplaceableStrategy(null!, _implKey, _defaultIndex));
    }

    [Fact]
    public void EnsureReplaceableStrategy_NullImplKey_Throws()
    {
        var entity = new StubSndEntity("e");
        Assert.Throws<ArgumentNullException>(() =>
            entity.EnsureReplaceableStrategy(null!, _defaultIndex));
    }

    [Fact]
    public void EnsureReplaceableStrategy_NullDefault_Throws()
    {
        var entity = new StubSndEntity("e");
        Assert.Throws<ArgumentNullException>(() =>
            entity.EnsureReplaceableStrategy(_implKey, null!));
    }
}
