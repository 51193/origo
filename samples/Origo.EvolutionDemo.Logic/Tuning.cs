using System;

namespace Origo.EvolutionDemo.Logic;

/// <summary>
///     Read-only compatibility facade over <see cref="DemoSettings.Current" />.
///     Advanced settings edit the active settings object; gameplay code keeps
///     using Tuning for compact, discoverable call sites.
/// </summary>
public static class Tuning
{
    public static float PondRadius => DemoSettings.Current.PondRadius;
    public static int InitialPopulation => DemoSettings.Current.InitialPopulation;
    public static int PopulationCap => DemoSettings.Current.PopulationCap;
    public static int InitialFood => DemoSettings.Current.InitialFood;
    public static int FoodPatchCount => DemoSettings.Current.FoodPatchCount;
    public static float FoodPatchRadius => DemoSettings.Current.FoodPatchRadius;
    public static float FoodPatchFalloff => DemoSettings.Current.FoodPatchFalloff;
    public static int FoodPatchLocalCapacity => DemoSettings.Current.FoodPatchLocalCapacity;
    public static float FoodSourceScanIntervalSeconds => DemoSettings.Current.FoodSourceScanIntervalSeconds;
    public static float FoodSourceSpawnIntervalSeconds => DemoSettings.Current.FoodSourceSpawnIntervalSeconds;
    public static int FoodSourceSpawnBurst => DemoSettings.Current.FoodSourceSpawnBurst;
    public static float FoodSourceWalkIntervalSeconds => DemoSettings.Current.FoodSourceWalkIntervalSeconds;
    public static float FoodSourceWalkSpeedMin => DemoSettings.Current.FoodSourceWalkSpeedMin;
    public static float FoodSourceWalkSpeedMax => DemoSettings.Current.FoodSourceWalkSpeedMax;
    public static float FoodSourceWalkTurnRadians => DemoSettings.Current.FoodSourceWalkTurnRadians;
    public static float FoodSourceCyclePeriodMin => DemoSettings.Current.FoodSourceCyclePeriodMin;
    public static float FoodSourceCyclePeriodMax => DemoSettings.Current.FoodSourceCyclePeriodMax;
    public static float FoodSourceCycleLowMin => DemoSettings.Current.FoodSourceCycleLowMin;
    public static float FoodSourceCycleLowMax => DemoSettings.Current.FoodSourceCycleLowMax;
    public static float FoodSourceCycleHighMin => DemoSettings.Current.FoodSourceCycleHighMin;
    public static float FoodSourceCycleHighMax => DemoSettings.Current.FoodSourceCycleHighMax;
    public static int FoodCap => DemoSettings.Current.FoodCap;
    public static float FoodPerSecond => DemoSettings.Current.FoodPerSecond;
    public static bool CollisionEnabled => DemoSettings.Current.CollisionEnabled;
    public static bool EarlyFoodBloomEnabled => DemoSettings.Current.EarlyFoodBloomEnabled;
    public static int StartupFoodPerFounder => DemoSettings.Current.StartupFoodPerFounder;
    public static int DeathFoodCount => DemoSettings.Current.DeathFoodCount;
    public static float EarlyFoodBonus => DemoSettings.Current.EarlyFoodBonus;
    public static float EarlyFoodRampSeconds => DemoSettings.Current.EarlyFoodRampSeconds;
    public static float EarlyFoodSpawnBonus => DemoSettings.Current.EarlyFoodSpawnBonus;
    public static int FeedFoodCount => DemoSettings.Current.FeedFoodCount;
    public static float FeedCooldownSeconds => DemoSettings.Current.FeedCooldownSeconds;
    public static float SimulationTickSeconds => DemoSettings.Current.SimulationTickSeconds;
    public static int AiUpdateBatch => DemoSettings.Current.AiUpdateBatch;
    public static float MaxCatchUpStepsPerFrame => DemoSettings.Current.MaxCatchUpStepsPerFrame;
    public static float SenseRange => DemoSettings.Current.SenseRange;
    public static float OscillatorPeriodSeconds => DemoSettings.Current.OscillatorPeriodSeconds;
    public static float SenseHalfAngle => DemoSettings.Current.SenseHalfAngle;
    public static float BodyRadius => DemoSettings.Current.BodyRadius;
    public static int CollisionIterations => DemoSettings.Current.CollisionIterations;
    public static float FoodPickupRadius => DemoSettings.Current.FoodPickupRadius;
    public static float CreatureSpeed => DemoSettings.Current.CreatureSpeed;
    public static float TurnSpeed => DemoSettings.Current.TurnSpeed;
    public static float DriveThreshold => DemoSettings.Current.DriveThreshold;
    public static float SpikeThreshold => DemoSettings.Current.SpikeThreshold;
    public static float SpikeEnergyCost => DemoSettings.Current.SpikeEnergyCost;
    public static float SpikeRadius => DemoSettings.Current.SpikeRadius;
    public static float SpikeCooldownSeconds => DemoSettings.Current.SpikeCooldownSeconds;
    public static float InitialEnergy => DemoSettings.Current.InitialEnergy;
    public static float FoodEnergy => DemoSettings.Current.FoodEnergy;
    public static float BaseMetabolismPerSecond => DemoSettings.Current.BaseMetabolismPerSecond;
    public static float MoveEnergyPerSecond => DemoSettings.Current.MoveEnergyPerSecond;
    public static float TurnEnergyPerSecond => DemoSettings.Current.TurnEnergyPerSecond;
    public static float ReproductionEnergy => DemoSettings.Current.ReproductionEnergy;
    public static float ReproductionParentCost => DemoSettings.Current.ReproductionParentCost;
    public static float ReproductionChildEnergy => DemoSettings.Current.ReproductionChildEnergy;
    public static float ReproductionCooldownSeconds => DemoSettings.Current.ReproductionCooldownSeconds;
    public static float MaxAgeSeconds => DemoSettings.Current.MaxAgeSeconds;
    public static double DefaultMutationRate => DemoSettings.Current.DefaultMutationRate;
    public static double FounderMutationRate => DemoSettings.Current.FounderMutationRate;
    public static double BodyMutationRate => DemoSettings.Current.BodyMutationRate;
    public static double BrainMutationRate => DemoSettings.Current.BrainMutationRate;
    public static double BodyLengthMutationRate => DemoSettings.Current.BodyLengthMutationRate;
    public static double FrameshiftMutationRate => DemoSettings.Current.FrameshiftMutationRate;
    public static int LineageMutationThreshold => DemoSettings.Current.LineageMutationThreshold;
    public static bool OvergrowthEnabled => DemoSettings.Current.OvergrowthEnabled;
    public static float OvergrowthScanIntervalSeconds => DemoSettings.Current.OvergrowthScanIntervalSeconds;
    public static float OvergrowthCellSize => DemoSettings.Current.OvergrowthCellSize;
    public static int OvergrowthFoodThreshold => DemoSettings.Current.OvergrowthFoodThreshold;
    public static double OvergrowthMutationRate => DemoSettings.Current.OvergrowthMutationRate;
    public static int OvergrowthFoodCost => DemoSettings.Current.OvergrowthFoodCost;
    public static float OvergrowthChildEnergy => DemoSettings.Current.OvergrowthChildEnergy;
    public static float BodyRadiusBase => DemoSettings.Current.BodyRadiusBase;
    public static float FoodPickupPadding => DemoSettings.Current.FoodPickupPadding;
    public static float BodySizeMin => DemoSettings.Current.BodySizeMin;
    public static float BodySizeMax => DemoSettings.Current.BodySizeMax;
    public static float BodySpeedMin => DemoSettings.Current.BodySpeedMin;
    public static float BodySpeedMax => DemoSettings.Current.BodySpeedMax;
    public static float BodyTurnMin => DemoSettings.Current.BodyTurnMin;
    public static float BodyTurnMax => DemoSettings.Current.BodyTurnMax;
    public static float BodySenseMin => DemoSettings.Current.BodySenseMin;
    public static float BodySenseMax => DemoSettings.Current.BodySenseMax;
    public static float BodySpikeMin => DemoSettings.Current.BodySpikeMin;
    public static float BodySpikeMax => DemoSettings.Current.BodySpikeMax;
    public static float BodyMetabolismMin => DemoSettings.Current.BodyMetabolismMin;
    public static float BodyMetabolismMax => DemoSettings.Current.BodyMetabolismMax;
    public static float BodyAgeMin => DemoSettings.Current.BodyAgeMin;
    public static float BodyAgeMax => DemoSettings.Current.BodyAgeMax;
    public static float BodyMaxEnergyBase => DemoSettings.Current.BodyMaxEnergyBase;
    public static float BodyMaxEnergyPerSize => DemoSettings.Current.BodyMaxEnergyPerSize;
}
