using System;
using Origo.EvolutionDemo.Logic.V4;

namespace Origo.EvolutionDemo.Logic;

/// <summary>
///     Combined V4 genome. The body and brain are expressed from one stable-ID
///     gene graph; the serialized hex value is the save/display representation.
/// </summary>
public sealed class CreatureGenome
{
    public CreatureGenome(GenomeV4 genome)
    {
        ArgumentNullException.ThrowIfNull(genome);
        Genome = genome;
    }

    /// <summary>The canonical V4 genome.</summary>
    public GenomeV4 Genome { get; }

    /// <summary>Canonical hex representation used for display and appearance derivation.</summary>
    public string Body => GenomeV4Codec.ToHex(Genome);

    /// <summary>Compact base64 representation stored in SND entities and saves.</summary>
    public string SerializedBody => GenomeV4Codec.ToBase64(Genome);

    /// <summary>Number of genes, used for reproductive cost and display.</summary>
    public int GeneCount => Genome.Genes.Count;

    /// <summary>Compiled demo-facing phenotype.</summary>
    public BodyPhenotype Phenotype => DemoPhenotypeAdapter.ToBodyPhenotype(Genome);

    /// <summary>Creates a V4 founder with a variable body and neural graph.</summary>
    public static CreatureGenome RandomFounder(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return new CreatureGenome(GenomeV4Factory.CreateDemoFounder(random));
    }

    /// <summary>Decodes a serialized V4 genome.</summary>
    public static CreatureGenome FromSerialized(string serialized)
    {
        ArgumentNullException.ThrowIfNull(serialized);
        return new CreatureGenome(GenomeV4Codec.FromSerialized(serialized));
    }

    /// <summary>Attempts to decode a serialized V4 genome.</summary>
    public static bool TryDecode(string? serialized, out CreatureGenome? genome)
    {
        genome = null;
        if (string.IsNullOrWhiteSpace(serialized))
            return false;
        try
        {
            genome = FromSerialized(serialized);
            _ = genome.Phenotype;
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException
            or System.IO.InvalidDataException or System.IO.IOException)
        {
            genome = null;
            return false;
        }
    }

    /// <summary>
    ///     Mutates the V4 genome with the operator mix and step count of
    ///     <paramref name="profile" />.
    ///     <paramref name="changes" /> is the number of genes that actually
    ///     differ between the parent and child, so a no-op mutation step and a
    ///     mutation attempt measure the same genome divergence.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when a parameter is null.</exception>
    public static CreatureGenome Mutate(
        CreatureGenome parent,
        MutationProfile profile,
        Random random,
        out int changes)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(random);
        var working = parent.Genome.Clone();
        for (var step = 0; step < profile.Steps; step++)
        {
            var next = GenomeV4Mutator.Mutate(working, random, profile.Settings);
            if (string.Equals(new CreatureGenome(next).Body,
                    new CreatureGenome(working).Body, StringComparison.Ordinal))
                continue;
            working = next;
        }

        ClampLifeTraits(working, DemoSettings.Current);
        changes = GenomeDivergence.CountChangedGenes(parent.Genome, working);
        return new CreatureGenome(working);
    }

    private static void ClampLifeTraits(GenomeV4 genome, DemoSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        foreach (var gene in genome.Genes)
        {
            if (gene.Kind != GeneKind.LifeHistory)
                continue;
            var (trait, value) = GenePayloadV4.ReadLifeHistory(gene.Payload);
            var (minimum, maximum) = TraitRange(trait, settings);
            if (minimum > maximum)
                (minimum, maximum) = (maximum, minimum);
            var clamped = Math.Clamp(value, minimum, maximum);
            if (clamped.Equals(value))
                continue;
            BitConverter.GetBytes(clamped).CopyTo(gene.Payload, sizeof(ushort));
        }
    }

    private static (float Minimum, float Maximum) TraitRange(LifeTraitKindV4 trait, DemoSettings settings) =>
        trait switch
        {
            LifeTraitKindV4.Size => (settings.BodySizeMin, settings.BodySizeMax),
            LifeTraitKindV4.Speed => (settings.BodySpeedMin, settings.BodySpeedMax),
            LifeTraitKindV4.TurnSpeed => (settings.BodyTurnMin, settings.BodyTurnMax),
            LifeTraitKindV4.SenseRange => (settings.BodySenseMin, settings.BodySenseMax),
            LifeTraitKindV4.SpikeRange => (settings.BodySpikeMin, settings.BodySpikeMax),
            LifeTraitKindV4.Metabolism => (settings.BodyMetabolismMin, settings.BodyMetabolismMax),
            LifeTraitKindV4.MaxAge => (settings.BodyAgeMin, settings.BodyAgeMax),
            _ => throw new InvalidOperationException($"No configured range for life trait '{trait}'."),
        };
}
