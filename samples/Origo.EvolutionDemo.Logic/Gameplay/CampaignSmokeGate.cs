namespace Origo.EvolutionDemo.Logic.Gameplay;

/// <summary>
///     Decides whether the demo smoke should keep waiting for the first
///     generation. A valid seed may need more than the base smoke target, but
///     it must not wait forever.
/// </summary>
public static class CampaignSmokeGate
{
    /// <summary>
    ///     Returns true while the pond is alive and either the base smoke target
    ///     has not been reached or generation 1 has not appeared before its
    ///     deadline.
    /// </summary>
    /// <param name="time">Current simulation time in seconds.</param>
    /// <param name="targetSeconds">Normal smoke target in seconds.</param>
    /// <param name="generationDeadlineSeconds">Latest time to wait for generation 1.</param>
    /// <param name="generation">Current maximum generation.</param>
    /// <param name="creatures">Current living population.</param>
    public static bool ShouldWaitForGeneration(
        float time,
        float targetSeconds,
        float generationDeadlineSeconds,
        int generation,
        int creatures) =>
        creatures > 0
        && (time < targetSeconds
            || (generation < 1 && time < generationDeadlineSeconds));
}
