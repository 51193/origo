using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Origo.EvolutionDemo.Logic.V4;

/// <summary>A sensor or action port row for the generic inspector.</summary>
public sealed record InspectorPortV4(ushort Id, string Label);

/// <summary>An expressed organ row for the generic inspector.</summary>
public sealed record InspectorOrganV4(uint GeneId, string Label, int CopyIndex, float Size);

/// <summary>Generic, presentation-independent phenotype inspection model.</summary>
public sealed record PhenotypeInspectionV4(
    IReadOnlyList<InspectorOrganV4> Organs,
    IReadOnlyList<InspectorPortV4> Sensors,
    IReadOnlyList<InspectorPortV4> Actions,
    int NeuralNodes,
    int NeuralSynapses,
    float MaintenancePerSecond)
{
    /// <summary>One-line or multiline diagnostic summary used by the demo UI.</summary>
    public string Describe()
    {
        var organText = Organs.Count == 0
            ? "none"
            : string.Join(", ", Organs.Select(static organ =>
                $"{organ.Label} #{organ.CopyIndex} ({organ.Size:0.00})"));
        var sensorText = Sensors.Count == 0
            ? "none"
            : string.Join(", ", Sensors.Select(static port => port.Label));
        var actionText = Actions.Count == 0
            ? "none"
            : string.Join(", ", Actions.Select(static port => port.Label));
        return string.Create(CultureInfo.InvariantCulture,
            $"Organs: {organText}\n" +
            $"Sensors: {sensorText}\n" +
            $"Actions: {actionText}\n" +
            $"Neural: {NeuralNodes} nodes / {NeuralSynapses} synapses\n" +
            $"Maintenance: {MaintenancePerSecond:0.000}/s");
    }
}

/// <summary>Builds the generic inspection model from a compiled V4 phenotype.</summary>
public static class PhenotypeInspectorV4
{
    public static PhenotypeInspectionV4 Inspect(PhenotypeV4 phenotype)
    {
        ArgumentNullException.ThrowIfNull(phenotype);
        var organs = phenotype.Body.Organs
            .Select(static organ => new InspectorOrganV4(
                organ.GeneId,
                organ.Kind.ToString(),
                organ.CopyIndex,
                organ.Size))
            .ToList();
        var sensors = phenotype.SensorPorts
            .Select(static id => new InspectorPortV4(
                id,
                InterfaceRegistryV4.SensorDefinitions.TryGetValue(id, out var definition)
                    ? definition.DisplayName
                    : id.ToString(CultureInfo.InvariantCulture)))
            .ToList();
        var actions = phenotype.ActionPorts
            .Select(static id => new InspectorPortV4(
                id,
                InterfaceRegistryV4.ActionDefinitions.TryGetValue(id, out var definition)
                    ? definition.DisplayName
                    : id.ToString(CultureInfo.InvariantCulture)))
            .ToList();
        return new PhenotypeInspectionV4(
            organs,
            sensors,
            actions,
            phenotype.Brain.Nodes.Count,
            phenotype.Brain.Edges.Count,
            phenotype.Energy.MaintenancePerSecond);
    }
}

/// <summary>Formats expressed sensor values with their stable interface labels.</summary>
public static class SensorDisplayV4
{
    /// <summary>
    ///     Formats only the expressed ports, using the registry label and the
    ///     value addressed by the stable sensor-interface id.
    /// </summary>
    public static string Format(IReadOnlyList<ushort> ports, ReadOnlySpan<float> values)
    {
        ArgumentNullException.ThrowIfNull(ports);
        var parts = new string[ports.Count];
        for (var index = 0; index < ports.Count; index++)
        {
            var port = ports[index];
            var label = InterfaceRegistryV4.SensorDefinitions.TryGetValue(port, out var definition)
                ? definition.DisplayName
                : port.ToString(CultureInfo.InvariantCulture);
            parts[index] = string.Create(
                CultureInfo.InvariantCulture,
                $"{label} {(port < values.Length ? values[port] : 0f):0.00}");
        }

        return string.Join("  ", parts);
    }
}
