using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

[Trait("Category", "Benchmark")]
public class TypedDataRealWorldBenchmarkTests(ITestOutputHelper output)
{
    private const int _dictSize = 1024;
    private const int _dictMask = _dictSize - 1;
    private const int _sampleCount = 128;
    private const int _sampleMask = _sampleCount - 1;

    private const int _readIterations = 2_000_000;
    private const int _writeIterations = 500_000;
    private const int _iterateIterations = 2_000;
    private const int _warmupRounds = 1;
    private const int _timedRounds = 5;

    private static readonly TimeSpan _perBenchmarkCap = TimeSpan.FromSeconds(8);

    private readonly PerfReporter _perf = PerfReporter.ForTest(output);

    // ─── Scenario 1: Dict Lookup + TryExtract ──

    [Fact]
    public void DictLookup_TryExtract_vs_BoxedDict()
    {
        var keyCount = _dictSize / 4;
        var keys = MakeSampleKeys(keyCount);
        var genDict = FillTypedDataDict(keys);
        var boxedDict = FillBoxedDict(keys);

        var rows = new List<(string, int, TimeSpan, long, TimeSpan, long)>
        {
            RunDictReadMeasurement("String", genDict, boxedDict, "string", keys,
                static td => td.TryGetString(out _)),
            RunDictReadMeasurement("Int32", genDict, boxedDict, "int", keys,
                static td => td.TryGetInt32(out _)),
            RunDictReadMeasurement("Single", genDict, boxedDict, "float", keys,
                static td => td.TryGetSingle(out _)),
            RunDictReadMeasurement("Boolean", genDict, boxedDict, "bool", keys,
                static td => td.TryGetBoolean(out _)),
        };

        _perf.CompareTable(
            $"Dict Lookup + TryExtract ({_readIterations:N0} iters, min of {_timedRounds})",
            "TypedData", "Boxed", rows);
    }

    private static (string, int, TimeSpan, long, TimeSpan, long) RunDictReadMeasurement(string typeKey,
        Dictionary<string, TypedData> genDict, Dictionary<string, object> boxedDict,
        string typeCheck, string[] keys, Func<TypedData, bool> genCheck)
    {
        var genBest = TimeSpan.MaxValue;
        var boxedBest = TimeSpan.MaxValue;

        for (var round = 0; round < _warmupRounds + _timedRounds; round++)
        {
            var genHits = 0;
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < _readIterations; i++)
            {
                var key = keys[i % keys.Length];
                if (genDict.TryGetValue(key, out var td) && genCheck(td))
                    genHits++;
            }
            sw.Stop();

            var boxedHits = 0;
            var sw2 = Stopwatch.StartNew();
            for (var i = 0; i < _readIterations; i++)
            {
                var key = keys[i % keys.Length];
                if (boxedDict.TryGetValue(key, out var obj) && MatchesType(obj, typeCheck))
                    boxedHits++;
            }
            sw2.Stop();

            Assert.Equal(genHits, boxedHits);

            if (round >= _warmupRounds)
            {
                if (sw.Elapsed < genBest) genBest = sw.Elapsed;
                if (sw2.Elapsed < boxedBest) boxedBest = sw2.Elapsed;
            }
        }

        var (genAlloc, boxedAlloc) = MeasureDictReadAlloc(genDict, boxedDict, keys, genCheck, typeCheck);

        AssertInCap($"DictRead {typeKey}", genBest);

