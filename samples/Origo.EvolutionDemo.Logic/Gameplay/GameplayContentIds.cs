using System;
using System.Collections.Generic;
using Origo.EvolutionDemo.Logic.V4;

namespace Origo.EvolutionDemo.Logic.Gameplay;

/// <summary>Stable content ids for current V4 organs/actions/sensors.</summary>
public static class GameplayContentIds
{
    private static readonly Dictionary<OrganKindV4, string> Organs = new()
    {
        [OrganKindV4.SpikeOrgan] = "gene.organ.spike",
        [OrganKindV4.Vision] = "gene.organ.vision",
        [OrganKindV4.Rhythm] = "gene.organ.rhythm",
        [OrganKindV4.CreatureSense] = "gene.organ.creature_sense",
        [OrganKindV4.SpikeBattery] = "gene.organ.spike_battery",
        [OrganKindV4.HardDigest] = "gene.organ.hard_digest",
        [OrganKindV4.ToxinResistance] = "gene.organ.toxin_resistance",
        [OrganKindV4.CarrionSense] = "gene.organ.carrion_sense",
        [OrganKindV4.Sprint] = "gene.organ.sprint",
    };

    private static readonly Dictionary<ActionKindV4, string> Actions = new()
    {
        [ActionKindV4.Move] = "gene.action.move",
        [ActionKindV4.Turn] = "gene.action.turn",
        [ActionKindV4.Spike] = "gene.action.spike",
        [ActionKindV4.Aim] = "gene.action.aim",
        [ActionKindV4.Sprint] = "gene.action.sprint",
    };

    private static readonly Dictionary<SensorKindV4, string> Sensors = new()
    {
        [SensorKindV4.FoodAhead] = "gene.sensor.food_ahead",
        [SensorKindV4.FoodLeft] = "gene.sensor.food_left",
        [SensorKindV4.FoodRight] = "gene.sensor.food_right",
        [SensorKindV4.WallAhead] = "gene.sensor.wall_ahead",
        [SensorKindV4.Energy] = "gene.sensor.energy",
        [SensorKindV4.CreatureAhead] = "gene.sensor.creature_ahead",
        [SensorKindV4.CorpseAhead] = "gene.sensor.corpse_ahead",
        [SensorKindV4.Sin] = "gene.sensor.sin",
        [SensorKindV4.Cos] = "gene.sensor.cos",
    };

    public static string? ForOrgan(OrganKindV4 organ) =>
        Organs.TryGetValue(organ, out var id) ? id : null;

    public static string? ForAction(ActionKindV4 action) =>
        Actions.TryGetValue(action, out var id) ? id : null;

    public static string? ForSensor(SensorKindV4 sensor) =>
        Sensors.TryGetValue(sensor, out var id) ? id : null;

    public static IReadOnlyCollection<string> AllOrganIds => Organs.Values;
    public static IReadOnlyCollection<string> AllActionIds => Actions.Values;
    public static IReadOnlyCollection<string> AllSensorIds => Sensors.Values;
}
