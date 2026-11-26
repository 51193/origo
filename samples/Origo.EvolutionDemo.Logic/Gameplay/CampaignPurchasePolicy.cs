using System;
using System.Collections.Generic;
using System.Linq;

namespace Origo.EvolutionDemo.Logic.Gameplay;

/// <summary>
///     How an automated campaign test spends Evolution Points. The strategy is a
///     test player model, not a recommendation for the real unlock shop.
/// </summary>
public enum CampaignPurchaseStrategy
{
    /// <summary>Buy every affordable unlock in the control order on every tick.</summary>
    Greedy,

    /// <summary>Save points for the next planned goal and buy only that goal once.</summary>
    Planned,

    /// <summary>Buy the cheapest currently unlockable ability at each decision.</summary>
    Cheapest,

    /// <summary>Visit the shop at long intervals and buy everything affordable in plan order.</summary>
    Idle,
}

/// <summary>Optional content controls for controlled campaign test runs.</summary>
/// <param name="AllowEnrichment">When false, skip the enrichment unlock.</param>
/// <param name="AllowPredation">When false, skip predation, battery, and armor.</param>
public sealed record CampaignPurchaseOptions(
    bool AllowEnrichment = true,
    bool AllowPredation = true);

/// <summary>One unlock the policy would buy at the current points total.</summary>
/// <param name="UnlockId">Unlock id.</param>
/// <param name="Cost">Definition cost in Evolution Points.</param>
/// <param name="PointsBefore">Points available when the decision was made.</param>
public sealed record CampaignPurchaseDecision(string UnlockId, long Cost, long PointsBefore);

/// <summary>
///     Deterministic purchase policies for campaign test runs. The policy reads
///     only the runtime progression/content and never mutates it; the test host
///     applies the decision through <see cref="GameplayRuntime.TryUnlock" /> so
///     cost, prerequisites, and milestone gates stay authoritative.
/// </summary>
public static class CampaignPurchasePolicy
{
    /// <summary>
    ///     Capacity/survival-first plan used by the <see cref="CampaignPurchaseStrategy.Planned" />
    ///     and <see cref="CampaignPurchaseStrategy.Idle" /> test players. Optional
    ///     senses and enrichment come last so the plan is not dominated by cheap
    ///     side grades before the pond expands.
    /// </summary>
    public static readonly IReadOnlyList<string> PlannedOrder =
    [
        CampaignUnlockIds.ToughFood,
        CampaignUnlockIds.HardDigest,
        CampaignUnlockIds.Pond1,
        CampaignUnlockIds.Sprint,
        CampaignUnlockIds.ToxicFood,
        CampaignUnlockIds.ToxinResistance,
        CampaignUnlockIds.Pond2,
        CampaignUnlockIds.CorpseCycle,
        CampaignUnlockIds.CarrionSense,
        CampaignUnlockIds.Pond3,
        CampaignUnlockIds.Pond4,
        CampaignUnlockIds.FoodTypeVision,
        CampaignUnlockIds.Predation,
        CampaignUnlockIds.Battery,
        CampaignUnlockIds.Armor,
        CampaignUnlockIds.Pond5,
        CampaignUnlockIds.Pond6,
        CampaignUnlockIds.CreatureSense,
        CampaignUnlockIds.Rhythm,
        CampaignUnlockIds.Enrichment,
    ];

    /// <summary>Control order for the greedy buy-everything baseline.</summary>
    public static readonly IReadOnlyList<string> ControlOrder =
    [
        CampaignUnlockIds.ToughFood,
        CampaignUnlockIds.HardDigest,
        CampaignUnlockIds.Pond1,
        CampaignUnlockIds.Sprint,
        CampaignUnlockIds.ToxicFood,
        CampaignUnlockIds.ToxinResistance,
        CampaignUnlockIds.CreatureSense,
        CampaignUnlockIds.Rhythm,
        CampaignUnlockIds.CorpseCycle,
        CampaignUnlockIds.CarrionSense,
        CampaignUnlockIds.Pond2,
        CampaignUnlockIds.Predation,
        CampaignUnlockIds.Battery,
        CampaignUnlockIds.Armor,
        CampaignUnlockIds.Pond3,
        CampaignUnlockIds.FoodTypeVision,
        CampaignUnlockIds.Enrichment,
        CampaignUnlockIds.Pond4,
        CampaignUnlockIds.Pond5,
        CampaignUnlockIds.Pond6,
    ];

