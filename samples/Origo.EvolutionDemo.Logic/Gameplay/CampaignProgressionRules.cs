using System;
using System.Collections.Generic;
using Origo.EvolutionDemo.Logic.V4;

namespace Origo.EvolutionDemo.Logic.Gameplay;

/// <summary>Stable ids for the campaign progression tree.</summary>
public static class CampaignUnlockIds
{
    public const string Core = "unlock.core";
    public const string ToughFood = "unlock.forage.tough_food";
    public const string HardDigest = "unlock.forage.hard_digest";
    public const string Sprint = "unlock.forage.sprint";
    public const string Pond1 = "unlock.pond.1";
    public const string ToxicFood = "unlock.chem.toxic_food";
    public const string ToxinResistance = "unlock.chem.toxin_resistance";
    public const string CreatureSense = "unlock.sense.creature";
    public const string Rhythm = "unlock.sense.rhythm";
    public const string Pond2 = "unlock.pond.2";
    public const string CorpseCycle = "unlock.death.corpse";
    public const string CarrionSense = "unlock.carrion.sense";
    public const string Pond3 = "unlock.pond.3";
    public const string Predation = "unlock.predation.spike";
    public const string Battery = "unlock.predation.battery";
    public const string Armor = "unlock.predation.armor";
    public const string Pond4 = "unlock.pond.4";
    public const string FoodTypeVision = "unlock.vision.foodtype";
    public const string Enrichment = "unlock.spawn.enrichment";
    public const string Pond5 = "unlock.pond.5";
    public const string Pond6 = "unlock.pond.6";
}

/// <summary>Campaign start presets and unlock-driven world rules.</summary>
public static class CampaignProgressionRules
{
    private static readonly string[] PondUnlocks =
    [
        CampaignUnlockIds.Pond1, CampaignUnlockIds.Pond2, CampaignUnlockIds.Pond3,
        CampaignUnlockIds.Pond4, CampaignUnlockIds.Pond5, CampaignUnlockIds.Pond6,
    ];

    private static readonly float[] Radii = [700f, 1_200f, 2_000f, 3_200f, 5_000f, 7_500f, 10_000f];
    private static readonly int[] PopulationCaps = [64, 120, 250, 500, 900, 1_600, 2_500];
    private static readonly int[] InitialPopulations = [24, 32, 40, 60, 90, 120, 150];
    private static readonly int[] FoodCaps = [180, 260, 400, 700, 1_100, 1_700, 2_500];
    private static readonly float[] FoodRates = [12f, 18f, 30f, 55f, 90f, 140f, 220f];
    private static readonly int[] PatchCounts = [3, 5, 7, 10, 14, 18, 20];

    /// <summary>Returns the unlocked pond tier (0–6). Sandbox uses the final tier.</summary>
    public static int PondLevel(GameplayProgression progression)
    {
        ArgumentNullException.ThrowIfNull(progression);
        if (progression.Mode == GameplayMode.Sandbox)
            return 6;
        var level = 0;
        foreach (var unlock in PondUnlocks)
            if (progression.UnlockedIds.Contains(unlock))
                level++;
        return Math.Clamp(level, 0, 6);
    }

    /// <summary>Applies the campaign's primordial/expanded preset to a new pond.</summary>
    public static void ApplyStartSettings(DemoSettings settings, GameplayProgression progression)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(progression);
        var level = PondLevel(progression);
        settings.PondRadius = Radii[level];
        settings.InitialPopulation = InitialPopulations[level];
        settings.PopulationCap = PopulationCaps[level];
        settings.InitialFood = FoodCaps[level];
        settings.FoodCap = FoodCaps[level];
        settings.FoodPerSecond = FoodRates[level];
        settings.FoodPatchCount = PatchCounts[level];
        settings.FoodPatchRadius = MathF.Min(60f, settings.PondRadius * 0.2f);
        settings.CollisionEnabled = false;
        settings.EarlyFoodBloomEnabled = false;
        var (tough, toxic) = FoodShares(progression, settings);
        settings.ToughFoodShare = tough;
        settings.ToxicFoodShare = toxic;
        settings.Normalize();
    }

    /// <summary>Number of food sources at the current pond tier.</summary>
    public static int FoodPatchCount(GameplayProgression progression) =>
        PatchCounts[PondLevel(progression)];

    /// <summary>
    ///     Applies only the live-expandable world settings to an active pond.
    ///     Initial population is intentionally not changed for a running pond.
    /// </summary>
    public static void ApplyWorldScale(DemoSettings settings, GameplayProgression progression)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(progression);
        var level = PondLevel(progression);
        settings.PondRadius = Radii[level];
        settings.PopulationCap = PopulationCaps[level];
        settings.FoodCap = FoodCaps[level];
        settings.FoodPerSecond = FoodRates[level];
        settings.FoodPatchRadius = MathF.Min(60f, settings.PondRadius * 0.2f);
        var (tough, toxic) = FoodShares(progression, settings);
        settings.ToughFoodShare = tough;
        settings.ToxicFoodShare = toxic;
        settings.Normalize();
    }

    /// <summary>
    ///     The demo organ-mask bit that an unlock's innovator injection should
    ///     express, or null when the unlock has no body organ to inject.
    /// </summary>
    public static int? InnovationOrganMask(string unlockId) => unlockId switch
    {
        CampaignUnlockIds.HardDigest => (int)DemoOrganMaskV4.HardDigest,
        CampaignUnlockIds.ToxinResistance => (int)DemoOrganMaskV4.ToxinResistance,
        CampaignUnlockIds.CreatureSense => (int)DemoOrganMaskV4.CreatureSense,
        CampaignUnlockIds.Rhythm => (int)DemoOrganMaskV4.Rhythm,
        CampaignUnlockIds.CarrionSense => (int)DemoOrganMaskV4.CarrionSense,
        CampaignUnlockIds.Predation => (int)DemoOrganMaskV4.Spike,
        CampaignUnlockIds.Battery => (int)DemoOrganMaskV4.SpikeBattery,
        CampaignUnlockIds.Armor => (int)DemoOrganMaskV4.Armor,
        CampaignUnlockIds.Sprint => (int)DemoOrganMaskV4.Sprint,
        CampaignUnlockIds.FoodTypeVision => (int)DemoOrganMaskV4.FoodTypeVision,
        _ => null,
    };

    /// <summary>Campaign food composition, staged by the unlocked food mechanics.</summary>
    public static (float Tough, float Toxic) FoodShares(GameplayProgression progression, DemoSettings settings)
    {
        ArgumentNullException.ThrowIfNull(progression);
        ArgumentNullException.ThrowIfNull(settings);
        if (progression.Mode == GameplayMode.Sandbox)
            return (settings.ToughFoodShare, settings.ToxicFoodShare);
        if (progression.UnlockedIds.Contains(CampaignUnlockIds.FoodTypeVision))
            return (0.40f, 0.40f);
        var tough = progression.UnlockedIds.Contains(CampaignUnlockIds.ToughFood) ? 0.15f : 0f;
        var toxic = progression.UnlockedIds.Contains(CampaignUnlockIds.ToxicFood) ? 0.15f : 0f;
        if (tough > 0f && toxic > 0f)
            return (0.25f, 0.15f);
        return (tough, toxic);
    }
}
