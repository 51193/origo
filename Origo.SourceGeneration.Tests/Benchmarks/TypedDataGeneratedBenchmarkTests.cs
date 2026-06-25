using System;
using System.Diagnostics;
using Origo.Core.Snd.Metadata;
using Origo.SourceGeneration.Tests.TestSupport;
using Xunit;

namespace Origo.SourceGeneration.Tests;

/// <summary>
///     Performance benchmarks for the source-generated TypedData product: the inline
///     (zero-boxing) storage and Kind-based dispatch emitted by TypedDataGenerator,
///     compared against an unoptimized boxed reference implementation.
///
///     These are lenient benchmarks. They do NOT require the generated path to be
///     faster than the boxed baseline; they only guard against gross slowdowns
///     (generated path must stay within a generous multiple of the baseline) and
///     against runaway durations (each benchmark has an absolute time cap). The
///     comparison table is printed on every CI run via <see cref="PerfReporter" />.
///
///     Only the public TypedData API is used (explicit operators, TryGetXxx, Data,
///     FromObject), so no access to Origo.Core internals is required.
/// </summary>
public class TypedDataGeneratedBenchmarkTests
{
    private const int Count = 500_000;
    private const int MixedCount = 200_000;
    private const int WarmupCount = 10_000;

    // Lenient: the generated path may be slower than the boxed baseline, but not by
    // more than this factor. Tiny absolute durations bypass the ratio check to avoid
    // flakiness from CI timing jitter.
    private const double MaxSlowdownFactor = 8.0;
    private static readonly TimeSpan NegligibleDuration = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan MaxBenchmarkDuration = TimeSpan.FromSeconds(5);

    private readonly PerfReporter _perf;

    public TypedDataGeneratedBenchmarkTests(ITestOutputHelper output)
    {
        _perf = PerfReporter.ForTest(output);
    }

    private sealed class OldTypedData
    {
        public Type DataType { get; }
        public object? Data { get; }

        public OldTypedData(Type dataType, object? data)
        {
            DataType = dataType;
            Data = data;
        }
    }

    [Fact]
    public void WriteThroughput_GeneratedInline_vs_BoxedClass()
    {
        var genArr = new TypedData[Count];
        var boxedArr = new OldTypedData[Count];

        for (var i = 0; i < WarmupCount; i++)
        {
            genArr[i] = (TypedData)i;
            boxedArr[i] = new OldTypedData(typeof(int), i);
        }

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < Count; i++)
            genArr[i] = (TypedData)i;
        sw.Stop();
        var genTime = sw.Elapsed;

        sw.Restart();
        for (var i = 0; i < Count; i++)
            boxedArr[i] = new OldTypedData(typeof(int), i);
        sw.Stop();
        var boxedTime = sw.Elapsed;

        _perf.Compare("Write: generated inline TypedData vs boxed class",
            "Generated (TypedData)i", Count, genTime, 0,
            "Boxed OldTypedData", Count, boxedTime, 0);

