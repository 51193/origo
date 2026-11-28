using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Origo.EvolutionDemo.Logic.Gameplay;

/// <summary>Compact persistence codec for cumulative births per root lineage.</summary>
public static class RootBirthCodec
{
    /// <summary>Serializes root:births pairs sorted by root, omitting zeros.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="births" /> is null.</exception>
    public static string Serialize(IReadOnlyDictionary<int, int> births)
    {
        ArgumentNullException.ThrowIfNull(births);
        var builder = new StringBuilder();
        foreach (var (root, count) in births.OrderBy(static pair => pair.Key))
        {
            if (root < 0 || count < 0)
                throw new ArgumentOutOfRangeException(nameof(births), "Root births must be non-negative.");
            if (count == 0)
                continue;
            if (builder.Length > 0)
                builder.Append(';');
            builder.Append(root.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(count.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    /// <summary>Parses root:births pairs; empty text yields an empty map.</summary>
    /// <exception cref="FormatException">An entry is malformed, duplicated, or negative.</exception>
    public static IReadOnlyDictionary<int, int> Parse(string? text)
    {
        var result = new Dictionary<int, int>();
        if (string.IsNullOrWhiteSpace(text))
            return result;
        foreach (var entry in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = entry.IndexOf(':');
            if (colon <= 0 || entry.IndexOf(':', colon + 1) >= 0)
                throw new FormatException($"Malformed root birth entry '{entry}'.");
            if (!int.TryParse(entry[..colon], NumberStyles.Integer, CultureInfo.InvariantCulture, out var root)
                || root < 0
                || !int.TryParse(entry[(colon + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var births)
                || births < 0)
                throw new FormatException($"Malformed root birth entry '{entry}'.");
            if (!result.TryAdd(root, births))
                throw new FormatException($"Duplicate root birth entry '{root}'.");
        }

        return result;
    }
}
