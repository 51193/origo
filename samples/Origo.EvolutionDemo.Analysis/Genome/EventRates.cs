using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Origo.EvolutionDemo.Analysis.IO;

namespace Origo.EvolutionDemo.Analysis.Genome;

/// <summary>One cumulative controller counter delta over a save interval.</summary>
/// <param name="SaveId">Ending save identifier.</param>
/// <param name="Path">Ending scene path.</param>
/// <param name="Time">Ending simulation time.</param>
/// <param name="IntervalSeconds">Seconds since the previous save.</param>
/// <param name="Key">Controller counter key.</param>
/// <param name="Delta">Counter delta; on a reset, the ending value.</param>
/// <param name="RatePerSecond">Delta divided by interval seconds.</param>
/// <param name="Reset">True when the counter decreased between saves.</param>
public sealed record EventRate(
    string SaveId,
    string Path,
    double Time,
    double IntervalSeconds,
    string Key,
    double Delta,
    double RatePerSecond,
    bool Reset);

/// <summary>Derives per-interval rates from cumulative controller counters.</summary>
public static class EventRateAnalyzer
{
    /// <summary>Analyzes selected cumulative counters across consecutive saves.</summary>
    /// <exception cref="ArgumentNullException">Arguments are null.</exception>
    /// <exception cref="InvalidDataException">A save interval is non-positive.</exception>
    public static IReadOnlyList<EventRate> Analyze(
        IReadOnlyList<SaveSnapshot> saves,
        IReadOnlyList<string> keys)
    {
        ArgumentNullException.ThrowIfNull(saves);
        ArgumentNullException.ThrowIfNull(keys);
        var ordered = saves
            .OrderBy(static save => save.Time)
            .ThenBy(static save => save.Id, StringComparer.Ordinal)
            .ThenBy(static save => save.Path, StringComparer.Ordinal)
            .ToArray();
        var result = new List<EventRate>();
        for (var index = 1; index < ordered.Length; index++)
        {
            var previous = ordered[index - 1];
            var current = ordered[index];
            var interval = current.Time - previous.Time;
            if (!(interval > 0d))
                throw new InvalidDataException(
                    $"Non-positive interval between '{previous.Id}' and '{current.Id}'.");
            foreach (var key in keys)
            {
                if (!previous.ControllerNumbers.TryGetValue(key, out var before)
                    || !current.ControllerNumbers.TryGetValue(key, out var after))
                    continue;
                var reset = after < before;
                var delta = reset ? after : after - before;
                result.Add(new EventRate(
                    current.Id,
                    current.Path,
                    current.Time,
                    interval,
                    key,
                    delta,
                    delta / interval,
                    reset));
            }
        }

        return result;
    }
}
