using System;
using System.Collections.Generic;
using Origo.EvolutionDemo.Logic;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class CacheEvictionTests
{
    [Fact]
    public void TrimOldestHalf_RemovesHalfOfTheEntries()
    {
        var cache = new Dictionary<string, int>(System.StringComparer.Ordinal);
        for (var i = 0; i < 10; i++)
            cache[$"key-{i}"] = i;

        CacheEviction.TrimOldestHalf(cache);

        Assert.Equal(5, cache.Count);
    }

    [Fact]
    public void TrimOldestHalf_KeepsTheRemainingEntriesIntact()
    {
        var expected = new Dictionary<string, int>(StringComparer.Ordinal);
        var cache = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < 8; i++)
        {
            var key = $"key-{i}";
            expected[key] = i;
            cache[key] = i;
        }

        CacheEviction.TrimOldestHalf(cache);

        foreach (var pair in cache)
            Assert.Equal(expected[pair.Key], pair.Value);
    }

    [Fact]
    public void TrimOldestHalf_EmptyCacheIsNoOp()
    {
        var cache = new Dictionary<string, int>(System.StringComparer.Ordinal);

        CacheEviction.TrimOldestHalf(cache);

        Assert.Empty(cache);
    }

    [Fact]
    public void TrimOldestHalf_SingleEntryIsEvicted()
    {
        var cache = new Dictionary<string, int>(System.StringComparer.Ordinal)
        {
            ["only"] = 1,
        };

        CacheEviction.TrimOldestHalf(cache);

        Assert.Empty(cache);
    }

    [Fact]
    public void TrimOldestHalf_RejectsNull()
    {
        Assert.Throws<System.ArgumentNullException>(
            () => CacheEviction.TrimOldestHalf<string, int>(null!));
    }
}
