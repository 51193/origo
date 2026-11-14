using System.IO;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Origo.EvolutionDemo.Analysis.IO;
using Origo.EvolutionDemo.Logic.V4;

namespace Origo.EvolutionDemo.Analysis.Genome;

/// <summary>One creature row in the analysis feature matrix.</summary>
public sealed record FeatureRow(
    string SavePath,
    double SaveTime,
    string Name,
    int Lineage,
    int Generation,
    IReadOnlyDictionary<string, double> Values);

/// <summary>Extracts structural, phenotypic, and behavioral features from v6 save creatures.</summary>
public static class FeatureExtractor
{
    public static IReadOnlyList<FeatureRow> Extract(IReadOnlyList<SaveSnapshot> saves, int maxCreatures, int seed)
    {
        ArgumentNullException.ThrowIfNull(saves);
        var rows = new List<FeatureRow>();
        var random = new Random(seed);
        foreach (var save in saves)
        {
            var creatures = save.Creatures;
            if (maxCreatures > 0 && creatures.Count > maxCreatures)
                creatures = [.. creatures.OrderBy(_ => random.Next()).Take(maxCreatures)];
            foreach (var creature in creatures)
            {
                var row = ExtractOne(save, creature);
                if (row is not null)
                    rows.Add(row);
            }
        }

        return rows;
    }

