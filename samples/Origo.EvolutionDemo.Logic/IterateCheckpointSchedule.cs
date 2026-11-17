using System;
using System.Collections.Generic;

namespace Origo.EvolutionDemo.Logic;

/// <summary>
///     Absolute simulation-time marks for timed-iteration checkpoint saves.
///     Marks are multiples of the configured interval measured from simulation
///     time zero, so the same pond produces stable, analysis-friendly snapshot
///     times regardless of when iteration started.
/// </summary>
public static class IterateCheckpointSchedule
{
    /// <summary>Smallest accepted positive checkpoint spacing in simulation seconds.</summary>
    public const float MinimumIntervalSeconds = 30f;

    /// <summary>
    ///     Returns the checkpoint marks strictly inside
    ///     (<paramref name="startTime" />, <paramref name="targetTime" />).
    ///     The final target time is never a checkpoint: the normal save path owns
    ///     the end-of-iteration state.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when a time or the interval is not finite.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when the interval is negative or a positive interval is below
    ///     <see cref="MinimumIntervalSeconds" />; fail fast instead of silently
    ///     writing thousands of snapshots.
    /// </exception>
    public static IReadOnlyList<float> Marks(float startTime, float targetTime, float intervalSeconds)
    {
        if (!float.IsFinite(startTime) || !float.IsFinite(targetTime) || !float.IsFinite(intervalSeconds))
            throw new ArgumentException("Iterate checkpoint times and interval must be finite.");
        if (intervalSeconds < 0f)
            throw new ArgumentOutOfRangeException(
                nameof(intervalSeconds), "Iterate checkpoint interval must be zero or positive.");
        if (intervalSeconds > 0f && intervalSeconds < MinimumIntervalSeconds)
            throw new ArgumentOutOfRangeException(
                nameof(intervalSeconds),
                $"Iterate checkpoint interval must be 0 or at least {MinimumIntervalSeconds:0} simulation seconds.");
        if (intervalSeconds == 0f || targetTime <= startTime + 0.05f)
            return [];

        var marks = new List<float>();
        var mark = MathF.Ceiling((startTime + 0.05f) / intervalSeconds) * intervalSeconds;
        while (mark < targetTime - 0.05f)
        {
            marks.Add(mark);
            mark += intervalSeconds;
        }

        return marks;
    }
}
