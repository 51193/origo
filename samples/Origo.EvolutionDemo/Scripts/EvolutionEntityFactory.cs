using System;
using System.Globalization;
using Origo.Core.Snd.Metadata;

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

    public static SndMetaData Creature(
        string name,
        string genome,
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
            .SetString(DemoKeys.Genome, genome)
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
            .SetFloat(DemoKeys.SpikeCooldown, 0f)
            .SetFloat(DemoKeys.SpikeFlash, -1f)
            .SetFloat(DemoKeys.Age, 0f)
            .SetInt(DemoKeys.Generation, generation)
            .SetInt(DemoKeys.Lineage, lineage)
            .SetInt(DemoKeys.Children, 0)
            .SetFloat(DemoKeys.ReproCooldown, cooldown)
            .SetString(DemoKeys.Sensors, "0,0,0,0,0,0")
            .SetString(DemoKeys.Outputs, "M 0.00 / T 0.00 / S 0.00")
            .Build();

    public static SndMetaData Food(string name, float x, float y) =>
        new SndMetaFluentBuilder(name)
            .SetNode("main", FoodScene)
            .SetString(DemoKeys.Kind, DemoKeys.FoodKind)
            .SetFloat(DemoKeys.X, x)
            .SetFloat(DemoKeys.Y, y)
            .Build();
}
