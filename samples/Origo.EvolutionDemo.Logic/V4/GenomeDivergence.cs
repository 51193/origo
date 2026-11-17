using System;
using System.Collections.Generic;

namespace Origo.EvolutionDemo.Logic.V4;

/// <summary>
///     Structural comparison of two V4 genomes. The demo uses this to decide
///     whether a child has diverged enough for a new lineage, instead of
///     counting mutation attempts (which include no-ops and say nothing about
///     the actual genome difference).
/// </summary>
public static class GenomeDivergence
{
    /// <summary>Counts genes that were added, removed, or changed between the genomes.</summary>
    /// <exception cref="ArgumentNullException">Thrown when a genome is null.</exception>
    public static int CountChangedGenes(GenomeV4 parent, GenomeV4 child)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(child);
        var parentGenes = new Dictionary<uint, GeneV4>(parent.Genes.Count);
        foreach (var gene in parent.Genes)
            parentGenes[gene.Id] = gene;

        var changed = 0;
        var seen = new HashSet<uint>(child.Genes.Count);
        foreach (var gene in child.Genes)
        {
            seen.Add(gene.Id);
            if (!parentGenes.TryGetValue(gene.Id, out var previous))
            {
                changed++;
                continue;
            }

            if (!Equivalent(previous, gene))
                changed++;
        }

        foreach (var gene in parent.Genes)
        {
            if (!seen.Contains(gene.Id))
                changed++;
        }

        return changed;
    }

    private static bool Equivalent(GeneV4 left, GeneV4 right) =>
        left.Kind == right.Kind
        && left.CopyCount == right.CopyCount
        && left.Expression.Equals(right.Expression)
        && left.Payload.AsSpan().SequenceEqual(right.Payload)
        && RegulatorsEqual(left.Regulators, right.Regulators);

    private static bool RegulatorsEqual(List<RegulationV4> left, List<RegulationV4> right)
    {
        if (left.Count != right.Count)
            return false;
        for (var index = 0; index < left.Count; index++)
        {
            if (left[index].Condition != right[index].Condition
                || !left[index].Threshold.Equals(right[index].Threshold))
                return false;
        }

        return true;
    }
}
