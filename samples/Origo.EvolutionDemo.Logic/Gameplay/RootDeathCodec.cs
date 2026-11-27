using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Origo.EvolutionDemo.Logic.Gameplay;

/// <summary>Cumulative death counts for one root lineage.</summary>
/// <param name="Starvation">Zero-energy deaths.</param>
/// <param name="Predation">Lethal spike deaths.</param>
/// <param name="Aging">Maximum-age deaths.</param>
public readonly record struct RootDeathTally(int Starvation, int Predation, int Aging)
{
    /// <summary>Total tracked deaths for the root.</summary>
    public int Total => Starvation + Predation + Aging;
}

/// <summary>
///     Compact persistence codec for per-root death totals. The format is
///     root:starvation,predation,aging joined by semicolons, sorted by root.
/// </summary>
public static class RootDeathCodec
{
    /// <summary>Serializes totals, omitting roots with zero deaths.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="roots" /> is null.</exception>
    public static string Serialize(IReadOnlyDictionary<int, RootDeathTally> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        var builder = new StringBuilder();
        foreach (var (root, tally) in roots.OrderBy(static pair => pair.Key))
        {
            if (root < 0 || tally.Starvation < 0 || tally.Predation < 0 || tally.Aging < 0)
                throw new ArgumentOutOfRangeException(nameof(roots), "Root death totals must be non-negative.");
            if (tally.Total == 0)
                continue;
            if (builder.Length > 0)
                builder.Append(';');
            builder.Append(root.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(tally.Starvation.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(tally.Predation.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(tally.Aging.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    /// <summary>Parses the compact root death totals; an empty value yields an empty map.</summary>
    /// <exception cref="FormatException">The value is malformed, duplicated, or negative.</exception>
    public static IReadOnlyDictionary<int, RootDeathTally> Parse(string? text)
    {
        var result = new Dictionary<int, RootDeathTally>();
        if (string.IsNullOrWhiteSpace(text))
            return result;
        foreach (var entry in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = entry.IndexOf(':');
            var comma1 = entry.IndexOf(',', colon + 1);
            var comma2 = entry.IndexOf(',', comma1 + 1);
            if (colon <= 0 || comma1 <= colon + 1 || comma2 <= comma1 + 1 || entry.IndexOf(',', comma2 + 1) >= 0)
                throw new FormatException($"Malformed root death entry '{entry}'.");
            if (!int.TryParse(entry[..colon], NumberStyles.Integer, CultureInfo.InvariantCulture, out var root)
                || root < 0)
                throw new FormatException($"Malformed root id in '{entry}'.");
            if (!int.TryParse(entry[(colon + 1)..comma1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var starvation)
                || starvation < 0
                || !int.TryParse(entry[(comma1 + 1)..comma2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var predation)
                || predation < 0
                || !int.TryParse(entry[(comma2 + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var aging)
                || aging < 0)
                throw new FormatException($"Malformed death totals in '{entry}'.");
            if (!result.TryAdd(root, new RootDeathTally(starvation, predation, aging)))
                throw new FormatException($"Duplicate root death entry '{root}'.");
        }

        return result;
    }
}
