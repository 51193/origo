using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Origo.Core.Snd.Metadata;
using Origo.TestSupport;
using Xunit;

namespace Origo.SourceGeneration.Tests;

[Trait("Category", "Benchmark")]
public class TypedDataGeneratedBenchmarkTests(ITestOutputHelper output)
{
    private const int _poolSize = 1 << 16;
    private const int _poolMask = _poolSize - 1;
    private const int _sampleCount = 256;
    private const int _sampleMask = _sampleCount - 1;
    private const int _readIterations = 10_000_000;
    private const int _writeIterations = 2_000_000;
    private const int _warmupRounds = 1;
    private const int _timedRounds = 5;

    private const double _maxSlowdownFactor = 8.0;
    private static readonly TimeSpan _baselineFloor = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan _perBenchmarkCap = TimeSpan.FromSeconds(5);

    private readonly PerfReporter _perf = PerfReporter.ForTest(output);

    private delegate TypedData GenFactory<T>(T value);

    private delegate bool GenReader<T>(in TypedData td, out T value);

    private delegate bool IsType(in TypedData td);

    private sealed class OldTypedData(Type dataType, object? data)
    {
        public Type DataType { get; } = dataType;
        public object? Data { get; } = data;
    }

    // ─── Value types write ─────────────────────────────────────────

    [Fact]
    public void ValueTypes_WriteThroughput_GeneratedOperator_vs_BoxedClass()
    {
        var rows = new List<(string, int, TimeSpan, long, TimeSpan, long)>
        {
            RunWriteMeasurement("SG Write Int32", MakeSamples(i => i), static v => (TypedData)v),
            RunWriteMeasurement("SG Write Int64", MakeSamples(i => (long)i), static v => (TypedData)v),
            RunWriteMeasurement("SG Write Single", MakeSamples(i => i * 1.5f), static v => (TypedData)v),
            RunWriteMeasurement("SG Write Double", MakeSamples(i => i * 1.5d), static v => (TypedData)v),
            RunWriteMeasurement("SG Write Boolean", MakeSamples(i => i % 2 == 0), static v => (TypedData)v),
            RunWriteMeasurement("SG Write Char", MakeSamples(i => (char)('A' + i % 26)), static v => (TypedData)v),
        };

        _perf.CompareTable(
            $"Write throughput ({_writeIterations:N0} iters, min of {_timedRounds})",
            "Generated", "Boxed", rows);
    }

    // ─── Value types read ──────────────────────────────────────────

    [Fact]
    public void ValueTypes_ReadThroughput_GeneratedKind_vs_BoxedIsT()
    {
        var rows = new List<(string, int, TimeSpan, long, TimeSpan, long)>
        {
            RunReadMeasurement("SG Read Int32", MakeSamples(i => i),
                static v => (TypedData)v,
                static (in td, out v) => td.TryGetInt32(out v),
                static o => o.Data is int),
            RunReadMeasurement("SG Read Int64", MakeSamples(i => (long)i),
                static v => (TypedData)v,
                static (in td, out v) => td.TryGetInt64(out v),
                static o => o.Data is long),
            RunReadMeasurement("SG Read Single", MakeSamples(i => i * 1.5f),
                static v => (TypedData)v,
                static (in td, out v) => td.TryGetSingle(out v),
                static o => o.Data is float),
            RunReadMeasurement("SG Read Double", MakeSamples(i => i * 1.5d),
                static v => (TypedData)v,
                static (in td, out v) => td.TryGetDouble(out v),
                static o => o.Data is double),
            RunReadMeasurement("SG Read Boolean", MakeSamples(i => i % 2 == 0),
                static v => (TypedData)v,
                static (in td, out v) => td.TryGetBoolean(out v),
                static o => o.Data is bool),
            RunReadMeasurement("SG Read Char", MakeSamples(i => (char)('A' + i % 26)),
                static v => (TypedData)v,
                static (in td, out v) => td.TryGetChar(out v),
                static o => o.Data is char),
        };

        _perf.CompareTable(
            $"Read throughput ({_readIterations:N0} iters, min of {_timedRounds})",
            "Generated", "Boxed", rows);
    }

