using System;
using System.Collections.Generic;

namespace Origo.Core.Save.Meta;

/// <summary>
///     Merges registered contributors with the save request overrides:
///     contributors first (in registration order, same-name keys from later
///     contributors override earlier ones), then <paramref name="overrides" />.
/// </summary>
internal static class SaveMetaMerger
{
    /// <summary>
    ///     Returns the merged dictionary; returns <c>null</c> when there are
    ///     no keys, consistent with the semantics of not providing custom
    ///     meta.
    /// </summary>
    public static IReadOnlyDictionary<string, string>? Merge(
        IReadOnlyList<ISaveMetaContributor> contributors,
        in SaveMetaBuildContext context,
        IReadOnlyDictionary<string, string>? overrides)
    {
        ArgumentNullException.ThrowIfNull(contributors);

        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var c in contributors)
        {
            ArgumentNullException.ThrowIfNull(c);
            var contributed = c.Contribute(in context);
            if (contributed is null) continue;
            foreach (var kv in contributed)
            {
                if (string.IsNullOrEmpty(kv.Key) || kv.Value is null)
                    continue;
                merged[kv.Key] = kv.Value;
            }
        }

        ApplyOverrides(merged, overrides);
        return merged.Count == 0 ? null : merged;
    }

    private static void ApplyOverrides(
        Dictionary<string, string> merged,
        IReadOnlyDictionary<string, string>? overrides)
    {
        if (overrides is null)
            return;

        foreach (var kv in overrides)
        {
            if (string.IsNullOrEmpty(kv.Key))
                continue;
            if (kv.Value is null)
                continue;
            merged[kv.Key] = kv.Value;
        }
    }
}
