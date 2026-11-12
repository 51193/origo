using System;
using System.Collections.Generic;
using System.Linq;

namespace Origo.EvolutionDemo.Logic.V4;

/// <summary>
///     Maps a compiled V4 phenotype onto the legacy demo's presentation-facing
///     BodyPhenotype for rendering and physics. The runtime sensors/actions are
///     now supplied by the V4 port registries, not this adapter.
/// </summary>
public static class DemoPhenotypeAdapter
{
    /// <summary>Compiles a V4 genome and adapts it to the demo's current phenotype.</summary>
    public static BodyPhenotype ToBodyPhenotype(GenomeV4 genome)
    {
        var phenotype = GenomeV4Compiler.Compile(genome, ExpressionContextV4.Neutral);
        return ToBodyPhenotype(phenotype);
    }

    /// <summary>Adapts a compiled V4 body to the demo's BodyPhenotype.</summary>
    public static BodyPhenotype ToBodyPhenotype(PhenotypeV4 phenotype)
    {
        ArgumentNullException.ThrowIfNull(phenotype);
        var organs = phenotype.Body.Organs;
        var size = TraitOrDefault(phenotype, LifeTraitKindV4.Size, 1f);
        var bodyRadius = Tuning.BodyRadiusBase * size;
        var spikeBattery = organs.Where(static organ => organ.Kind == OrganKindV4.SpikeBattery).ToList();
        var spikeCount = Math.Clamp(spikeBattery.Count, 1, 8);
        return new BodyPhenotype(
            size,
            bodyRadius,
            bodyRadius + Tuning.FoodPickupPadding,
            Tuning.BodyMaxEnergyBase + (Tuning.BodyMaxEnergyPerSize * size * size),
            TraitOrDefault(phenotype, LifeTraitKindV4.Speed, Tuning.CreatureSpeed),
            TraitOrDefault(phenotype, LifeTraitKindV4.TurnSpeed, Tuning.TurnSpeed),
            TraitOrDefault(phenotype, LifeTraitKindV4.SenseRange, Tuning.SenseRange),
            TraitOrDefault(phenotype, LifeTraitKindV4.SpikeRange, Tuning.SpikeRadius),
            TraitOrDefault(phenotype, LifeTraitKindV4.Metabolism, 1f),
            TraitOrDefault(phenotype, LifeTraitKindV4.MaxAge, Tuning.MaxAgeSeconds),
            DemoOrganMaskBuilder.FromOrgans(organs),
            spikeCount,
            MathF.PI / 4f,
            0f);
    }

    private static float TraitOrDefault(PhenotypeV4 phenotype, LifeTraitKindV4 trait, float fallback) =>
        phenotype.Body.LifeTraits.TryGetValue(trait, out var value) ? value : fallback;
}
