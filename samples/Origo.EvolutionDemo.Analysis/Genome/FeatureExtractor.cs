using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Origo.EvolutionDemo.Analysis.IO;
using Origo.EvolutionDemo.Logic;
using Origo.EvolutionDemo.Logic.V4;

namespace Origo.EvolutionDemo.Analysis.Genome;

/// <summary>One creature row in the analysis feature matrix.</summary>
public sealed record FeatureRow(
    string SavePath,
    double SaveTime,
    string Name,
    int Lineage,
    int Generation,
    IReadOnlyDictionary<string, double> Values,
    IReadOnlySet<string> GeneSignatures);

/// <summary>One creature that could not be turned into a feature row.</summary>
/// <param name="SavePath">Save that contains the creature.</param>
/// <param name="Name">Entity name reported by the save.</param>
/// <param name="Reason">Decode or phenotype-compile failure summary.</param>
public sealed record SkippedCreature(string SavePath, string Name, string Reason);

/// <summary>
///     Result of feature extraction: the analyzed rows plus the accounting needed
///     to report sampling and skipped creatures. <see cref="TotalCreatures" /> is
///     the raw creature-entity count of the saves (including reader-level skips),
///     <see cref="ConsideredCreatures" /> is the number of parsed creatures
///     attempted after <c>--max</c> sampling, and <see cref="SkippedCreatures" />
///     counts reader-level skips (missing genome) plus decode/phenotype failures.
///     The caller decides whether skips are fatal (<c>--allow-skips</c>).
/// </summary>
public sealed record FeatureExtractionResult(
    IReadOnlyList<FeatureRow> Rows,
    int TotalCreatures,
    int ConsideredCreatures,
    IReadOnlyList<SkippedCreature> SkippedCreatures);

/// <summary>
///     Extracts structural, phenotypic, and behavioral features from v7 save
///     creatures. Every <see cref="OrganKindV4" /> gets an <c>organ.*</c> count
///     (zero when absent) and every <see cref="LifeTraitKindV4" /> gets a
///     <c>trait.*</c> value taken from the effective runtime phenotype
///     (<see cref="DemoPhenotypeAdapter.ToBodyPhenotype(PhenotypeV4)" />), so no
///     feature is NaN by construction. Non-finite values are a hard error.
/// </summary>
public static class FeatureExtractor
{
    /// <summary>Extracts features from every creature of the supplied saves.</summary>
    /// <param name="saves">Parsed save snapshots.</param>
    /// <param name="maxCreatures">Per-save row cap; zero or negative analyzes every creature.</param>
    /// <param name="seed">Base seed for the deterministic per-save row sample.</param>
    public static FeatureExtractionResult Extract(
        IReadOnlyList<SaveSnapshot> saves,
        int maxCreatures,
        int seed)
    {
        ArgumentNullException.ThrowIfNull(saves);
        var rows = new List<FeatureRow>();
        var skips = new List<SkippedCreature>();
        var total = 0;
        var considered = 0;
        for (var saveIndex = 0; saveIndex < saves.Count; saveIndex++)
        {
            var save = saves[saveIndex];
            total += save.RawCreatureCount;
            foreach (var skip in save.SkippedCreatures)
                skips.Add(new SkippedCreature(save.Path, skip.Name, skip.Reason));
            var creatures = save.Creatures;
            if (maxCreatures > 0 && creatures.Count > maxCreatures)
            {
                // Per-save derived seed: adding or resizing another save does not
                // change which rows of this save are sampled.
                var random = new Random(seed + (saveIndex * 7919));
                creatures = [.. creatures.OrderBy(_ => random.Next()).Take(maxCreatures)];
            }

            considered += creatures.Count;
            foreach (var creature in creatures)
            {
                try
                {
                    rows.Add(ExtractOne(save, creature));
                }
                catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException
                    or ArgumentException or FormatException or IOException)
                {
                    skips.Add(new SkippedCreature(
                        save.Path, creature.Name, $"{ex.GetType().Name}: {ex.Message}"));
                }
            }
        }

