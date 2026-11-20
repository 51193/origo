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

/// <summary>
///     All Fisher/Benjamini-Hochberg results for one cluster: the full tested
///     signature set (not a display subset), the tested count <c>m</c>, and the
///     rows sorted by ascending p-value.
/// </summary>
public sealed record EnrichmentAnalysis(
    int Cluster,
    int TestedCount,
    IReadOnlyList<EnrichmentResult> Signatures);

/// <summary>
///     Two-sided Fisher exact tests for gene-signature presence in one cluster
///     versus the rest of the population, corrected with the Benjamini-Hochberg
///     step-up over the full tested signature set (signatures supported by at
///     least <see cref="MinimumSupport" /> rows overall). <c>m</c> is the tested
///     count, <c>q(i) = min over j &gt;= i of p(j) * m / j</c>, so
///     <c>q &gt;= p</c> and <c>q &lt;= 1</c>; the report never truncates the
///     correction to a display cap. Noise rows (label <c>-1</c>) are excluded from
///     both sides of the test.
/// </summary>
public static class GeneEnrichment
{
    /// <summary>A signature is tested only when it occurs in at least this many rows overall.</summary>
    public const int MinimumSupport = 3;

    /// <summary>Analyzes every tested signature for one cluster against the rest.</summary>
    /// <exception cref="ArgumentException">Rows and labels have different lengths.</exception>
    public static EnrichmentAnalysis Analyze(
        IReadOnlyList<FeatureRow> rows,
        int[] labels,
        int cluster)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(labels);
        if (rows.Count != labels.Length)
            throw new ArgumentException("A label is required for every row.", nameof(labels));
        var clusterIndexes = new List<int>();
        var restIndexes = new List<int>();
        for (var index = 0; index < rows.Count; index++)
        {
            if (labels[index] < 0)
                continue;
            if (labels[index] == cluster)
                clusterIndexes.Add(index);
            else
                restIndexes.Add(index);
        }

        if (clusterIndexes.Count == 0 || restIndexes.Count == 0)
            return new EnrichmentAnalysis(cluster, 0, []);
        var support = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var index in clusterIndexes.Concat(restIndexes))
            foreach (var signature in rows[index].GeneSignatures)
                support[signature] = support.GetValueOrDefault(signature) + 1;
        var tested = support
            .Where(static pair => pair.Value >= MinimumSupport)
            .Select(static pair => pair.Key)
            .OrderBy(static signature => signature, StringComparer.Ordinal)
            .ToArray();
        var logFactorials = BuildLogFactorials(rows.Count);
        var results = new List<EnrichmentResult>(tested.Length);
        foreach (var signature in tested)
        {
            var signatureCount = support[signature];
            var clusterWith = clusterIndexes.Count(index => rows[index].GeneSignatures.Contains(signature));
            var clusterWithout = clusterIndexes.Count - clusterWith;
            var restWith = signatureCount - clusterWith;
            var restWithout = restIndexes.Count - restWith;
            var pValue = FisherExact(clusterWith, clusterWithout, restWith, restWithout, logFactorials);
            var oddsRatio = ((clusterWith + 0.5d) * (restWithout + 0.5d))
                / ((clusterWithout + 0.5d) * (restWith + 0.5d));
            results.Add(new EnrichmentResult(
                cluster, signature, clusterWith, clusterIndexes.Count,
                restWith, restIndexes.Count, oddsRatio, pValue, 1));
        }

        results.Sort(static (left, right) =>
        {
            var byP = left.PValue.CompareTo(right.PValue);
            return byP != 0 ? byP : string.CompareOrdinal(left.Signature, right.Signature);
        });
        var qValues = BenjaminiHochberg([.. results.Select(static result => result.PValue)]);
        for (var index = 0; index < results.Count; index++)
            results[index] = results[index] with { QValue = qValues[index] };

        return new EnrichmentAnalysis(cluster, results.Count, results);
    }

    /// <summary>
    ///     Benjamini-Hochberg step-up over the sorted p-values: <c>m</c> is the
    ///     number of tested hypotheses and <c>q(i) = min over j &gt;= i of
    ///     p(j) * m / j</c>, clamped to <c>[p(i), 1]</c> so <c>q &gt;= p</c>.
    /// </summary>
    /// <exception cref="ArgumentException">A p-value is not in [0, 1].</exception>
    public static IReadOnlyList<double> BenjaminiHochberg(IReadOnlyList<double> pValues)
    {
        ArgumentNullException.ThrowIfNull(pValues);
        var m = pValues.Count;
        var qValues = new double[m];
        var running = 1d;
        for (var index = m - 1; index >= 0; index--)
        {
            var pValue = pValues[index];
            if (!double.IsFinite(pValue) || pValue is < 0 or > 1)
                throw new ArgumentException(
                    $"p-value at rank {index + 1} is not in [0, 1]: '{pValue}'.", nameof(pValues));
            running = System.Math.Min(running, pValue * m / (index + 1d));
            qValues[index] = System.Math.Min(1d, System.Math.Max(pValue, running));
        }

        return qValues;
    }

    /// <summary>Two-sided Fisher exact test for the 2x2 table <c>[a, b; c, d]</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A cell count is negative.</exception>
    public static double FisherExact(int a, int b, int c, int d)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(a);
        ArgumentOutOfRangeException.ThrowIfNegative(b);
        ArgumentOutOfRangeException.ThrowIfNegative(c);
        ArgumentOutOfRangeException.ThrowIfNegative(d);
        return FisherExact(a, b, c, d, BuildLogFactorials(a + b + c + d));
    }

    private static double[] BuildLogFactorials(int size)
    {
        var logFactorials = new double[size + 1];
        for (var value = 1; value <= size; value++)
            logFactorials[value] = logFactorials[value - 1] + System.Math.Log(value);
        return logFactorials;
    }

    private static double FisherExact(int a, int b, int c, int d, double[] logFactorials)
    {
        var n = a + b + c + d;
        var row1 = a + b;
        var column1 = a + c;
        if (n == 0)
            return 1;
        var observed = LogHypergeometric(a, row1, column1, n, logFactorials);
        var total = 0d;
        var min = System.Math.Max(0, column1 - (c + d));
        var max = System.Math.Min(row1, column1);
        for (var x = min; x <= max; x++)
        {
            var probability = LogHypergeometric(x, row1, column1, n, logFactorials);
            if (probability <= observed + 1e-9)
                total += System.Math.Exp(probability);
        }

        return System.Math.Min(1d, total);
    }

    private static double LogHypergeometric(int x, int row1, int column1, int n, double[] logFactorials) =>
        LogChoose(column1, x, logFactorials)
        + LogChoose(n - column1, row1 - x, logFactorials)
        - LogChoose(n, row1, logFactorials);

    private static double LogChoose(int n, int k, double[] logFactorials) =>
        k < 0 || k > n ? double.NegativeInfinity
        : logFactorials[n] - logFactorials[k] - logFactorials[n - k];
}
