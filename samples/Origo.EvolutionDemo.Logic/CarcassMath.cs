using System;

namespace Origo.EvolutionDemo.Logic;

/// <summary>Pure scheduling helper for delayed carcass nutrient release.</summary>
public static class CarcassMath
{
    /// <summary>
    ///     Advances one carcass timer and releases at most the available food
    ///     slots. The unused timer remainder is preserved so slow frames do not
    ///     change the total released amount.
    /// </summary>
    public static (int Pulses, float Timer, int Remaining) Advance(
        float timer,
        int remaining,
        float intervalSeconds,
        float deltaSeconds,
        int freeFoodSlots)
    {
        remaining = Math.Max(0, remaining);
        if (remaining == 0)
            return (0, 0f, 0);
        if (intervalSeconds <= 0f || deltaSeconds <= 0f)
            return (0, MathF.Max(0f, timer), remaining);

        timer += deltaSeconds;
        var pulses = 0;
        var maxPulses = Math.Max(0, freeFoodSlots);
        while (timer >= intervalSeconds && remaining > 0 && pulses < maxPulses)
        {
            timer -= intervalSeconds;
            remaining--;
            pulses++;
        }

        if (remaining == 0)
            timer = 0f;
        return (pulses, timer, remaining);
    }
}