    // ─── Reference type ────────────────────────────────────────────

    [Fact]
    public void ReferenceType_String_GeneratedRefSlot_vs_BoxedClass()
    {
        var samples = MakeSamples(i => "s_" + i);

        var rows = new List<(string, int, TimeSpan, long, TimeSpan, long)>
        {
            RunWriteMeasurement("SG Write String", samples,
                static v => new TypedData(TypedData.KindMap.String, 0, v)),
            RunReadMeasurement("SG Read String", samples,
                static v => new TypedData(TypedData.KindMap.String, 0, v),
                ReadString,
                static o => o.Data is string),
        };

        _perf.CompareTable(
            $"String ref slot (write {_writeIterations:N0}, read {_readIterations:N0}, min of {_timedRounds})",
            "Generated", "Boxed", rows);
    }

    [Fact]
    public void StringRead_IsString_vs_BoxedIsT()
    {
        var samples = MakeSamples(i => "s_" + i);
        var result = RunIsMeasurement("SG IsString String", samples,
            static v => new TypedData(TypedData.KindMap.String, 0, v),
            static (in td) => td.IsString,
            static o => o.Data is string);

        _perf.CompareTable(
            $"IsString check ({_readIterations:N0} iters, min of {_timedRounds})",
            "Generated", "Boxed",
            [result]);
    }

    // ─── Mixed dispatch ────────────────────────────────────────────

    [Fact]
    public void MixedDispatch_GeneratedKind_vs_BoxedIsT()
    {
        var genPool = new TypedData[_poolSize];
        var boxedPool = new OldTypedData[_poolSize];
        for (var i = 0; i < _poolSize; i++)
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
                    genPool[i] = new TypedData(TypedData.KindMap.String, 0, "s_" + i);
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

        for (var round = 0; round < _warmupRounds + _timedRounds; round++)
        {
            var genHits = 0;
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < _readIterations; i++)
            {
                ref readonly var td = ref genPool[i & _poolMask];
                if (td.TryGetInt32(out _)) genHits++;
                if (td.TryGetSingle(out _)) genHits++;
                if (td.TryGetBoolean(out _)) genHits++;
                if (td.TryGetString(out _)) genHits++;
                if (td.TryGetDouble(out _)) genHits++;
            }
            sw.Stop();

            var boxedHits = 0;
            var sw2 = Stopwatch.StartNew();
            for (var i = 0; i < _readIterations; i++)
            {
                var data = boxedPool[i & _poolMask].Data;
                if (data is int) boxedHits++;
                if (data is float) boxedHits++;
                if (data is bool) boxedHits++;
                if (data is string) boxedHits++;
                if (data is double) boxedHits++;
            }
            sw2.Stop();

            Assert.Equal(genHits, boxedHits);

            if (round >= _warmupRounds)
            {
                if (sw.Elapsed < genBest) genBest = sw.Elapsed;
                if (sw2.Elapsed < boxedBest) boxedBest = sw2.Elapsed;
            }
        }

        var (genAlloc, boxedAlloc) = MeasureMixedAlloc(genPool, boxedPool);

        _perf.CompareTable(
            $"Mixed dispatch (int/float/bool/string/double, {_readIterations:N0} iters, min of {_timedRounds})",
            "Generated", "Boxed",
            [
                ("Mixed", _readIterations, genBest, genAlloc, boxedBest, boxedAlloc)
            ]);

