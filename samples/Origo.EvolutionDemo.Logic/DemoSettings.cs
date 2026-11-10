using System;
using System.Text.Json;

namespace Origo.EvolutionDemo.Logic;

/// <summary>
///     Central configurable world settings. The demo keeps a single active
///     instance for the foreground pond; Tuning exposes read-only proxies so
///     simulation code keeps a compact call site while the advanced-settings UI
///     edits this object.
/// </summary>
public sealed class DemoSettings
{
    /// <summary>Active foreground settings used by Tuning proxies.</summary>
    public static DemoSettings Current { get; set; } = new();

    /// <summary>Creates a fresh settings object with the current defaults.</summary>
    public static DemoSettings Defaults() => new();

    // World / population.
    public float PondRadius { get; set; } = 10_000f;
    public int InitialPopulation { get; set; } = 900;
    public int PopulationCap { get; set; } = 2_500;
    public int InitialFood { get; set; } = 2_000;
    public int FoodCap { get; set; } = 6_000;
    public float FoodPerSecond { get; set; } = 250f;
    public float BodyRadius { get; set; } = 5f;
    public float CreatureSpeed { get; set; } = 55f;
    public float TurnSpeed { get; set; } = 2.8f;
    public float SenseRange { get; set; } = 120f;
    public float FoodPickupRadius { get; set; } = 14f;
    public float SenseHalfAngle { get; set; } = MathF.PI / 3f;
    public float OscillatorPeriodSeconds { get; set; } = 6f;
    public float DriveThreshold { get; set; } = 0.15f;
    public float SimulationTickSeconds { get; set; } = 0.1f;
    public int AiUpdateBatch { get; set; } = 600;
    public float MaxCatchUpStepsPerFrame { get; set; } = 40f;

    // Energy / reproduction.
    public float InitialEnergy { get; set; } = 78f;
    public float FoodEnergy { get; set; } = 18f;
    public float ToughFoodEnergy { get; set; } = 25f;
    public float ToxicFoodEnergy { get; set; } = 32f;
    public float ToughFoodShare { get; set; } = 0.22f;
    public float ToxicFoodShare { get; set; } = 0.08f;
    public float BaseMetabolismPerSecond { get; set; } = 0.55f;
    public float MoveEnergyPerSecond { get; set; } = 1.1f;
    public float TurnEnergyPerSecond { get; set; } = 0.2f;
    public float ReproductionEnergy { get; set; } = 140f;
    public float ReproductionParentCost { get; set; } = 70f;
    public float ReproductionChildEnergy { get; set; } = 30f;
    public float ReproductionCooldownSeconds { get; set; } = 10f;
    public float MaxAgeSeconds { get; set; } = 300f;
    public double DefaultMutationRate { get; set; } = 0.015d;
    public double FounderMutationRate { get; set; } = 0.10d;
    public double BodyMutationRate { get; set; } = 0.015d;
    public double BrainMutationRate { get; set; } = 0.003d;
    public double BodyLengthMutationRate { get; set; } = 0.005d;
    public double FrameshiftMutationRate { get; set; }
    public int LineageMutationThreshold { get; set; } = 2;

    // Spike.
    public float SpikeThreshold { get; set; } = 0.05f;
    public float SpikeEnergyCost { get; set; } = 3f;
    public float SpikePerPelletCost { get; set; } = 1f;
    public float SpikeCooldownPerExtraPellet { get; set; } = 0.10f;
    public float SpikeEnergyPerUnit { get; set; } = 0.04f;
    public float SpikeAimHalfAngleRadians { get; set; } = MathF.PI / 3f;
    public float SpikeBeamRadius { get; set; } = 32f;
    public float SpikeDrainFraction { get; set; } = 0.70f;
    public float SpikeRadius { get; set; } = 48f;
    public float SpikeCooldownSeconds { get; set; } = 1.0f;

