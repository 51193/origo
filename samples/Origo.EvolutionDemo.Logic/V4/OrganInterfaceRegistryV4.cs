using System;
using System.Collections.Generic;

namespace Origo.EvolutionDemo.Logic.V4;

/// <summary>Maps an organ kind to the sensor/action ports it exposes.</summary>
public static class OrganInterfaceRegistryV4
{
    private static readonly Dictionary<OrganKindV4, List<ushort>> SensorPorts = [];
    private static readonly Dictionary<OrganKindV4, List<(ushort Port, int Priority)>> ActionPorts = [];

    static OrganInterfaceRegistryV4()
    {
        RegisterSensor(OrganKindV4.Vision, SensorKindV4.FoodAhead);
        RegisterSensor(OrganKindV4.Vision, SensorKindV4.FoodLeft);
        RegisterSensor(OrganKindV4.Vision, SensorKindV4.FoodRight);
        RegisterSensor(OrganKindV4.CreatureSense, SensorKindV4.CreatureAhead);
        RegisterSensor(OrganKindV4.Rhythm, SensorKindV4.Sin);
        RegisterSensor(OrganKindV4.Rhythm, SensorKindV4.Cos);
        RegisterSensor(OrganKindV4.CarrionSense, SensorKindV4.CorpseAhead);

        RegisterAction(OrganKindV4.SpikeOrgan, ActionKindV4.Aim, 20);
        RegisterAction(OrganKindV4.SpikeOrgan, ActionKindV4.Spike, 40);
        RegisterAction(OrganKindV4.Sprint, ActionKindV4.Sprint, 0);
    }

    /// <summary>Registers a sensor exposed by an organ kind.</summary>
    public static void RegisterSensor(OrganKindV4 organ, SensorKindV4 sensor)
    {
        if (!SensorPorts.TryGetValue(organ, out var ports))
            SensorPorts[organ] = ports = [];
        var id = (ushort)sensor;
        if (!ports.Contains(id))
            ports.Add(id);
    }

    /// <summary>Registers an action exposed by an organ kind; priority controls effect order.</summary>
    public static void RegisterAction(OrganKindV4 organ, ActionKindV4 action, int priority)
    {
        if (!ActionPorts.TryGetValue(organ, out var ports))
            ActionPorts[organ] = ports = [];
        var id = (ushort)action;
        if (ports.Exists(entry => entry.Port == id))
            return;
        ports.Add((id, priority));
    }

    /// <summary>Builds the ordered sensor ports for the expressed organs.</summary>
    public static List<ushort> BuildSensorPorts(IReadOnlyList<OrganInstanceV4> organs)
    {
        ArgumentNullException.ThrowIfNull(organs);
        var result = new List<ushort> { (ushort)SensorKindV4.WallAhead, (ushort)SensorKindV4.Energy };
        var seen = new HashSet<ushort>();
        foreach (var organ in organs)
        {
            if (!SensorPorts.TryGetValue(organ.Kind, out var ports))
                continue;
            foreach (var port in ports)
                if (seen.Add(port))
                    result.Add(port);
        }

        return result;
    }

    /// <summary>Builds the ordered action ports for the expressed organs.</summary>
    public static List<ushort> BuildActionPorts(IReadOnlyList<OrganInstanceV4> organs)
    {
        ArgumentNullException.ThrowIfNull(organs);
        var ordered = new List<(ushort Port, int Priority)>
        {
            ((ushort)ActionKindV4.Turn, 10),
            ((ushort)ActionKindV4.Move, 30),
        };
        var seen = new HashSet<ushort>();
        foreach (var organ in organs)
        {
            if (!ActionPorts.TryGetValue(organ.Kind, out var ports))
                continue;
            foreach (var port in ports)
                if (seen.Add(port.Port))
                    ordered.Add(port);
        }

        ordered.Sort(static (a, b) => a.Priority.CompareTo(b.Priority));
        return ordered.ConvertAll(static entry => entry.Port);
    }
}
