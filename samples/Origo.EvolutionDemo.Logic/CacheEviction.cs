using System;
using System.Collections.Generic;

namespace Origo.EvolutionDemo.Logic;

/// <summary>
///     Eviction policy for the demo's bounded derivation caches (decoded brains
///     and derived appearances). When a cache reaches its size limit the oldest
///     inserted half is removed instead of clearing everything, so a long-lived
///     pond does not re-derive every live genome in one frame.
/// </summary>
public static class CacheEviction
{
    /// <summary>Default size limit for the demo's derivation caches.</summary>
    public const int DefaultLimit = 8192;

    /// <summary>
    ///     Removes roughly half of the entries in <paramref name="cache" />,
    ///     starting from the oldest enumeration positions. Dictionary
    ///     enumeration follows insertion order while no entries were removed, so
    ///     the trim keeps the most recently derived entries on a steady pond.
    /// </summary>
    /// <typeparam name="TKey">Cache key type.</typeparam>
    /// <typeparam name="TValue">Cache value type.</typeparam>
    /// <param name="cache">The cache to trim; must not be null.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="cache" /> is null.
    /// </exception>
    public static void TrimOldestHalf<TKey, TValue>(IDictionary<TKey, TValue> cache)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(cache);
        if (cache.Count == 0)
            return;

        var target = Math.Max(1, cache.Count / 2);
        var removable = new List<TKey>(target);
        foreach (var key in cache.Keys)
        {
            if (removable.Count >= target)
                break;
            removable.Add(key);
        }

        foreach (var key in removable)
            cache.Remove(key);
    }
}