    // Collision / bloom / feeding.
    public bool CollisionEnabled { get; set; }
    public int CollisionIterations { get; set; } = 2;
    public bool EarlyFoodBloomEnabled { get; set; }
    public float EarlyFoodBonus { get; set; } = 500f;
    public float EarlyFoodRampSeconds { get; set; } = 180f;
    public float EarlyFoodSpawnBonus { get; set; } = 50f;
    public int StartupFoodPerFounder { get; set; } = 1;
    public int DeathFoodCount { get; set; } = 2;
    public float CarcassFoodIntervalSeconds { get; set; } = 1.25f;
    public int FeedFoodCount { get; set; } = 40;
    public float FeedCooldownSeconds { get; set; } = 5f;

    // Food patches / adaptive sources.
    public int FoodPatchCount { get; set; } = 16;
    public float FoodPatchRadius { get; set; } = 900f;
    public float FoodPatchFalloff { get; set; } = 0.55f;
    public int FoodPatchLocalCapacity { get; set; } = 240;
    public int FoodPatchLayoutVersion { get; set; } = 2;

    // Body-gene phenotype ranges.
    public float BodyRadiusBase { get; set; } = 5f;
    public float FoodPickupPadding { get; set; } = 9f;
    public float BodySizeMin { get; set; } = 0.65f;
    public float BodySizeMax { get; set; } = 1.8f;
    public float BodySpeedMin { get; set; } = 30f;
    public float BodySpeedMax { get; set; } = 90f;
    public float BodyTurnMin { get; set; } = 1.5f;
    public float BodyTurnMax { get; set; } = 4.5f;
    public float BodySenseMin { get; set; } = 60f;
    public float BodySenseMax { get; set; } = 260f;
    public float BodySpikeMin { get; set; } = 20f;
    public float BodySpikeMax { get; set; } = 120f;
    public float BodyMetabolismMin { get; set; } = 0.55f;
    public float BodyMetabolismMax { get; set; } = 1.6f;
    public float BodyAgeMin { get; set; } = 120f;
    public float BodyAgeMax { get; set; } = 600f;
    public float BodyMaxEnergyBase { get; set; } = 120f;
    public float BodyMaxEnergyPerSize { get; set; } = 80f;
    public float FoodSourceScanIntervalSeconds { get; set; } = 1f;
    public float FoodSourceSpawnIntervalSeconds { get; set; } = 0.5f;
    public int FoodSourceSpawnBurst { get; set; } = 8;
    public float FoodSourceWalkIntervalSeconds { get; set; } = 0.5f;
    public float FoodSourceWalkSpeedMin { get; set; } = 4f;
    public float FoodSourceWalkSpeedMax { get; set; } = 16f;
    public float FoodSourceWalkTurnRadians { get; set; } = 0.55f;
    public float FoodSourceCyclePeriodMin { get; set; } = 90f;
    public float FoodSourceCyclePeriodMax { get; set; } = 240f;
    public float FoodSourceCycleLowMin { get; set; } = 0.05f;
    public float FoodSourceCycleLowMax { get; set; } = 0.35f;
    public float FoodSourceCycleHighMin { get; set; } = 1.4f;
    public float FoodSourceCycleHighMax { get; set; } = 2.5f;

    // Global overgrowth / mutation pressure.
    public bool OvergrowthEnabled { get; set; } = true;
    public float OvergrowthScanIntervalSeconds { get; set; } = 8f;
    public float OvergrowthCellSize { get; set; } = 1_000f;
    public int OvergrowthFoodThreshold { get; set; } = 70;
    public double OvergrowthMutationRate { get; set; } = 0.25d;
    public int OvergrowthFoodCost { get; set; } = 28;
    public float OvergrowthChildEnergy { get; set; } = 55f;

    /// <summary>Creates a shallow clone suitable for a UI draft.</summary>
    public DemoSettings Clone() => (DemoSettings)MemberwiseClone();

