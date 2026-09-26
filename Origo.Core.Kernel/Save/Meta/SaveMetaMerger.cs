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
    ///     no keys. Contributors that violate the interface contract (null
    ///     dictionary, blank key, or null value) fail the save instead of
    ///     silently dropping metadata.
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

            var contributed = c.Contribute(in context) ?? throw new InvalidOperationException(
                $"Save meta contributor '{c.GetType().FullName}' returned null; " +
                "contribute an empty dictionary instead.");

            foreach (var kv in contributed)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                    throw new InvalidOperationException(
                        $"Save meta contributor '{c.GetType().FullName}' returned a blank key.");
                if (kv.Value is null)
                    throw new InvalidOperationException(
                        $"Save meta contributor '{c.GetType().FullName}' returned a null value for key '{kv.Key}'.");

                merged[kv.Key] = kv.Value;
            }
        }

        return merged.Count == 0 ? null : merged;
    }
}
