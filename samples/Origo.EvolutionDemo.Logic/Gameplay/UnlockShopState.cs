using System;
using System.Linq;

namespace Origo.EvolutionDemo.Logic.Gameplay;

/// <summary>
///     Presentation state for the unlock shop. The fingerprint changes whenever
///     either the definitions or the progression state that controls button
///     enablement changes.
/// </summary>
public static class UnlockShopState
{
    /// <summary>Builds a deterministic fingerprint for the shop rows.</summary>
    public static string Fingerprint(GameplayContentSnapshot content, GameplayProgression progression)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(progression);
        var definitions = content.Unlocks.Values
            .OrderBy(static unlock => unlock.Id, StringComparer.Ordinal)
            .Select(static unlock =>
                $"{unlock.Id}:{unlock.Cost}:{unlock.Title}:{unlock.Category}:" +
                $"{string.Join(',', unlock.Prerequisites)}:{string.Join(',', unlock.Grants)}:" +
                $"{string.Join(',', unlock.RequiredMilestones)}");
        return string.Join('|', definitions)
            + $"|mode={progression.Mode}"
            + $"|points={progression.Points}"
            + $"|unlocked={string.Join(';', progression.UnlockedIds.OrderBy(static id => id, StringComparer.Ordinal))}"
            + $"|milestones={string.Join(';', progression.CompletedMilestones.OrderBy(static id => id, StringComparer.Ordinal))}";
    }
}