    /// <summary>Clamps values into playable ranges and normalizes paired ranges.</summary>
    public void Normalize()
    {
        PondRadius = Math.Clamp(PondRadius, 500f, 50_000f);
        InitialPopulation = Math.Clamp(InitialPopulation, 1, 10_000);
        PopulationCap = Math.Clamp(PopulationCap, InitialPopulation, 20_000);
        InitialFood = Math.Clamp(InitialFood, 0, 50_000);
        FoodCap = Math.Clamp(FoodCap, InitialFood, 50_000);
        FoodPerSecond = Math.Clamp(FoodPerSecond, 0f, 5_000f);
        BodyRadius = Math.Clamp(BodyRadius, 0.5f, 30f);
        CreatureSpeed = Math.Clamp(CreatureSpeed, 1f, 500f);
        TurnSpeed = Math.Clamp(TurnSpeed, 0.05f, 20f);
        SenseRange = Math.Clamp(SenseRange, 5f, 2_000f);
        SenseHalfAngle = Math.Clamp(SenseHalfAngle, 0.05f, MathF.PI / 2f);
        OscillatorPeriodSeconds = Math.Clamp(OscillatorPeriodSeconds, 0.1f, 600f);
        DriveThreshold = Math.Clamp(DriveThreshold, 0f, 1f);
        SimulationTickSeconds = Math.Clamp(SimulationTickSeconds, 0.02f, 1f);
        AiUpdateBatch = Math.Clamp(AiUpdateBatch, 1, 20_000);
        MaxCatchUpStepsPerFrame = Math.Clamp(MaxCatchUpStepsPerFrame, 1f, 1_000f);
        FoodPickupRadius = Math.Clamp(FoodPickupRadius, 1f, 500f);
        InitialEnergy = Math.Clamp(InitialEnergy, 1f, 10_000f);
        FoodEnergy = Math.Clamp(FoodEnergy, 0f, 10_000f);
        ToughFoodEnergy = Math.Clamp(ToughFoodEnergy, 0f, 10_000f);
        ToxicFoodEnergy = Math.Clamp(ToxicFoodEnergy, 0f, 10_000f);
        ToughFoodShare = Math.Clamp(ToughFoodShare, 0f, 0.95f);
        ToxicFoodShare = Math.Clamp(ToxicFoodShare, 0f, 0.95f - ToughFoodShare);
        BaseMetabolismPerSecond = Math.Clamp(BaseMetabolismPerSecond, 0f, 100f);
        MoveEnergyPerSecond = Math.Clamp(MoveEnergyPerSecond, 0f, 100f);
        TurnEnergyPerSecond = Math.Clamp(TurnEnergyPerSecond, 0f, 100f);
        ReproductionEnergy = Math.Clamp(ReproductionEnergy, 1f, 10_000f);
        ReproductionParentCost = Math.Clamp(ReproductionParentCost, 0f, 10_000f);
        ReproductionChildEnergy = Math.Clamp(ReproductionChildEnergy, 0f, 10_000f);
        ReproductionCooldownSeconds = Math.Clamp(ReproductionCooldownSeconds, 0.05f, 3_600f);
        MaxAgeSeconds = Math.Clamp(MaxAgeSeconds, 1f, 100_000f);
        DefaultMutationRate = Math.Clamp(DefaultMutationRate, 0d, 1d);
        FounderMutationRate = Math.Clamp(FounderMutationRate, 0d, 1d);
        BodyMutationRate = Math.Clamp(BodyMutationRate, 0d, 1d);
        BrainMutationRate = Math.Clamp(BrainMutationRate, 0d, 1d);
        BodyLengthMutationRate = Math.Clamp(BodyLengthMutationRate, 0d, 1d);
        FrameshiftMutationRate = Math.Clamp(FrameshiftMutationRate, 0d, 1d);
        LineageMutationThreshold = Math.Clamp(LineageMutationThreshold, 1, 1_000);
        SpikeThreshold = Math.Clamp(SpikeThreshold, 0f, 1f);
        SpikeEnergyCost = Math.Clamp(SpikeEnergyCost, 0f, 10_000f);
        SpikePerPelletCost = Math.Clamp(SpikePerPelletCost, 0f, 10_000f);
        SpikeCooldownPerExtraPellet = Math.Clamp(SpikeCooldownPerExtraPellet, 0f, 100f);
        SpikeEnergyPerUnit = Math.Clamp(SpikeEnergyPerUnit, 0f, 100f);
        SpikeAimHalfAngleRadians = Math.Clamp(SpikeAimHalfAngleRadians, 0f, MathF.PI);
        SpikeBeamRadius = Math.Clamp(SpikeBeamRadius, 0.1f, 500f);
        SpikeDrainFraction = Math.Clamp(SpikeDrainFraction, 0f, 1f);
        SpikeRadius = Math.Clamp(SpikeRadius, 1f, 2_000f);
        SpikeCooldownSeconds = Math.Clamp(SpikeCooldownSeconds, 0.05f, 3_600f);
        CollisionIterations = Math.Clamp(CollisionIterations, 1, 20);
        EarlyFoodBonus = Math.Clamp(EarlyFoodBonus, 0f, 50_000f);
        EarlyFoodRampSeconds = Math.Clamp(EarlyFoodRampSeconds, 1f, 10_000f);
        EarlyFoodSpawnBonus = Math.Clamp(EarlyFoodSpawnBonus, 0f, 10_000f);
        StartupFoodPerFounder = Math.Clamp(StartupFoodPerFounder, 0, 100);
        DeathFoodCount = Math.Clamp(DeathFoodCount, 0, 200);
        CarcassFoodIntervalSeconds = Math.Clamp(CarcassFoodIntervalSeconds, 0.1f, 60f);
        FeedFoodCount = Math.Clamp(FeedFoodCount, 0, 10_000);
        FeedCooldownSeconds = Math.Clamp(FeedCooldownSeconds, 0f, 3_600f);
        FoodPatchCount = Math.Clamp(FoodPatchCount, 0, 256);
        FoodPatchRadius = Math.Clamp(FoodPatchRadius, 10f, PondRadius);
        FoodPatchFalloff = Math.Clamp(FoodPatchFalloff, 0f, 100f);
        FoodPatchLocalCapacity = Math.Clamp(FoodPatchLocalCapacity, 1, 100_000);
        FoodSourceScanIntervalSeconds = Math.Clamp(FoodSourceScanIntervalSeconds, 0.02f, 3_600f);
        FoodSourceSpawnIntervalSeconds = Math.Clamp(FoodSourceSpawnIntervalSeconds, 0.02f, 3_600f);
        FoodSourceSpawnBurst = Math.Clamp(FoodSourceSpawnBurst, 1, 1_000);
        FoodSourceWalkIntervalSeconds = Math.Clamp(FoodSourceWalkIntervalSeconds, 0.02f, 3_600f);
        FoodSourceWalkSpeedMin = Math.Clamp(FoodSourceWalkSpeedMin, 0f, 10_000f);
        FoodSourceWalkSpeedMax = Math.Clamp(FoodSourceWalkSpeedMax, FoodSourceWalkSpeedMin, 10_000f);
        FoodSourceWalkTurnRadians = Math.Clamp(FoodSourceWalkTurnRadians, 0f, MathF.PI);
        FoodSourceCyclePeriodMin = Math.Clamp(FoodSourceCyclePeriodMin, 1f, 100_000f);
        FoodSourceCyclePeriodMax = Math.Clamp(FoodSourceCyclePeriodMax, FoodSourceCyclePeriodMin, 100_000f);
        FoodSourceCycleLowMin = Math.Clamp(FoodSourceCycleLowMin, 0f, 10f);
        FoodSourceCycleLowMax = Math.Clamp(FoodSourceCycleLowMax, FoodSourceCycleLowMin, 10f);
        FoodSourceCycleHighMin = Math.Clamp(FoodSourceCycleHighMin, 0f, 100f);
        FoodSourceCycleHighMax = Math.Clamp(FoodSourceCycleHighMax, FoodSourceCycleHighMin, 100f);
        OvergrowthScanIntervalSeconds = Math.Clamp(OvergrowthScanIntervalSeconds, 0.1f, 3_600f);
        OvergrowthCellSize = Math.Clamp(OvergrowthCellSize, 50f, PondRadius);
        OvergrowthFoodThreshold = Math.Clamp(OvergrowthFoodThreshold, 1, 100_000);
        OvergrowthFoodCost = Math.Clamp(OvergrowthFoodCost, 1, OvergrowthFoodThreshold);
        OvergrowthMutationRate = Math.Clamp(OvergrowthMutationRate, 0d, 1d);
        OvergrowthChildEnergy = Math.Clamp(OvergrowthChildEnergy, 0f, 10_000f);
        BodyRadiusBase = Math.Clamp(BodyRadiusBase, 0.5f, 30f);
        FoodPickupPadding = Math.Clamp(FoodPickupPadding, 0f, 100f);
        BodySizeMin = Math.Clamp(BodySizeMin, 0.1f, 5f);
        BodySizeMax = Math.Clamp(BodySizeMax, BodySizeMin, 5f);
        BodySpeedMin = Math.Clamp(BodySpeedMin, 1f, 500f);
        BodySpeedMax = Math.Clamp(BodySpeedMax, BodySpeedMin, 500f);
        BodyTurnMin = Math.Clamp(BodyTurnMin, 0.05f, 20f);
        BodyTurnMax = Math.Clamp(BodyTurnMax, BodyTurnMin, 20f);
        BodySenseMin = Math.Clamp(BodySenseMin, 5f, 2_000f);
        BodySenseMax = Math.Clamp(BodySenseMax, BodySenseMin, 2_000f);
        BodySpikeMin = Math.Clamp(BodySpikeMin, 1f, 2_000f);
        BodySpikeMax = Math.Clamp(BodySpikeMax, BodySpikeMin, 2_000f);
        BodyMetabolismMin = Math.Clamp(BodyMetabolismMin, 0f, 100f);
        BodyMetabolismMax = Math.Clamp(BodyMetabolismMax, BodyMetabolismMin, 100f);
        BodyAgeMin = Math.Clamp(BodyAgeMin, 1f, 100_000f);
        BodyAgeMax = Math.Clamp(BodyAgeMax, BodyAgeMin, 100_000f);
        BodyMaxEnergyBase = Math.Clamp(BodyMaxEnergyBase, 0f, 10_000f);
        BodyMaxEnergyPerSize = Math.Clamp(BodyMaxEnergyPerSize, 0f, 10_000f);
    }

    /// <summary>Serializes all settings for controller/save persistence.</summary>
    public string ToJson() => JsonSerializer.Serialize(this);

    /// <summary>Parses persisted settings, falling back to defaults on invalid input.</summary>
    public static DemoSettings FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new DemoSettings();
        try
        {
            var restored = JsonSerializer.Deserialize<DemoSettings>(json) ?? new DemoSettings();
            using var document = JsonDocument.Parse(json);
            var hasLayoutVersion = document.RootElement.TryGetProperty(
                nameof(FoodPatchLayoutVersion),
                out var layoutVersion);
            if (!hasLayoutVersion || layoutVersion.GetInt32() < 2 || restored.FoodPatchLayoutVersion < 2)
            {
                // Pre-versioned saves used a tightly concentrated 420/1.8
                // patch. Move them to the wide, gentle profile on load.
                restored.FoodPatchRadius = 900f;
                restored.FoodPatchFalloff = 0.55f;
                restored.FoodPatchLocalCapacity = 300;
                restored.FoodPatchLayoutVersion = 2;
            }

            restored.Normalize();
            return restored;
        }
        catch (JsonException)
        {
            return new DemoSettings();
        }
    }
}
