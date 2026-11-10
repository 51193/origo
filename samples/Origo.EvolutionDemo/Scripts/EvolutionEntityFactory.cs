using System;
using System.Globalization;
using Origo.Core.Snd.Metadata;
using Origo.EvolutionDemo.Logic;

namespace Origo.EvolutionDemo;

/// <summary>
///     Shared metadata factories for entities that are spawned by more than one
///     strategy (controller reproduction, food sources, global overgrowth).
///     Keeping the data shape in one place prevents divergent spawn fields.
/// </summary>
internal static class EvolutionEntityFactory
{
    public const string CreatureScene = "res://Origo/demo/CreatureEntity.tscn";
    public const string FoodScene = "res://Origo/demo/FoodEntity.tscn";

    public static string CreatureName(int id) =>
        string.Create(CultureInfo.InvariantCulture, $"creature_{id:000000}");

    public static string FoodName(int id) =>
        string.Create(CultureInfo.InvariantCulture, $"food_{id:000000}");

    public static string CorpseName(int id) =>
        string.Create(CultureInfo.InvariantCulture, $"corpse_{id:000000}");

    public static SndMetaData Creature(
        string name,
        Origo.EvolutionDemo.Logic.CreatureGenome genome,
        Origo.EvolutionDemo.Logic.BodyPhenotype phenotype,
        float x,
        float y,
        float heading,
        float energy,
        int generation,
        int lineage,
        float cooldown) =>
        new SndMetaFluentBuilder(name)
            .SetNode("main", CreatureScene)
            .SetString(DemoKeys.Kind, DemoKeys.CreatureKind)
            .SetString(DemoKeys.BodyGenome, genome.Body)
            .SetString(DemoKeys.BrainGenome, genome.Brain)
            .SetFloat(DemoKeys.BodyRadius, phenotype.BodyRadius)
            .SetFloat(DemoKeys.FoodPickupRadius, phenotype.FoodPickupRadius)
            .SetFloat(DemoKeys.MaxEnergy, phenotype.MaxEnergy)
            .SetFloat(DemoKeys.MoveSpeed, phenotype.MoveSpeed)
            .SetFloat(DemoKeys.TurnSpeed, phenotype.TurnSpeed)
            .SetFloat(DemoKeys.SenseRange, phenotype.SenseRange)
            .SetFloat(DemoKeys.SpikeRadius, phenotype.SpikeRadius)
            .SetFloat(DemoKeys.BasalMetabolism, phenotype.BasalMetabolism)
            .SetFloat(DemoKeys.MaxAge, phenotype.MaxAge)
            .SetBool(DemoKeys.SpikeOrgan, phenotype.SpikeOrgan)
            .SetBool(DemoKeys.SpikeBattery, phenotype.SpikeBatteryOrgan)
            .SetInt(DemoKeys.SpikeCount, phenotype.SpikeCount)
            .SetFloat(DemoKeys.SpikeSpread, phenotype.SpikeSpreadRadians)
            .SetFloat(DemoKeys.SpikeJitter, phenotype.SpikeJitterRadians)
            .SetBool(DemoKeys.VisionOrgan, phenotype.VisionOrgan)
            .SetBool(DemoKeys.RhythmOrgan, phenotype.RhythmOrgan)
            .SetBool(DemoKeys.CreatureSenseOrgan, phenotype.CreatureSenseOrgan)
            .SetBool(DemoKeys.HardDigestOrgan, phenotype.HardDigestOrgan)
            .SetBool(DemoKeys.ToxinResistanceOrgan, phenotype.ToxinResistanceOrgan)
            .SetInt(DemoKeys.HiddenLayers, phenotype.HiddenLayers)
            .SetInt(DemoKeys.HiddenWidth1, phenotype.HiddenWidth1)
            .SetInt(DemoKeys.HiddenWidth2, phenotype.HiddenWidth2)
            .SetInt(DemoKeys.HiddenWidth3, phenotype.HiddenWidth3)
            .SetFloat(DemoKeys.X, x)
            .SetFloat(DemoKeys.Y, y)
            .SetFloat(DemoKeys.Heading, heading)
            .SetFloat(DemoKeys.PrevX, x)
            .SetFloat(DemoKeys.PrevY, y)
            .SetFloat(DemoKeys.PrevHeading, heading)
            .SetFloat(DemoKeys.Energy, energy)
            .SetFloat(DemoKeys.Drive, 0.2f)
            .SetFloat(DemoKeys.Turn, 0f)
            .SetFloat(DemoKeys.SpikeDrive, 0f)
            .SetFloat(DemoKeys.Aim, 0f)
            .SetFloat(DemoKeys.SpikeCooldown, 0f)
            .SetFloat(DemoKeys.SpikeFlash, -1f)
            .SetFloat(DemoKeys.SpikeEndX, 0f)
            .SetFloat(DemoKeys.SpikeEndY, 0f)
            .SetString(DemoKeys.SpikeFan, string.Empty)
            .SetFloat(DemoKeys.Age, 0f)
            .SetInt(DemoKeys.Generation, generation)
            .SetInt(DemoKeys.Lineage, lineage)
            .SetInt(DemoKeys.Children, 0)
            .SetInt(DemoKeys.Kills, 0)
            .SetFloat(DemoKeys.ReproCooldown, cooldown)
            .SetString(DemoKeys.Sensors, "0,0,0,0,0,0,0,0,0,0,0,0")
            .SetString(DemoKeys.Outputs, "M 0.00 / T 0.00 / S 0.00 / A 0.00")
            .Build();

    public static SndMetaData Food(
        string name,
        float x,
        float y,
        bool fromCorpse = false,
        FoodType foodType = FoodType.Soft)
    {
        var food = new SndMetaFluentBuilder(name)
            .SetNode("main", FoodScene)
            .SetString(DemoKeys.Kind, DemoKeys.FoodKind)
            .SetInt(DemoKeys.FoodType, (int)foodType)
            .SetFloat(DemoKeys.X, x)
            .SetFloat(DemoKeys.Y, y);
        if (fromCorpse)
            food.SetBool(DemoKeys.FoodFromCorpse, true);
        return food.Build();
    }

    public static SndMetaData Corpse(
        string name,
        float x,
        float y,
        int foodRemaining,
        float intervalSeconds) =>
        new SndMetaFluentBuilder(name)
            .SetNode("main", FoodScene)
            .SetString(DemoKeys.Kind, DemoKeys.CorpseKind)
            .SetFloat(DemoKeys.X, x)
            .SetFloat(DemoKeys.Y, y)
            .SetInt(DemoKeys.CorpseFoodRemaining, foodRemaining)
            .SetInt(DemoKeys.CorpseFoodTotal, foodRemaining)
            .SetFloat(DemoKeys.CorpseTimer, 0f)
            .SetFloat(DemoKeys.CorpseInterval, intervalSeconds)
            .Build();
}
