using System;

namespace Origo.EvolutionDemo.Logic;

/// <summary>
///     Stable phase selection for the demo's batched AI updates. Hashing the
///     creature's stable id against the tick keeps each creature on a fixed
///     cadence even when deaths/spawns reorder the snapshot list; the previous
///     snapshot-index phase shifted whenever the population changed.
/// </summary>
public static class AiBatchPhase
{
    /// <summary>True when the creature should update AI on this tick.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="stride" /> is below one.</exception>
    public static bool ShouldUpdate(int stableId, int tickNumber, int stride)
    {
        if (stride < 1)
            throw new ArgumentOutOfRangeException(nameof(stride), "AI batch stride must be at least one.");
        if (tickNumber <= 0)
            return true;
        var phase = ((stableId % stride) + stride) % stride;
        return ((tickNumber + stride) % stride) == phase;
    }
}
