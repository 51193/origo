using System;
using System.Collections.Generic;
using Origo.Core.DataSource;

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
    ///     dictionary, blank key, null value, or a key/value the line-based
    ///     <c>.map</c> format cannot round-trip) fail the save instead of
    ///     silently dropping metadata. Validation runs before the payload is
    ///     built, so an invalid entry fails with no save files written.
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

                // Display metadata is persisted through the line-based .map
                // format. Reject keys/values the codec cannot round-trip here,
                // before the payload writer has written any file, so the error
                // names the contributing type instead of surfacing as a meta.map
                // encode failure with a partial current/ directory.
                if (MapTextFormat.FindKeyError(kv.Key) is { } keyError)
                    throw new InvalidOperationException(
                        $"Save meta contributor '{c.GetType().FullName}' returned display-metadata key " +
                        $"'{kv.Key}' that cannot be stored in meta.map: {keyError}.");

                if (MapTextFormat.FindValueError(kv.Value) is { } valueError)
                    throw new InvalidOperationException(
                        $"Save meta contributor '{c.GetType().FullName}' returned a display-metadata value " +
                        $"for key '{kv.Key}' that cannot be stored in meta.map: {valueError}.");

                merged[kv.Key] = kv.Value;
            }
        }

        return merged.Count == 0 ? null : merged;
    }
}
