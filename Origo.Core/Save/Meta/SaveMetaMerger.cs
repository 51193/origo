using System;
using System.Collections.Generic;

namespace Origo.Core.Save.Meta;

/// <summary>
///     Merges the registered contributors in registration order (same-name
///     keys from later contributors override earlier ones).
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
        in SaveMetaBuildContext context)
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

        return merged.Count == 0 ? null : merged;
    }
}
