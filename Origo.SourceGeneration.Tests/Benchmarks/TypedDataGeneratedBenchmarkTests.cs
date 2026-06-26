using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Origo.Core.Snd.Metadata;
using Origo.SourceGeneration.Tests.TestSupport;
using Xunit;

namespace Origo.SourceGeneration.Tests;

/// <summary>
///     Performance benchmarks for the source-generated TypedData product: the inline
///     (zero-boxing) storage and Kind-based dispatch emitted by TypedDataGenerator,
///     compared against an unoptimized boxed reference implementation, across several
///     value types and a reference type.
///
///     These are lenient benchmarks. They do NOT require the generated path to be
///     faster than the boxed baseline; they only guard against gross slowdowns
///     (generated path must stay within a generous multiple of the baseline) and
///     against runaway durations (per-benchmark absolute time cap).
///
///     Noise control: each measurement uses a fixed-capacity pool addressed by a bit
///     mask, a large iteration count (so a single timed pass spans many OS time
///     slices), one warmup round, and several timed rounds — taking the minimum
///     elapsed time per side to drop rounds disturbed by preemption or GC.
///
///     Only the public TypedData API is used (explicit operators, TryGetXxx,
///     TryGetString, Data, FromObject), so no access to Origo.Core internals is
///     required. Tagged [Trait("Category", "Benchmark")] so it runs in a dedicated
///     CI step (scripts/benchmark.sh) rather than alongside the coverage-gated suite.
/// </summary>
[Trait("Category", "Benchmark")]
public class TypedDataGeneratedBenchmarkTests
{
    private const int PoolSize = 1 << 16;
    private const int PoolMask = PoolSize - 1;
    private const int SampleCount = 256;
    private const int SampleMask = SampleCount - 1;
    private const int ReadIterations = 10_000_000;
    private const int WriteIterations = 2_000_000;
    private const int WarmupRounds = 1;
    private const int TimedRounds = 5;

    private const double MaxSlowdownFactor = 8.0;
    private static readonly TimeSpan BaselineFloor = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan PerBenchmarkCap = TimeSpan.FromSeconds(5);

    private readonly PerfReporter _perf;

    public TypedDataGeneratedBenchmarkTests(ITestOutputHelper output)
    {
        _perf = PerfReporter.ForTest(output);
    }

    private delegate TypedData GenFactory<T>(T value);

    private delegate bool GenReader<T>(in TypedData td, out T value);

    private delegate bool IsType(in TypedData td);

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

    // ─── Value types ───────────────────────────────────────────────

    [Fact]
    public void ValueTypes_WriteThroughput_GeneratedOperator_vs_BoxedClass()
    {
        RunWriteBenchmark("Int32", MakeSamples(i => i), static v => (TypedData)v);
        RunWriteBenchmark("Int64", MakeSamples(i => (long)i), static v => (TypedData)v);
        RunWriteBenchmark("Single", MakeSamples(i => i * 1.5f), static v => (TypedData)v);
        RunWriteBenchmark("Double", MakeSamples(i => i * 1.5d), static v => (TypedData)v);
        RunWriteBenchmark("Boolean", MakeSamples(i => i % 2 == 0), static v => (TypedData)v);
        RunWriteBenchmark("Char", MakeSamples(i => (char)('A' + i % 26)), static v => (TypedData)v);
    }

    [Fact]
    public void ValueTypes_ReadThroughput_GeneratedKind_vs_BoxedIsT()
    {
        RunReadBenchmark("Int32", MakeSamples(i => i),
            static v => (TypedData)v,
            static (in TypedData td, out int v) => td.TryGetInt32(out v),
            static o => o.Data is int);
        RunReadBenchmark("Int64", MakeSamples(i => (long)i),
            static v => (TypedData)v,
            static (in TypedData td, out long v) => td.TryGetInt64(out v),
            static o => o.Data is long);
        RunReadBenchmark("Single", MakeSamples(i => i * 1.5f),
            static v => (TypedData)v,
            static (in TypedData td, out float v) => td.TryGetSingle(out v),
            static o => o.Data is float);
        RunReadBenchmark("Double", MakeSamples(i => i * 1.5d),
            static v => (TypedData)v,
            static (in TypedData td, out double v) => td.TryGetDouble(out v),
            static o => o.Data is double);
        RunReadBenchmark("Boolean", MakeSamples(i => i % 2 == 0),
            static v => (TypedData)v,
            static (in TypedData td, out bool v) => td.TryGetBoolean(out v),
            static o => o.Data is bool);
        RunReadBenchmark("Char", MakeSamples(i => (char)('A' + i % 26)),
            static v => (TypedData)v,
            static (in TypedData td, out char v) => td.TryGetChar(out v),
            static o => o.Data is char);
    }

