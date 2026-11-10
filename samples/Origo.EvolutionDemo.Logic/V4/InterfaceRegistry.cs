using System;
using System.Collections.Generic;

namespace Origo.EvolutionDemo.Logic.V4;

/// <summary>Metadata for one sensor interface available to V4 genomes.</summary>
public sealed record SensorDefinitionV4(ushort Id, string Key, string DisplayName);

/// <summary>Metadata for one action interface available to V4 genomes.</summary>
public sealed record ActionDefinitionV4(ushort Id, string Key, string DisplayName);

/// <summary>
///     Registry of stable sensor/action interface IDs. Adding a new interface is
///     an explicit registration, not a new fixed slot in the genome format.
/// </summary>
public static class InterfaceRegistryV4
{
    private static readonly Dictionary<ushort, SensorDefinitionV4> Sensors = [];
    private static readonly Dictionary<ushort, ActionDefinitionV4> Actions = [];

    static InterfaceRegistryV4()
    {
        RegisterSensor(new SensorDefinitionV4((ushort)SensorKindV4.FoodAhead, "food.ahead", "Food ahead"));
        RegisterSensor(new SensorDefinitionV4((ushort)SensorKindV4.FoodLeft, "food.left", "Food left"));
        RegisterSensor(new SensorDefinitionV4((ushort)SensorKindV4.FoodRight, "food.right", "Food right"));
        RegisterSensor(new SensorDefinitionV4((ushort)SensorKindV4.WallAhead, "wall.ahead", "Wall ahead"));
        RegisterSensor(new SensorDefinitionV4((ushort)SensorKindV4.Energy, "energy", "Energy"));
        RegisterSensor(new SensorDefinitionV4((ushort)SensorKindV4.CreatureAhead, "creature.ahead", "Creature ahead"));
        RegisterSensor(new SensorDefinitionV4((ushort)SensorKindV4.CorpseAhead, "corpse.ahead", "Carrion ahead"));
        RegisterSensor(new SensorDefinitionV4((ushort)SensorKindV4.Sin, "rhythm.sin", "Rhythm sin"));
        RegisterSensor(new SensorDefinitionV4((ushort)SensorKindV4.Cos, "rhythm.cos", "Rhythm cos"));

        RegisterAction(new ActionDefinitionV4((ushort)ActionKindV4.Move, "move", "Move"));
        RegisterAction(new ActionDefinitionV4((ushort)ActionKindV4.Turn, "turn", "Turn"));
        RegisterAction(new ActionDefinitionV4((ushort)ActionKindV4.Spike, "spike", "Spike"));
        RegisterAction(new ActionDefinitionV4((ushort)ActionKindV4.Aim, "aim", "Aim"));
    }

    public static IReadOnlyDictionary<ushort, SensorDefinitionV4> SensorDefinitions => Sensors;

    public static IReadOnlyDictionary<ushort, ActionDefinitionV4> ActionDefinitions => Actions;

    public static void RegisterSensor(SensorDefinitionV4 definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Sensors[definition.Id] = definition;
    }

    public static void RegisterAction(ActionDefinitionV4 definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Actions[definition.Id] = definition;
    }
}
