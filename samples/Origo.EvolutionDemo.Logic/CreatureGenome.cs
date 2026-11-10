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

    /// <summary>Canonical hex representation stored in SND entities.</summary>
    public string Body => GenomeV4Codec.ToHex(Genome);

    /// <summary>Brain is part of the same V4 genome; retained as an empty display field.</summary>
    public string Brain => Genome.Genes.Count > 0 ? string.Empty : string.Empty;

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
        return new CreatureGenome(GenomeV4Codec.FromHex(serialized));
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
            or System.IO.InvalidDataException)
        {
            genome = null;
            return false;
        }
    }

    /// <summary>
    ///     Mutates the V4 genome. Rates are kept in the signature for the demo's
    ///     existing tuning surface; V4 mutation uses its typed operator distribution.
    /// </summary>
    public static CreatureGenome Mutate(
        CreatureGenome parent,
        double bodyStepRate,
        double bodyLengthRate,
        double frameshiftRate,
        double brainStepRate,
        Random random,
        out int changes)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(random);
        var working = parent.Genome;
        var steps = 1 + random.Next(3);
        var applied = 0;
        for (var step = 0; step < steps; step++)
        {
            var next = GenomeV4Mutator.Mutate(working, random);
            if (string.Equals(new CreatureGenome(next).Body,
                    new CreatureGenome(working).Body, StringComparison.Ordinal))
                continue;
            working = next;
            applied++;
        }

        changes = applied;
        _ = bodyStepRate;
        _ = bodyLengthRate;
        _ = frameshiftRate;
        _ = brainStepRate;
        return new CreatureGenome(working);
    }
}