        return new FeatureExtractionResult(rows, total, considered, skips);
    }

    private static FeatureRow ExtractOne(SaveSnapshot save, CreatureRecord creature)
    {
        var genome = GenomeV4Codec.FromSerialized(creature.Genome);
        var phenotype = GenomeV4Compiler.Compile(genome, ExpressionContextV4.Neutral);
        var body = DemoPhenotypeAdapter.ToBodyPhenotype(phenotype);
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
            ["behavior.softMeals"] = creature.SoftMeals,
            ["behavior.toughMeals"] = creature.ToughMeals,
            ["behavior.toxicMeals"] = creature.ToxicMeals,
            ["behavior.generation"] = creature.Generation,
        };
        var totalMeals = creature.SoftMeals + creature.ToughMeals + creature.ToxicMeals;
        values["diet.totalMeals"] = totalMeals;
        var softShare = totalMeals == 0 ? 0d : (double)creature.SoftMeals / totalMeals;
        var toughShare = totalMeals == 0 ? 0d : (double)creature.ToughMeals / totalMeals;
        var toxicShare = totalMeals == 0 ? 0d : (double)creature.ToxicMeals / totalMeals;
        values["diet.softShare"] = softShare;
        values["diet.toughShare"] = toughShare;
        values["diet.toxicShare"] = toxicShare;
        values["diet.corpseShare"] = totalMeals == 0 ? 0d : (double)creature.CorpseMeals / totalMeals;
        var dominance = System.Math.Max(softShare, System.Math.Max(toughShare, toxicShare));
        values["diet.dominance"] = dominance;
        values["diet.entropy"] = totalMeals == 0 ? 0d : DietEntropy(softShare, toughShare, toxicShare);

        foreach (var organKind in Enum.GetValues<OrganKindV4>())
            values[$"organ.{organKind}"] =
                phenotype.Body.Organs.Count(organ => organ.Kind == organKind);
        foreach (var traitKind in Enum.GetValues<LifeTraitKindV4>())
            values[$"trait.{traitKind}"] = EffectiveTrait(traitKind, body);
        foreach (var (feature, value) in values)
            if (!double.IsFinite(value))
                throw new InvalidDataException(
                    $"Feature '{feature}' is non-finite for creature '{creature.Name}' in save '{save.Path}'.");
        var signatures = genome.Genes
            .Select(DescribeGene)
            .ToHashSet(StringComparer.Ordinal);
        return new FeatureRow(save.Path, save.Time, creature.Name, creature.Lineage,
            creature.Generation, values, signatures);
    }

    private static float EffectiveTrait(LifeTraitKindV4 kind, BodyPhenotype body) => kind switch
    {
        LifeTraitKindV4.Size => body.SizeScale,
        LifeTraitKindV4.Speed => body.MoveSpeed,
        LifeTraitKindV4.TurnSpeed => body.TurnSpeed,
        LifeTraitKindV4.SenseRange => body.SenseRange,
        LifeTraitKindV4.Metabolism => body.BasalMetabolism,
        LifeTraitKindV4.MaxAge => body.MaxAge,
        LifeTraitKindV4.SpikeRange => body.SpikeRadius,
        _ => throw new InvalidOperationException(
            $"No effective runtime mapping for life trait '{kind}'."),
    };

    private static double DietEntropy(double soft, double tough, double toxic)
    {
        const double log3 = 1.0986122886681098d;
        var entropy = 0d;
        foreach (var share in new[] { soft, tough, toxic })
        {
            if (share > 0d)
                entropy -= share * System.Math.Log(share);
        }

        return entropy / log3;
    }

    /// <summary>Human-readable stable gene signature for enrichment reports.</summary>
    public static string DescribeGene(GeneV4 gene)
    {
        ArgumentNullException.ThrowIfNull(gene);
        return gene.Kind switch
        {
            GeneKind.BodySegment => DescribeBody(gene),
            GeneKind.Organ => DescribeOrgan(gene),
            GeneKind.Neuron => DescribeNeuron(gene),
            GeneKind.Synapse => DescribeSynapse(gene),
            GeneKind.Reflex => DescribeReflex(gene),
            GeneKind.LifeHistory => DescribeLife(gene),
            _ => string.Create(CultureInfo.InvariantCulture, $"{gene.Kind}:{Convert.ToHexString(gene.Payload)}"),
        };
    }

    private static string DescribeBody(GeneV4 gene)
    {
        var (length, width) = GenePayloadV4.ReadBodySegment(gene.Payload);
        return string.Create(CultureInfo.InvariantCulture, $"BodySegment(length={length:0.00},width={width:0.00})");
    }

    private static string DescribeOrgan(GeneV4 gene)
    {
        var (kind, size) = GenePayloadV4.ReadOrgan(gene.Payload);
        return string.Create(CultureInfo.InvariantCulture, $"Organ({kind},size={size:0.00})");
    }

    private static string DescribeNeuron(GeneV4 gene)
    {
        var (kind, interfaceId, activation, bias) = GenePayloadV4.ReadNeuron(gene.Payload);
        var interfaceName = kind switch
        {
            NeuronKindV4.Sensor => ((SensorKindV4)interfaceId).ToString(),
            NeuronKindV4.Output => ((ActionKindV4)interfaceId).ToString(),
            _ => string.Create(CultureInfo.InvariantCulture, $"hidden:{interfaceId}"),
        };
        return string.Create(CultureInfo.InvariantCulture,
            $"Neuron({kind},{interfaceName},{activation},bias={bias:0.00})");
    }

    private static string DescribeSynapse(GeneV4 gene)
    {
        var (from, to, weight) = GenePayloadV4.ReadSynapse(gene.Payload);
        return string.Create(CultureInfo.InvariantCulture, $"Synapse({from}->{to},weight={weight:0.00})");
    }

    private static string DescribeReflex(GeneV4 gene)
    {
        var (sensor, action, gain) = GenePayloadV4.ReadReflex(gene.Payload);
        return string.Create(CultureInfo.InvariantCulture, $"Reflex({sensor}->{action},gain={gain:0.00})");
    }

    private static string DescribeLife(GeneV4 gene)
    {
        var (trait, value) = GenePayloadV4.ReadLifeHistory(gene.Payload);
        return string.Create(CultureInfo.InvariantCulture, $"Life({trait}={value:0.00})");
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