        return (typeKey, _readIterations, genBest, genAlloc, boxedBest, boxedAlloc);
    }

    // ─── Scenario 2: Dict Insert + Factory Create ──

    [Fact]
    public void DictInsert_FactoryCreate_vs_BoxedDict()
    {
        var rows = new List<(string, int, TimeSpan, long, TimeSpan, long)>
        {
            RunDictWriteMeasurement("String", MakeSamples(i => "s_" + i),
                v => new TypedData(TypedData.KindMap.String, 0, v)),
            RunDictWriteMeasurement("Int32", MakeSamples(i => i),
                v => (TypedData)v),
            RunDictWriteMeasurement("Single", MakeSamples(i => i * 1.5f),
                v => (TypedData)v),
            RunDictWriteMeasurement("Boolean", MakeSamples(i => i % 2 == 0),
                v => (TypedData)v),
        };

        _perf.CompareTable(
            $"Dict Insert + Factory Create ({_writeIterations:N0} iters, min of {_timedRounds})",
            "TypedData", "Boxed", rows);
    }

    private static (string, int, TimeSpan, long, TimeSpan, long) RunDictWriteMeasurement<T>(
        string typeLabel, T[] samples, Func<T, TypedData> makeGen)
    {
        var keys = MakeSampleKeys(_sampleCount);
        var genBest = TimeSpan.MaxValue;
        var boxedBest = TimeSpan.MaxValue;

        for (var round = 0; round < _warmupRounds + _timedRounds; round++)
        {
            var genDict = new Dictionary<string, TypedData>(_writeIterations, StringComparer.Ordinal);
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < _writeIterations; i++)
                genDict[keys[i % _sampleCount]] = makeGen(samples[i & _sampleMask]);
            sw.Stop();

            var boxedDict = new Dictionary<string, object>(_writeIterations, StringComparer.Ordinal);
            var sw2 = Stopwatch.StartNew();
            for (var i = 0; i < _writeIterations; i++)
                boxedDict[keys[i % _sampleCount]] = samples[i & _sampleMask]!;
            sw2.Stop();

            Assert.Equal(genDict.Count, boxedDict.Count);

            if (round >= _warmupRounds)
            {
                if (sw.Elapsed < genBest) genBest = sw.Elapsed;
                if (sw2.Elapsed < boxedBest) boxedBest = sw2.Elapsed;
            }
        }

        var (genAlloc, boxedAlloc) = MeasureDictWriteAlloc(keys, samples, makeGen);

        AssertInCap($"DictWrite {typeLabel}", genBest);

        return (typeLabel, _writeIterations, genBest, genAlloc, boxedBest, boxedAlloc);
    }

    // ─── Scenario 3: Multi-type extraction chain ──

    [Fact]
    public void MultiTypeExtractionChain_Generated_vs_Boxed()
    {
        var keys = MakeSampleKeys(1);
        var genDict = new Dictionary<string, TypedData>
        {
            [keys[0]] = (TypedData)42
        };
        var boxedDict = new Dictionary<string, object>
        {
            [keys[0]] = 42
        };

        var genBest = TimeSpan.MaxValue;
        var boxedBest = TimeSpan.MaxValue;

        for (var round = 0; round < _warmupRounds + _timedRounds; round++)
        {
            var genOk = 0;
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < _readIterations; i++)
            {
                genDict.TryGetValue(keys[0], out var td);
                if (td.TryGetSingle(out _)) genOk++;
                else if (td.TryGetInt32(out _)) genOk++;
                else if (td.TryGetInt64(out _)) genOk++;
                else if (td.TryGetDouble(out _)) genOk++;
            }
            sw.Stop();

            var boxedOk = 0;
            var sw2 = Stopwatch.StartNew();
            for (var i = 0; i < _readIterations; i++)
            {
                boxedDict.TryGetValue(keys[0], out var obj);
                if (obj is float) boxedOk++;
                else if (obj is int) boxedOk++;
                else if (obj is long) boxedOk++;
                else if (obj is double) boxedOk++;
            }
            sw2.Stop();

            Assert.Equal(genOk, boxedOk);

            if (round >= _warmupRounds)
            {
                if (sw.Elapsed < genBest) genBest = sw.Elapsed;
                if (sw2.Elapsed < boxedBest) boxedBest = sw2.Elapsed;
            }
        }

        var (genAlloc, boxedAlloc) = MeasureChainAlloc(genDict, boxedDict, keys[0]);

        _perf.CompareTable(
            $"Numeric coercion chain (float→int→long→double, int payload, {_readIterations:N0} iters, min of {_timedRounds})",
            "TypedData", "Boxed",
            new List<(string, int, TimeSpan, long, TimeSpan, long)>
            {
                ("NumChain", _readIterations, genBest, genAlloc, boxedBest, boxedAlloc)
            });

        AssertInCap("Numeric chain", genBest);
    }

    // ─── Scenario 4: Observer Notify ──

    [Fact]
    public void ObserverNotify_Generated_vs_Boxed()
    {
        const int poolSize = 1 << 12;
        const int poolMask = poolSize - 1;

        // Build fixed-size pools so both sides touch memory through the same
        // bitmask addressing pattern. Gen side: (TypedData old, TypedData new).
        // Boxed side: (object? old, object? new). Both sides do a type-check
        // on the 'new' value — TryGetString on gen, is string on boxed.
        var genOldPool = new TypedData[poolSize];
        var genNewPool = new TypedData[poolSize];
        var boxedOldPool = new object?[poolSize];
        var boxedNewPool = new object?[poolSize];
        var stringPayload = "intent_attack";

        for (var i = 0; i < poolSize; i++)
        {
            // 80% string, 20% default/null — mimics real observer workload
            // where most notifications carry a string intent.
            if (i % 5 != 0)
            {
                genOldPool[i] = new TypedData(TypedData.KindMap.String, 0, stringPayload);
                genNewPool[i] = new TypedData(TypedData.KindMap.String, 0, stringPayload);
                boxedOldPool[i] = stringPayload;
                boxedNewPool[i] = stringPayload;
            }
            else
            {
                genOldPool[i] = default;
                genNewPool[i] = default;
                boxedOldPool[i] = null;
                boxedNewPool[i] = null;
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
                var idx = i & poolMask;
                _ = genOldPool[idx];          // "pass old value"
                var ok = genNewPool[idx].TryGetString(out _);  // check new value
                if (ok) genHits++;
            }
            sw.Stop();

            var boxedHits = 0;
            var sw2 = Stopwatch.StartNew();
            for (var i = 0; i < _readIterations; i++)
            {
                var idx = i & poolMask;
                _ = boxedOldPool[idx];   // "pass old value"
                if (boxedNewPool[idx] is string) boxedHits++;  // check new value
            }
            sw2.Stop();

            Assert.Equal(genHits, boxedHits);

            if (round >= _warmupRounds)
            {
                if (sw.Elapsed < genBest) genBest = sw.Elapsed;
                if (sw2.Elapsed < boxedBest) boxedBest = sw2.Elapsed;
            }
        }

        var (genAlloc, boxedAlloc) = MeasureObserverAlloc(genOldPool, genNewPool, boxedOldPool, boxedNewPool);

        _perf.CompareTable(
            $"Observer notify (pool-based, {_readIterations:N0} iters, min of {_timedRounds})",
            "TypedData", "Boxed",
            new List<(string, int, TimeSpan, long, TimeSpan, long)>
            {
                ("Observer", _readIterations, genBest, genAlloc, boxedBest, boxedAlloc)
            });

        AssertInCap("Observer notify", genBest);
    }

    // ─── Scenario 5: Heterogeneous dict iteration ──

    [Fact]
    public void HeterogeneousDictIteration_GeneratedData_vs_BoxedDict()
    {
        var keys = MakeSampleKeys(_dictSize);
        var genDict = FillTypedDataDict(keys);
        var boxedDict = FillBoxedDict(keys);

        var genBest = TimeSpan.MaxValue;
        var boxedBest = TimeSpan.MaxValue;

        for (var round = 0; round < _warmupRounds + _timedRounds; round++)
        {
            object? dummy = null;
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < _iterateIterations; i++)
            {
                foreach (var kv in genDict)
                {
                    dummy = TypedDataObjectConverter.ToObject(kv.Value);
                }
            }
            sw.Stop();

            var sw2 = Stopwatch.StartNew();
            for (var i = 0; i < _iterateIterations; i++)
            {
                foreach (var kv in boxedDict)
                {
                    dummy = kv.Value;
                }
            }
            sw2.Stop();

            GC.KeepAlive(dummy);

            if (round >= _warmupRounds)
            {
                if (sw.Elapsed < genBest) genBest = sw.Elapsed;
                if (sw2.Elapsed < boxedBest) boxedBest = sw2.Elapsed;
            }
        }

        var (genAlloc, boxedAlloc) = MeasureHeteroAlloc(genDict, boxedDict);

        var total = _iterateIterations * _dictSize;
        _perf.CompareTable(
            $"Heterogeneous dict iterate (ToObject vs plain object, {total:N0} reads, min of {_timedRounds})",
            "TypedData", "Boxed",
            new List<(string, int, TimeSpan, long, TimeSpan, long)>
            {
                ("Hetero", total, genBest, genAlloc, boxedBest, boxedAlloc)
            });

        AssertInCap("Heterogeneous dict iterate", genBest);
    }

    // ─── Allocation measurement ───────────────────────────────────

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (long gen, long boxed) MeasureDictReadAlloc(
        Dictionary<string, TypedData> genDict, Dictionary<string, object> boxedDict,
        string[] keys, Func<TypedData, bool> genCheck, string typeKey)
    {
        var sink = 0;
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < _readIterations; i++)
        {
            var key = keys[i % keys.Length];
            if (genDict.TryGetValue(key, out var td) && genCheck(td))
                sink++;
        }
        var gen = GC.GetAllocatedBytesForCurrentThread() - start;

        start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < _readIterations; i++)
        {
            var key = keys[i % keys.Length];
            if (boxedDict.TryGetValue(key, out var obj) && MatchesType(obj, typeKey))
                sink++;
        }
        var boxed = GC.GetAllocatedBytesForCurrentThread() - start;
        GC.KeepAlive(sink);
        return (gen, boxed);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (long gen, long boxed) MeasureDictWriteAlloc<T>(
        string[] keys, T[] samples, Func<T, TypedData> makeGen)
    {
        var start = GC.GetAllocatedBytesForCurrentThread();
        var genDict = new Dictionary<string, TypedData>(_writeIterations, StringComparer.Ordinal);
        for (var i = 0; i < _writeIterations; i++)
            genDict[keys[i % _sampleCount]] = makeGen(samples[i & _sampleMask]);
        var gen = GC.GetAllocatedBytesForCurrentThread() - start;

        start = GC.GetAllocatedBytesForCurrentThread();
        var boxedDict = new Dictionary<string, object>(_writeIterations, StringComparer.Ordinal);
        for (var i = 0; i < _writeIterations; i++)
            boxedDict[keys[i % _sampleCount]] = samples[i & _sampleMask]!;
        var boxed = GC.GetAllocatedBytesForCurrentThread() - start;
        GC.KeepAlive(genDict);
        GC.KeepAlive(boxedDict);
        return (gen, boxed);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (long gen, long boxed) MeasureChainAlloc(
        Dictionary<string, TypedData> genDict, Dictionary<string, object> boxedDict, string key)
    {
        var sink = 0;
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < _readIterations; i++)
        {
            genDict.TryGetValue(key, out var td);
            if (td.TryGetSingle(out _)) sink++;
            else if (td.TryGetInt32(out _)) sink++;
            else if (td.TryGetInt64(out _)) sink++;
            else if (td.TryGetDouble(out _)) sink++;
        }
        var gen = GC.GetAllocatedBytesForCurrentThread() - start;

        start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < _readIterations; i++)
        {
            boxedDict.TryGetValue(key, out var obj);
            if (obj is float) sink++;
            else if (obj is int) sink++;
            else if (obj is long) sink++;
            else if (obj is double) sink++;
        }
        var boxed = GC.GetAllocatedBytesForCurrentThread() - start;
        GC.KeepAlive(sink);
        return (gen, boxed);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (long gen, long boxed) MeasureObserverAlloc(
        TypedData[] genOldPool, TypedData[] genNewPool,
        object?[] boxedOldPool, object?[] boxedNewPool)
    {
        const int poolMask = (1 << 12) - 1;
        var sink = 0;
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < _readIterations; i++)
        {
            var idx = i & poolMask;
            _ = genOldPool[idx];
            if (genNewPool[idx].TryGetString(out _))
                sink++;
        }
        var gen = GC.GetAllocatedBytesForCurrentThread() - start;

        start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < _readIterations; i++)
        {
            var idx = i & poolMask;
            _ = boxedOldPool[idx];
            if (boxedNewPool[idx] is string)
                sink++;
        }
        var boxed = GC.GetAllocatedBytesForCurrentThread() - start;
        GC.KeepAlive(sink);
        return (gen, boxed);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (long gen, long boxed) MeasureHeteroAlloc(
        Dictionary<string, TypedData> genDict, Dictionary<string, object> boxedDict)
    {
        object? dummy = null;
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < _iterateIterations; i++)
            foreach (var kv in genDict)
                dummy = TypedDataObjectConverter.ToObject(kv.Value);
        var gen = GC.GetAllocatedBytesForCurrentThread() - start;

        start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < _iterateIterations; i++)
            foreach (var kv in boxedDict)
                dummy = kv.Value;
        var boxed = GC.GetAllocatedBytesForCurrentThread() - start;
        GC.KeepAlive(dummy);
        return (gen, boxed);
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private static Dictionary<string, TypedData> FillTypedDataDict(string[] keys)
    {
        var dict = new Dictionary<string, TypedData>(keys.Length, StringComparer.Ordinal);
        for (var i = 0; i < keys.Length; i++)
        {
            dict[keys[i]] = MakeTypedData(i);
        }
        return dict;
    }

    private static Dictionary<string, object> FillBoxedDict(string[] keys)
    {
        var dict = new Dictionary<string, object>(keys.Length, StringComparer.Ordinal);
        for (var i = 0; i < keys.Length; i++)
        {
            dict[keys[i]] = MakeBoxedValue(i);
        }
        return dict;
    }

    private static TypedData MakeTypedData(int i)
    {
        var typeIndex = i % 5;
        return typeIndex switch
        {
            0 => (TypedData)i,
            1 => (TypedData)(float)i,
            2 => (TypedData)(i % 2 == 0),
            3 => new TypedData(TypedData.KindMap.String, 0, "s_" + i),
            _ => (TypedData)(double)i,
        };
    }

    private static object MakeBoxedValue(int i)
    {
        var typeIndex = i % 5;
        return typeIndex switch
        {
            0 => (object)i,
            1 => (object)(float)i,
            2 => (object)(i % 2 == 0),
            3 => (object)("s_" + i),
            _ => (object)(double)i,
        };
    }

    private static bool MatchesType(object obj, string typeKey)
    {
        return typeKey switch
        {
            "string" => obj is string,
            "int" => obj is int,
            "float" => obj is float,
            "bool" => obj is bool,
            "double" => obj is double,
            _ => throw new ArgumentException(typeKey),
        };
    }

    private static string[] MakeSampleKeys(int count)
    {
        var keys = new string[count];
        for (var i = 0; i < count; i++)
            keys[i] = "key_" + i;
        return keys;
    }

    private static T[] MakeSamples<T>(Func<int, T> factory)
    {
        var arr = new T[_sampleCount];
        for (var i = 0; i < _sampleCount; i++)
            arr[i] = factory(i);
        return arr;
    }

    private static void AssertInCap(string label, TimeSpan elapsed)
    {
        Assert.True(elapsed < _perBenchmarkCap,
            $"{label}: {elapsed.TotalMilliseconds:F2}ms exceeds {_perBenchmarkCap.TotalSeconds:F0}s cap");
    }
}