    // ─── Reference type ────────────────────────────────────────────

    [Fact]
    public void ReferenceType_String_GeneratedRefSlot_vs_BoxedClass()
    {
        var samples = MakeSamples(i => "s_" + i);

        RunWriteBenchmark("String", samples,
            static v => TypedData.FromObject(typeof(string), v));

        RunReadBenchmark("String", samples,
            static v => TypedData.FromObject(typeof(string), v),
            static (in TypedData td, out string v) => td.TryGetString(out v),
            static o => o.Data is string);
    }

    [Fact]
    public void StringRead_IsString_vs_BoxedIsT()
    {
        var samples = MakeSamples(i => "s_" + i);

        RunIsBenchmark("String", samples,
            static v => TypedData.FromObject(typeof(string), v),
            static (in TypedData td) => td.IsString,
            static o => o.Data is string);
    }

    // ─── Mixed dispatch ────────────────────────────────────────────

    [Fact]
    public void MixedDispatch_GeneratedKind_vs_BoxedIsT()
    {
        var genPool = new TypedData[PoolSize];
        var boxedPool = new OldTypedData[PoolSize];
        for (var i = 0; i < PoolSize; i++)
        {
            switch (i % 5)
            {
                case 0:
                    genPool[i] = (TypedData)i;
                    boxedPool[i] = new OldTypedData(typeof(int), i);
                    break;
                case 1:
                    genPool[i] = (TypedData)(float)i;
                    boxedPool[i] = new OldTypedData(typeof(float), (float)i);
                    break;
                case 2:
                    genPool[i] = (TypedData)(i % 2 == 0);
                    boxedPool[i] = new OldTypedData(typeof(bool), i % 2 == 0);
                    break;
                case 3:
                    genPool[i] = TypedData.FromObject(typeof(string), "s_" + i);
                    boxedPool[i] = new OldTypedData(typeof(string), "s_" + i);
                    break;
                default:
                    genPool[i] = (TypedData)(double)i;
                    boxedPool[i] = new OldTypedData(typeof(double), (double)i);
                    break;
            }
        }

        var genBest = TimeSpan.MaxValue;
        var boxedBest = TimeSpan.MaxValue;

        for (var round = 0; round < WarmupRounds + TimedRounds; round++)
        {
            var genHits = 0;
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < ReadIterations; i++)
            {
                ref readonly var td = ref genPool[i & PoolMask];
                if (td.TryGetInt32(out _)) genHits++;
                if (td.TryGetSingle(out _)) genHits++;
                if (td.TryGetBoolean(out _)) genHits++;
                if (td.TryGetString(out _)) genHits++;
                if (td.TryGetDouble(out _)) genHits++;
            }
            sw.Stop();

            var boxedHits = 0;
            var sw2 = Stopwatch.StartNew();
            for (var i = 0; i < ReadIterations; i++)
            {
                var data = boxedPool[i & PoolMask].Data;
                if (data is int) boxedHits++;
                if (data is float) boxedHits++;
                if (data is bool) boxedHits++;
                if (data is string) boxedHits++;
                if (data is double) boxedHits++;
            }
            sw2.Stop();

            Assert.Equal(genHits, boxedHits);

            if (round >= WarmupRounds)
            {
                if (sw.Elapsed < genBest) genBest = sw.Elapsed;
                if (sw2.Elapsed < boxedBest) boxedBest = sw2.Elapsed;
            }
        }

        // Allocation is measured in a separate NoInlining helper so the timed
        // loops above are the only pool-touching code in this method; the
        // measurement passes never share their codegen.
        var (genAlloc, boxedAlloc) = MeasureMixedAlloc(genPool, boxedPool);

        _perf.Compare(
            $"Mixed dispatch (int/float/bool/string/double): generated Kind vs boxed 'is T' (min of {TimedRounds})",
            "Generated TryGetXxx", ReadIterations, genBest, genAlloc,
            "Boxed Data is T", ReadIterations, boxedBest, boxedAlloc);