    /// <summary>
    ///     Returns the next purchase for one decision, or null when the strategy
    ///     should wait. A greedy or idle host calls this repeatedly; a planned or
    ///     cheapest host calls it once per decision interval.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="runtime" /> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The strategy value is not defined.</exception>
    public static CampaignPurchaseDecision? Decide(
        GameplayRuntime runtime,
        CampaignPurchaseStrategy strategy,
        CampaignPurchaseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        options ??= new CampaignPurchaseOptions();
        return strategy switch
        {
            CampaignPurchaseStrategy.Greedy => FirstUnlockable(runtime, ControlOrder, options),
            CampaignPurchaseStrategy.Planned => NextPlanned(runtime, options),
            CampaignPurchaseStrategy.Cheapest => CheapestUnlockable(runtime, options),
            CampaignPurchaseStrategy.Idle => FirstUnlockable(runtime, PlannedOrder, options),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unknown purchase strategy."),
        };
    }

    private static CampaignPurchaseDecision? NextPlanned(
        GameplayRuntime runtime, CampaignPurchaseOptions options)
    {
        foreach (var unlockId in PlannedOrder)
        {
            if (runtime.Unlocks.IsUnlocked(unlockId))
                continue;
            if (!IsAllowed(unlockId, options))
                continue;
            // The planned player saves for this target. If it is milestone-locked,
            // the test waits instead of spending the points on a side grade.
            return runtime.Unlocks.CanUnlock(unlockId)
                ? Decision(runtime, unlockId)
                : null;
        }

        return null;
    }

    private static CampaignPurchaseDecision? CheapestUnlockable(
        GameplayRuntime runtime, CampaignPurchaseOptions options)
    {
        var coreUnlocks = PlannedOrder.ToHashSet(StringComparer.Ordinal);
        var candidates = runtime.Content.Unlocks.Values
            .Where(unlock => coreUnlocks.Contains(unlock.Id)
                && IsAllowed(unlock.Id, options)
                && !runtime.Unlocks.IsUnlocked(unlock.Id)
                && runtime.Unlocks.CanUnlock(unlock.Id))
            .OrderBy(static unlock => unlock.Cost)
            .ThenBy(unlock => PlanIndex(unlock.Id))
            .ThenBy(static unlock => unlock.Id, StringComparer.Ordinal)
            .ToArray();
        return candidates.Length == 0 ? null : Decision(runtime, candidates[0].Id);
    }

    private static CampaignPurchaseDecision? FirstUnlockable(
        GameplayRuntime runtime, IReadOnlyList<string> order, CampaignPurchaseOptions options)
    {
        foreach (var unlockId in order)
        {
            if (runtime.Unlocks.IsUnlocked(unlockId))
                continue;
            if (!IsAllowed(unlockId, options))
                continue;
            if (runtime.Unlocks.CanUnlock(unlockId))
                return Decision(runtime, unlockId);
        }

        return null;
    }

    private static bool IsAllowed(string unlockId, CampaignPurchaseOptions options) =>
        (options.AllowEnrichment || !string.Equals(
            unlockId, CampaignUnlockIds.Enrichment, StringComparison.Ordinal))
        && (options.AllowPredation || unlockId is not (
            CampaignUnlockIds.Predation or CampaignUnlockIds.Battery or CampaignUnlockIds.Armor));

    private static CampaignPurchaseDecision Decision(GameplayRuntime runtime, string unlockId)
    {
        var definition = runtime.Content.Unlocks[unlockId];
        return new CampaignPurchaseDecision(unlockId, definition.Cost, runtime.Progression.Points);
    }

    private static int PlanIndex(string unlockId)
    {
        for (var index = 0; index < PlannedOrder.Count; index++)
            if (string.Equals(PlannedOrder[index], unlockId, StringComparison.Ordinal))
                return index;
        return PlannedOrder.Count;
    }
}