        AssertWithinBudget("Mixed dispatch", genBest, boxedBest);
    }

    // ─── Timing helpers (return data, no side-effect printing) ─────

    private static (string label, int iter, TimeSpan genTime, long genAlloc, TimeSpan boxedTime, long boxedAlloc)
        RunWriteMeasurement<T>(string typeLabel, T[] samples, GenFactory<T> makeGen)
    {
        var genPool = new TypedData[_poolSize];
        var boxedPool = new OldTypedData[_poolSize];

        var genBest = TimeSpan.MaxValue;
        var boxedBest = TimeSpan.MaxValue;

        for (var round = 0; round < _warmupRounds + _timedRounds; round++)
        {
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < _writeIterations; i++)
                genPool[i & _poolMask] = makeGen(samples[i & _sampleMask]);
            sw.Stop();

            var sw2 = Stopwatch.StartNew();
            for (var i = 0; i < _writeIterations; i++)
                boxedPool[i & _poolMask] = new OldTypedData(typeof(T), samples[i & _sampleMask]);
            sw2.Stop();

            if (round >= _warmupRounds)
            {
                if (sw.Elapsed < genBest) genBest = sw.Elapsed;
                if (sw2.Elapsed < boxedBest) boxedBest = sw2.Elapsed;
            }
        }

        Assert.False(genPool[0].IsNull && boxedPool[0] is null);

        var (genAlloc, boxedAlloc) = MeasureWriteAlloc(genPool, boxedPool, samples, makeGen);

        AssertWithinBudget($"Write {typeLabel}", genBest, boxedBest);

        return (typeLabel, _writeIterations, genBest, genAlloc, boxedBest, boxedAlloc);
    }

    private static bool ReadString(in TypedData td, out string value)
    {
        var ok = td.TryGetString(out var result);
        value = result!;
        return ok;
    }

    private static (string label, int iter, TimeSpan genTime, long genAlloc, TimeSpan boxedTime, long boxedAlloc)
        RunReadMeasurement<T>(string typeLabel, T[] samples, GenFactory<T> makeGen,
            GenReader<T> tryGet, Func<OldTypedData, bool> boxedMatch)
    {
        var genPool = new TypedData[_poolSize];
        var boxedPool = new OldTypedData[_poolSize];
        for (var i = 0; i < _poolSize; i++)
        {
            var sample = samples[i & _sampleMask];
            genPool[i] = makeGen(sample);
            boxedPool[i] = new OldTypedData(typeof(T), sample);
        }

        var genBest = TimeSpan.MaxValue;
        var boxedBest = TimeSpan.MaxValue;

        for (var round = 0; round < _warmupRounds + _timedRounds; round++)
        {
            var genHits = 0;
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < _readIterations; i++)
                if (tryGet(in genPool[i & _poolMask], out _))
                    genHits++;
            sw.Stop();

            var boxedHits = 0;
            var sw2 = Stopwatch.StartNew();
            for (var i = 0; i < _readIterations; i++)
                if (boxedMatch(boxedPool[i & _poolMask]))
                    boxedHits++;
            sw2.Stop();

            Assert.Equal(genHits, boxedHits);

            if (round >= _warmupRounds)
            {
                if (sw.Elapsed < genBest) genBest = sw.Elapsed;
                if (sw2.Elapsed < boxedBest) boxedBest = sw2.Elapsed;
            }
        }

        var (genAlloc, boxedAlloc) = MeasureReadAlloc(genPool, boxedPool, tryGet, boxedMatch);

        AssertWithinBudget($"Read {typeLabel}", genBest, boxedBest);

        return (typeLabel, _readIterations, genBest, genAlloc, boxedBest, boxedAlloc);
    }

    private static (string label, int iter, TimeSpan genTime, long genAlloc, TimeSpan boxedTime, long boxedAlloc)
        RunIsMeasurement<T>(string typeLabel, T[] samples, GenFactory<T> makeGen,
            IsType isCheck, Func<OldTypedData, bool> boxedMatch)
    {
        var genPool = new TypedData[_poolSize];
        var boxedPool = new OldTypedData[_poolSize];
        for (var i = 0; i < _poolSize; i++)
        {
            var sample = samples[i & _sampleMask];
            genPool[i] = makeGen(sample);
            boxedPool[i] = new OldTypedData(typeof(T), sample);
        }

        var genBest = TimeSpan.MaxValue;
        var boxedBest = TimeSpan.MaxValue;

        for (var round = 0; round < _warmupRounds + _timedRounds; round++)
        {
            var genHits = 0;
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < _readIterations; i++)
                if (isCheck(in genPool[i & _poolMask]))
                    genHits++;
            sw.Stop();

            var boxedHits = 0;
            var sw2 = Stopwatch.StartNew();
            for (var i = 0; i < _readIterations; i++)
                if (boxedMatch(boxedPool[i & _poolMask]))
                    boxedHits++;
            sw2.Stop();

            Assert.Equal(genHits, boxedHits);

            if (round >= _warmupRounds)
            {
                if (sw.Elapsed < genBest) genBest = sw.Elapsed;
                if (sw2.Elapsed < boxedBest) boxedBest = sw2.Elapsed;
            }
        }

        var (genAlloc, boxedAlloc) = MeasureIsAlloc(genPool, boxedPool, isCheck, boxedMatch);

        AssertWithinBudget($"Read {typeLabel}", genBest, boxedBest);

        return (typeLabel, _readIterations, genBest, genAlloc, boxedBest, boxedAlloc);
    }

    private static T[] MakeSamples<T>(Func<int, T> factory)
    {
        var arr = new T[_sampleCount];
        for (var i = 0; i < _sampleCount; i++)
            arr[i] = factory(i);
        return arr;
    }

    // ─── Allocation measurement (kept out-of-line) ─────────────────

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (long gen, long boxed) MeasureWriteAlloc<T>(
        TypedData[] genPool, OldTypedData[] boxedPool, T[] samples, GenFactory<T> makeGen)
    {
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < _writeIterations; i++)
            genPool[i & _poolMask] = makeGen(samples[i & _sampleMask]);
        var gen = GC.GetAllocatedBytesForCurrentThread() - start;

        start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < _writeIterations; i++)
            boxedPool[i & _poolMask] = new OldTypedData(typeof(T), samples[i & _sampleMask]);
        var boxed = GC.GetAllocatedBytesForCurrentThread() - start;
        return (gen, boxed);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (long gen, long boxed) MeasureReadAlloc<T>(
        TypedData[] genPool, OldTypedData[] boxedPool, GenReader<T> tryGet, Func<OldTypedData, bool> boxedMatch)
    {
        var sink = 0;
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < _readIterations; i++)
            if (tryGet(in genPool[i & _poolMask], out _))
                sink++;
        var gen = GC.GetAllocatedBytesForCurrentThread() - start;

        start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < _readIterations; i++)
            if (boxedMatch(boxedPool[i & _poolMask]))
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
        for (var i = 0; i < _readIterations; i++)
            if (isCheck(in genPool[i & _poolMask]))
                sink++;
        var gen = GC.GetAllocatedBytesForCurrentThread() - start;

        start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < _readIterations; i++)
            if (boxedMatch(boxedPool[i & _poolMask]))
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
        for (var i = 0; i < _readIterations; i++)
        {
            ref readonly var td = ref genPool[i & _poolMask];
            if (td.TryGetInt32(out _)) sink++;
            if (td.TryGetSingle(out _)) sink++;
            if (td.TryGetBoolean(out _)) sink++;
            if (td.TryGetString(out _)) sink++;
            if (td.TryGetDouble(out _)) sink++;
        }
        var gen = GC.GetAllocatedBytesForCurrentThread() - start;

        start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < _readIterations; i++)
        {
            var data = boxedPool[i & _poolMask].Data;
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
        Assert.True(generated < _perBenchmarkCap,
            $"{label}: generated min {generated.TotalMilliseconds:F2}ms exceeds {_perBenchmarkCap.TotalSeconds:F0}s cap");

        if (baseline < _baselineFloor)
            return;

        Assert.True(generated.TotalMilliseconds <= baseline.TotalMilliseconds * _maxSlowdownFactor,
            $"{label}: generated {generated.TotalMilliseconds:F2}ms exceeds {_maxSlowdownFactor:F0}x of baseline {baseline.TotalMilliseconds:F2}ms");
    }
}