        AssertWithinBudget("Write", genTime, boxedTime);
    }

    [Fact]
    public void ReadThroughput_GeneratedKind_vs_BoxedIsT_Int32()
    {
        var genArr = new TypedData[Count];
        var boxedArr = new OldTypedData[Count];
        for (var i = 0; i < Count; i++)
        {
            genArr[i] = (TypedData)i;
            boxedArr[i] = new OldTypedData(typeof(int), i);
        }

        long warmGen = 0;
        long warmBoxed = 0;
        for (var i = 0; i < WarmupCount; i++)
        {
            if (genArr[i].TryGetInt32(out var g)) warmGen += g;
            if (boxedArr[i].Data is int b) warmBoxed += b;
        }
        Assert.Equal(warmGen, warmBoxed);

        var sw = Stopwatch.StartNew();
        long genSum = 0;
        for (var i = 0; i < Count; i++)
            if (genArr[i].TryGetInt32(out var v))
                genSum += v;
        sw.Stop();
        var genTime = sw.Elapsed;

        sw.Restart();
        long boxedSum = 0;
        for (var i = 0; i < Count; i++)
            if (boxedArr[i].Data is int v)
                boxedSum += v;
        sw.Stop();
        var boxedTime = sw.Elapsed;

        Assert.Equal(genSum, boxedSum);

        _perf.Compare("Read Int32: generated TryGetInt32 vs boxed 'is int'",
            "Generated TryGetInt32", Count, genTime, 0,
            "Boxed Data is int", Count, boxedTime, 0);

        AssertWithinBudget("Read Int32", genTime, boxedTime);
    }

    [Fact]
    public void MixedDispatch_GeneratedKind_vs_BoxedIsT()
    {
        var genArr = new TypedData[MixedCount];
        var boxedArr = new OldTypedData[MixedCount];
        for (var i = 0; i < MixedCount; i++)
        {
            switch (i % 5)
            {
                case 0:
                    genArr[i] = (TypedData)i;
                    boxedArr[i] = new OldTypedData(typeof(int), i);
                    break;
                case 1:
                    genArr[i] = (TypedData)(float)i;
                    boxedArr[i] = new OldTypedData(typeof(float), (float)i);
                    break;
                case 2:
                    genArr[i] = (TypedData)(i % 2 == 0);
                    boxedArr[i] = new OldTypedData(typeof(bool), i % 2 == 0);
                    break;
                case 3:
                    genArr[i] = TypedData.FromObject(typeof(string), "s_" + i);
                    boxedArr[i] = new OldTypedData(typeof(string), "s_" + i);
                    break;
                default:
                    genArr[i] = (TypedData)(double)i;
                    boxedArr[i] = new OldTypedData(typeof(double), (double)i);
                    break;
            }
        }

        var sw = Stopwatch.StartNew();
        var genHits = 0;
        for (var i = 0; i < MixedCount; i++)
        {
            var td = genArr[i];
            if (td.TryGetInt32(out _)) genHits++;
            if (td.TryGetSingle(out _)) genHits++;
            if (td.TryGetBoolean(out _)) genHits++;
            if (td.TryGetString(out _)) genHits++;
            if (td.TryGetDouble(out _)) genHits++;
        }
        sw.Stop();
        var genTime = sw.Elapsed;

        sw.Restart();
        var boxedHits = 0;
        for (var i = 0; i < MixedCount; i++)
        {
            var data = boxedArr[i].Data;
            if (data is int) boxedHits++;
            if (data is float) boxedHits++;
            if (data is bool) boxedHits++;
            if (data is string) boxedHits++;
            if (data is double) boxedHits++;
        }
        sw.Stop();
        var boxedTime = sw.Elapsed;

        Assert.Equal(boxedHits, genHits);

        _perf.Compare("Mixed dispatch (int/float/bool/string/double): generated Kind vs boxed 'is T'",
            "Generated TryGetXxx", MixedCount * 5, genTime, 0,
            "Boxed Data is T", MixedCount * 5, boxedTime, 0);

        AssertWithinBudget("Mixed dispatch", genTime, boxedTime);
    }

    private static void AssertWithinBudget(string label, TimeSpan generated, TimeSpan baseline)
    {
        Assert.True(generated < MaxBenchmarkDuration,
            $"{label}: generated path took {generated.TotalSeconds:F2}s, exceeds {MaxBenchmarkDuration.TotalSeconds:F0}s cap");

        var withinRatio = generated.TotalMilliseconds <= baseline.TotalMilliseconds * MaxSlowdownFactor;
        var negligible = generated < NegligibleDuration;
        Assert.True(withinRatio || negligible,
            $"{label}: generated {generated.TotalMilliseconds:F2}ms exceeds {MaxSlowdownFactor:F0}x of baseline {baseline.TotalMilliseconds:F2}ms");
    }
}