        AssertWithinBudget("Mixed dispatch", genBest, boxedBest);
    }

    // ─── Generic timing helpers ────────────────────────────────────

    private void RunWriteBenchmark<T>(string typeLabel, T[] samples, GenFactory<T> makeGen)
    {
        var genPool = new TypedData[PoolSize];
        var boxedPool = new OldTypedData[PoolSize];

        var genBest = TimeSpan.MaxValue;
        var boxedBest = TimeSpan.MaxValue;

        for (var round = 0; round < WarmupRounds + TimedRounds; round++)
        {
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < WriteIterations; i++)
                genPool[i & PoolMask] = makeGen(samples[i & SampleMask]);
            sw.Stop();

            var sw2 = Stopwatch.StartNew();
            for (var i = 0; i < WriteIterations; i++)
                boxedPool[i & PoolMask] = new OldTypedData(typeof(T), samples[i & SampleMask]);
            sw2.Stop();

            if (round >= WarmupRounds)
            {
                if (sw.Elapsed < genBest) genBest = sw.Elapsed;
                if (sw2.Elapsed < boxedBest) boxedBest = sw2.Elapsed;
            }
        }

        // Defeat dead-store elimination: the pools are read after the timed loops.
        Assert.False(genPool[0].IsNull && boxedPool[0] is null);

        var (genAlloc, boxedAlloc) = MeasureWriteAlloc(genPool, boxedPool, samples, makeGen);

        _perf.Compare($"Write {typeLabel}: generated operator vs boxed class (min of {TimedRounds})",
            $"Generated {typeLabel}", WriteIterations, genBest, genAlloc,
            $"Boxed {typeLabel}", WriteIterations, boxedBest, boxedAlloc);

        AssertWithinBudget($"Write {typeLabel}", genBest, boxedBest);
    }

    private void RunReadBenchmark<T>(
        string typeLabel, T[] samples, GenFactory<T> makeGen, GenReader<T> tryGet, Func<OldTypedData, bool> boxedMatch)
    {
        var genPool = new TypedData[PoolSize];
        var boxedPool = new OldTypedData[PoolSize];
        for (var i = 0; i < PoolSize; i++)
        {
            var sample = samples[i & SampleMask];
            genPool[i] = makeGen(sample);
            boxedPool[i] = new OldTypedData(typeof(T), sample);
        }

        var genBest = TimeSpan.MaxValue;
        var boxedBest = TimeSpan.MaxValue;

        for (var round = 0; round < WarmupRounds + TimedRounds; round++)
        {
            var genHits = 0;
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < ReadIterations; i++)
                if (tryGet(in genPool[i & PoolMask], out _))
                    genHits++;
            sw.Stop();

            var boxedHits = 0;
            var sw2 = Stopwatch.StartNew();
            for (var i = 0; i < ReadIterations; i++)
                if (boxedMatch(boxedPool[i & PoolMask]))
                    boxedHits++;
            sw2.Stop();

            Assert.Equal(genHits, boxedHits);

            if (round >= WarmupRounds)
            {
                if (sw.Elapsed < genBest) genBest = sw.Elapsed;
                if (sw2.Elapsed < boxedBest) boxedBest = sw2.Elapsed;
            }
        }

        var (genAlloc, boxedAlloc) = MeasureReadAlloc(genPool, boxedPool, tryGet, boxedMatch);

        _perf.Compare($"Read {typeLabel}: generated TryGet vs boxed 'is {typeLabel}' (min of {TimedRounds})",
            $"Generated {typeLabel}", ReadIterations, genBest, genAlloc,
            $"Boxed is {typeLabel}", ReadIterations, boxedBest, boxedAlloc);

        AssertWithinBudget($"Read {typeLabel}", genBest, boxedBest);
    }

    private void RunIsBenchmark<T>(
        string typeLabel, T[] samples, GenFactory<T> makeGen, IsType isCheck, Func<OldTypedData, bool> boxedMatch)
    {
        var genPool = new TypedData[PoolSize];
        var boxedPool = new OldTypedData[PoolSize];
        for (var i = 0; i < PoolSize; i++)
        {
            var sample = samples[i & SampleMask];
            genPool[i] = makeGen(sample);
            boxedPool[i] = new OldTypedData(typeof(T), sample);
        }

        var genBest = TimeSpan.MaxValue;
        var boxedBest = TimeSpan.MaxValue;

        for (var round = 0; round < WarmupRounds + TimedRounds; round++)
        {
            var genHits = 0;
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < ReadIterations; i++)
                if (isCheck(in genPool[i & PoolMask]))
                    genHits++;
            sw.Stop();

            var boxedHits = 0;
            var sw2 = Stopwatch.StartNew();
            for (var i = 0; i < ReadIterations; i++)
                if (boxedMatch(boxedPool[i & PoolMask]))
                    boxedHits++;
            sw2.Stop();

            Assert.Equal(genHits, boxedHits);

            if (round >= WarmupRounds)
            {
                if (sw.Elapsed < genBest) genBest = sw.Elapsed;
                if (sw2.Elapsed < boxedBest) boxedBest = sw2.Elapsed;
            }
        }

        var (genAlloc, boxedAlloc) = MeasureIsAlloc(genPool, boxedPool, isCheck, boxedMatch);

        _perf.Compare($"Read {typeLabel}: generated IsType vs boxed 'is {typeLabel}' (min of {TimedRounds})",
            $"Generated {typeLabel}", ReadIterations, genBest, genAlloc,
            $"Boxed is {typeLabel}", ReadIterations, boxedBest, boxedAlloc);

        AssertWithinBudget($"Read {typeLabel}", genBest, boxedBest);
    }

    private static T[] MakeSamples<T>(Func<int, T> factory)
    {
        var arr = new T[SampleCount];
        for (var i = 0; i < SampleCount; i++)
            arr[i] = factory(i);
        return arr;
    }

    // ─── Allocation measurement (kept out-of-line) ─────────────────
    // Each measurement runs one untimed pass per side and returns the
    // GC.GetAllocatedBytesForCurrentThread delta. NoInlining keeps these loop
    // bodies out of the timed methods, so the measurement never shares codegen
    // with the timed loops.

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (long gen, long boxed) MeasureWriteAlloc<T>(
        TypedData[] genPool, OldTypedData[] boxedPool, T[] samples, GenFactory<T> makeGen)
    {
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < WriteIterations; i++)
            genPool[i & PoolMask] = makeGen(samples[i & SampleMask]);
        var gen = GC.GetAllocatedBytesForCurrentThread() - start;

        start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < WriteIterations; i++)
            boxedPool[i & PoolMask] = new OldTypedData(typeof(T), samples[i & SampleMask]);
        var boxed = GC.GetAllocatedBytesForCurrentThread() - start;
        return (gen, boxed);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (long gen, long boxed) MeasureReadAlloc<T>(
        TypedData[] genPool, OldTypedData[] boxedPool, GenReader<T> tryGet, Func<OldTypedData, bool> boxedMatch)
    {
        var sink = 0;
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < ReadIterations; i++)
            if (tryGet(in genPool[i & PoolMask], out _))
                sink++;
        var gen = GC.GetAllocatedBytesForCurrentThread() - start;

        start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < ReadIterations; i++)
            if (boxedMatch(boxedPool[i & PoolMask]))
                sink++;
        var boxed = GC.GetAllocatedBytesForCurrentThread() - start;
        GC.KeepAlive(sink);
        return (gen, boxed);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (long gen, long boxed) MeasureIsAlloc(
        TypedData[] genPool, OldTypedData[] boxedPool, IsType isCheck, Func<OldTypedData, bool> boxedMatch)
    {
        var sink = 0;
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < ReadIterations; i++)
            if (isCheck(in genPool[i & PoolMask]))
                sink++;
        var gen = GC.GetAllocatedBytesForCurrentThread() - start;

        start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < ReadIterations; i++)
            if (boxedMatch(boxedPool[i & PoolMask]))
                sink++;
        var boxed = GC.GetAllocatedBytesForCurrentThread() - start;
        GC.KeepAlive(sink);
        return (gen, boxed);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (long gen, long boxed) MeasureMixedAlloc(TypedData[] genPool, OldTypedData[] boxedPool)
    {
        var sink = 0;
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < ReadIterations; i++)
        {
            ref readonly var td = ref genPool[i & PoolMask];
            if (td.TryGetInt32(out _)) sink++;
            if (td.TryGetSingle(out _)) sink++;
            if (td.TryGetBoolean(out _)) sink++;
            if (td.TryGetString(out _)) sink++;
            if (td.TryGetDouble(out _)) sink++;
        }
        var gen = GC.GetAllocatedBytesForCurrentThread() - start;

        start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < ReadIterations; i++)
        {
            var data = boxedPool[i & PoolMask].Data;
            if (data is int) sink++;
            if (data is float) sink++;
            if (data is bool) sink++;
            if (data is string) sink++;
            if (data is double) sink++;
        }
        var boxed = GC.GetAllocatedBytesForCurrentThread() - start;
        GC.KeepAlive(sink);
        return (gen, boxed);
    }

    private static void AssertWithinBudget(string label, TimeSpan generated, TimeSpan baseline)
    {
        Assert.True(generated < PerBenchmarkCap,
            $"{label}: generated min {generated.TotalMilliseconds:F2}ms exceeds {PerBenchmarkCap.TotalSeconds:F0}s cap");

        // A sub-millisecond baseline cannot form a reliable ratio; the cap above still guards it.
        if (baseline < BaselineFloor)
            return;

        Assert.True(generated.TotalMilliseconds <= baseline.TotalMilliseconds * MaxSlowdownFactor,
            $"{label}: generated {generated.TotalMilliseconds:F2}ms exceeds {MaxSlowdownFactor:F0}x of baseline {baseline.TotalMilliseconds:F2}ms");
    }
}
