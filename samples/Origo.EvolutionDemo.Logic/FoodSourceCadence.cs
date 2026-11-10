using System;

namespace Origo.EvolutionDemo.Logic;

/// <summary>
///     Deterministic per-source phase offsets for the SND food-source clocks.
///     Every source starts its scan, spawn, and walk clocks at a different point
///     of the interval, so the pond's sources never all scan or spawn on the
///     same simulation tick.
/// </summary>
public static class FoodSourceCadence
{
    /// <summary>Source clocks that are offset independently.</summary>
    public enum Channel
    {
        /// <summary>The local/global food-density scan clock.</summary>
        Scan = 0,

        /// <summary>The spawn-burst clock.</summary>
        Spawn = 1,

        /// <summary>The wander clock.</summary>
        Walk = 2,
    }

    /// <summary>
    ///     Returns the deterministic phase offset, in seconds, for one source
    ///     clock. The result is in <c>[0, intervalSeconds)</c>.
    /// </summary>
    /// <param name="seed">The pond seed stored on the controller.</param>
    /// <param name="patchIndex">The patch/source index.</param>
    /// <param name="channel">The clock channel to offset.</param>
    /// <param name="intervalSeconds">The clock interval; must be positive.</param>
    /// <returns>The phase offset in seconds.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="intervalSeconds" /> is not positive.
    /// </exception>
    public static float Phase(int seed, int patchIndex, Channel channel, float intervalSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(intervalSeconds, 0f);

        uint hash;
        unchecked
        {
            hash = ((uint)seed * 2654435761u)
                ^ ((uint)(patchIndex + 1) * 2246822519u)
                ^ ((uint)(int)channel * 3266489917u);
            hash ^= hash >> 15;
            hash *= 2246822519u;
            hash ^= hash >> 13;
        }

        var fraction = (hash & 0xFFFFFFu) / 16777216f;
        return fraction * intervalSeconds;
    }
}