    private static FeatureRow? ExtractOne(SaveSnapshot save, CreatureRecord creature)
    {
        try
        {
            var genome = GenomeV4Codec.FromHex(creature.Genome);
            var phenotype = GenomeV4Compiler.Compile(genome, ExpressionContextV4.Neutral);
            var brain = GenomeV4Compiler.CompileBrain(phenotype);
            var sensors = new float[64];
            var actions = new float[64];
            brain.Evaluate(sensors, actions);
            var values = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["gene.count"] = genome.Genes.Count,
                ["gene.body"] = genome.Genes.Count(static gene => gene.Kind == GeneKind.BodySegment),
                ["gene.organ"] = genome.Genes.Count(static gene => gene.Kind == GeneKind.Organ),
                ["gene.neuron"] = genome.Genes.Count(static gene => gene.Kind == GeneKind.Neuron),
                ["gene.synapse"] = genome.Genes.Count(static gene => gene.Kind == GeneKind.Synapse),
                ["gene.reflex"] = genome.Genes.Count(static gene => gene.Kind == GeneKind.Reflex),
                ["gene.life"] = genome.Genes.Count(static gene => gene.Kind == GeneKind.LifeHistory),
                ["gene.regulation"] = genome.Genes.Sum(static gene => gene.Regulators.Count),
                ["gene.copies"] = genome.Genes.Sum(static gene => gene.CopyCount),
                ["body.segments"] = phenotype.Body.Segments.Count,
                ["body.organs"] = phenotype.Body.Organs.Count,
                ["neural.nodes"] = phenotype.Brain.Nodes.Count,
                ["neural.edges"] = phenotype.Brain.Edges.Count,
                ["neural.sensor"] = phenotype.Brain.Nodes.Count(static node => node.Kind == NeuronKindV4.Sensor),
                ["neural.inter"] = phenotype.Brain.Nodes.Count(static node => node.Kind == NeuronKindV4.Interneuron),
                ["neural.output"] = phenotype.Brain.Nodes.Count(static node => node.Kind == NeuronKindV4.Output),
                ["neural.linear"] = phenotype.Brain.Nodes.Count(static node => node.Activation == ActivationKindV4.Linear),
                ["neural.tanh"] = phenotype.Brain.Nodes.Count(static node => node.Activation == ActivationKindV4.Tanh),
                ["neural.sigmoid"] = phenotype.Brain.Nodes.Count(static node => node.Activation == ActivationKindV4.Sigmoid),
                ["neural.relu"] = phenotype.Brain.Nodes.Count(static node => node.Activation == ActivationKindV4.Relu),
                ["neural.bias.mean"] = phenotype.Brain.Nodes.Count == 0 ? 0 : phenotype.Brain.Nodes.Average(static node => node.Bias),
                ["neural.bias.absmax"] = phenotype.Brain.Nodes.Count == 0 ? 0 : phenotype.Brain.Nodes.Max(static node => System.Math.Abs(node.Bias)),
                ["neural.weight.mean"] = phenotype.Brain.Edges.Count == 0 ? 0 : phenotype.Brain.Edges.Average(static edge => edge.Weight),
                ["neural.weight.absmax"] = phenotype.Brain.Edges.Count == 0 ? 0 : phenotype.Brain.Edges.Max(static edge => System.Math.Abs(edge.Weight)),
                ["neural.weight.std"] = StandardDeviation(phenotype.Brain.Edges.Select(static edge => (double)edge.Weight)),
                ["ports.sensors"] = phenotype.SensorPorts.Count,
                ["ports.actions"] = phenotype.ActionPorts.Count,
                ["energy.body"] = phenotype.Energy.BodyCost,
                ["energy.brain"] = phenotype.Energy.BrainCost,
                ["energy.genome"] = phenotype.Energy.GenomeCost,
                ["energy.maintenance"] = phenotype.Energy.MaintenancePerSecond,
                ["neutral.move"] = actions[(int)ActionKindV4.Move],
                ["neutral.turn"] = actions[(int)ActionKindV4.Turn],
                ["neutral.spike"] = actions[(int)ActionKindV4.Spike],
                ["neutral.aim"] = actions[(int)ActionKindV4.Aim],
                ["neutral.sprint"] = actions[(int)ActionKindV4.Sprint],
                ["body.radius"] = creature.BodyRadius,
                ["body.moveSpeed"] = creature.MoveSpeed,
                ["body.turnSpeed"] = creature.TurnSpeed,
                ["body.senseRange"] = creature.SenseRange,
                ["body.spikeRadius"] = creature.SpikeRadius,
                ["body.basalMetabolism"] = creature.BasalMetabolism,
                ["body.maxAge"] = creature.MaxAge,
                ["body.spikeCount"] = creature.SpikeCount,
                ["behavior.age"] = creature.Age,
                ["behavior.energyRatio"] = creature.Energy / System.Math.Max(1d, creature.MaxEnergy),
                ["behavior.drive"] = creature.Drive,
                ["behavior.turn"] = creature.Turn,
                ["behavior.spikeDrive"] = creature.SpikeDrive,
                ["behavior.aim"] = creature.Aim,
                ["behavior.sprintDrive"] = creature.SprintDrive,
                ["behavior.displacement"] = System.Math.Sqrt(
                    ((creature.X - creature.PrevX) * (creature.X - creature.PrevX))
                    + ((creature.Y - creature.PrevY) * (creature.Y - creature.PrevY))),
                ["behavior.children"] = creature.Children,
                ["behavior.kills"] = creature.Kills,
                ["behavior.corpseMeals"] = creature.CorpseMeals,
                ["behavior.generation"] = creature.Generation,
            };
            foreach (var organ in phenotype.Body.Organs.GroupBy(static organ => organ.Kind))
                values[$"organ.{organ.Key}"] = organ.Count();
            foreach (var trait in phenotype.Body.LifeTraits)
                values[$"trait.{trait.Key}"] = trait.Value;
            return new FeatureRow(save.Path, save.Time, creature.Name, creature.Lineage,
                creature.Generation, values);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException
            or ArgumentException or FormatException)
        {
            return null;
        }
    }

    private static double StandardDeviation(IEnumerable<double> values)
    {
        var list = values.ToArray();
        if (list.Length < 2)
            return 0;
        var mean = list.Average();
        return System.Math.Sqrt(list.Sum(value => (value - mean) * (value - mean)) / list.Length);
    }
}
