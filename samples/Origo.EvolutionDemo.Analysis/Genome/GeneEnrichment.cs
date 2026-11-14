using System;
using System.Collections.Generic;
using System.Linq;

namespace Origo.EvolutionDemo.Analysis.Genome;

/// <summary>A gene-signature enrichment result for one cluster.</summary>
public sealed record EnrichmentResult(
    int Cluster,
    string Signature,
    int ClusterWith,
    int ClusterTotal,
    int RestWith,
    int RestTotal,
    double OddsRatio,
    double PValue,
    double QValue);

/// <summary>Fisher exact tests for gene-signature presence in clusters.</summary>
public static class GeneEnrichment
{
    public static IReadOnlyList<EnrichmentResult> Analyze(
        IReadOnlyList<FeatureRow> rows,
        int[] labels,
        int cluster)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(labels);
        var clusterIndexes = Enumerable.Range(0, rows.Count).Where(index => labels[index] == cluster).ToArray();
        var restIndexes = Enumerable.Range(0, rows.Count).Where(index => labels[index] != cluster).ToArray();
        if (clusterIndexes.Length == 0 || restIndexes.Length == 0)
            return [];
        var signatures = rows.SelectMany(static row => row.GeneSignatures)
            .Distinct(StringComparer.Ordinal)
            .Select(signature => new
            {
                Signature = signature,
                A = clusterIndexes.Count(index => rows[index].GeneSignatures.Contains(signature)),
                B = clusterIndexes.Length - clusterIndexes.Count(index => rows[index].GeneSignatures.Contains(signature)),
                C = restIndexes.Count(index => rows[index].GeneSignatures.Contains(signature)),
                D = restIndexes.Length - restIndexes.Count(index => rows[index].GeneSignatures.Contains(signature)),
            })
            .Where(static item => item.A + item.C >= 3)
            .Select(item => new
            {
                item.Signature,
                item.A,
                item.B,
                item.C,
                item.D,
                P = FisherExact(item.A, item.B, item.C, item.D),
                Odds = ((double)(item.A + 0.5) * (item.D + 0.5)) / ((item.B + 0.5) * (item.C + 0.5)),
            })
            .OrderBy(static item => item.P)
            .ThenByDescending(static item => System.Math.Abs(item.Odds - 1))
            .Take(40)
            .ToArray();
        var results = new List<EnrichmentResult>(signatures.Length);
        double previousQ = 1;
        for (var i = 0; i < signatures.Length; i++)
        {
            var item = signatures[i];
            var q = System.Math.Min(previousQ, item.P * signatures.Length / (i + 1));
            previousQ = q;
            results.Add(new EnrichmentResult(cluster, item.Signature, item.A, clusterIndexes.Length,
                item.C, restIndexes.Length, item.Odds, item.P, System.Math.Min(1, q)));
        }

        return results;
    }

    // Two-sided Fisher exact for [a,b;c,d].
    private static double FisherExact(int a, int b, int c, int d)
    {
        var n = a + b + c + d;
        var row1 = a + b;
        var row2 = c + d;
        var col1 = a + c;
        var observed = LogHypergeometric(a, row1, col1, n);
        var total = 0d;
        var min = System.Math.Max(0, col1 - row2);
        var max = System.Math.Min(row1, col1);
        for (var x = min; x <= max; x++)
        {
            var probability = System.Math.Exp(LogHypergeometric(x, row1, col1, n));
            if (probability <= System.Math.Exp(observed) * (1 + 1e-9))
                total += probability;
        }

        return System.Math.Min(1, total);
    }

    private static double LogHypergeometric(int x, int row1, int col1, int n)
    {
        static double LogChoose(int n, int k) =>
            k < 0 || k > n ? double.NegativeInfinity
            : LogFactorial(n) - LogFactorial(k) - LogFactorial(n - k);
        return LogChoose(col1, x) + LogChoose(n - col1, row1 - x) - LogChoose(n, row1);
    }

    private static double LogFactorial(int n)
    {
        var result = 0d;
        for (var i = 2; i <= n; i++)
            result += System.Math.Log(i);
        return result;
    }
}
