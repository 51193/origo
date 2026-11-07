using System;

namespace Origo.EvolutionDemo.Logic;

/// <summary>
///     Combined body and neural genomes. The body is variable-length and its
///     codon table supplies the phenotype and active brain topology. The neural
///     genome is a fixed maximum latent lattice and always remains structurally
///     valid when the body changes.
/// </summary>
public sealed record CreatureGenome(string Body, string Brain)
{
    /// <summary>Decodes the body phenotype for this genome.</summary>
    public BodyPhenotype Phenotype => BodyGenome.Decode(Body);

    /// <summary>Creates a calibrated founder with the default body and a mutated founder brain.</summary>
    public static CreatureGenome RandomFounder(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return new CreatureGenome(BodyGenome.DefaultTemplate, NeuralGenome.RandomFounder(random));
    }

    /// <summary>Mutates both genomes and returns the combined number of changes.</summary>
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
        var body = BodyGenome.Mutate(
            parent.Body,
            bodyStepRate,
            bodyLengthRate,
            frameshiftRate,
            random,
            out var bodyChanges);
        var brain = NeuralGenome.Mutate(parent.Brain, brainStepRate, random, out var brainChanges);
        changes = bodyChanges + brainChanges;
        return new CreatureGenome(body, brain);
    }
}
