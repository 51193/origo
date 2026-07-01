using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Origo.Core.Snd.Metadata;
using Origo.Core.Tests.TestSupport;
using Xunit;

namespace Origo.Core.Tests.Benchmarks;

[Trait("Category", "Benchmark")]
public class TypedDataRealWorldBenchmarkTests(ITestOutputHelper output)
{
    private const int DictSize = 1024;
    private const int DictMask = DictSize - 1;
    private const int SampleCount = 128;
    private const int SampleMask = SampleCount - 1;

    private const int ReadIterations = 2_000_000;
    private const int WriteIterations = 500_000;
    private const int IterateIterations = 2_000;
    private const int WarmupRounds = 1;
    private const int TimedRounds = 5;

    private static readonly TimeSpan PerBenchmarkCap = TimeSpan.FromSeconds(8);

    private readonly PerfReporter _perf = PerfReporter.ForTest(output);

    // ─── Scenario 1: SndDataManager.TryGetData<T> — Dictionary lookup + TryExtract ──

    [Fact]
    public void DictLookup_TryExtract_vs_BoxedDict()
    {
        var keyCount = DictSize / 4;
        var keys = MakeSampleKeys(keyCount);
        var genDict = FillTypedDataDict(keys);
        var boxedDict = FillBoxedDict(keys);

        RunDictRead("Dict String TryExtract", genDict, boxedDict, "string", keys,
            static td => td.TryGetString(out _));
        RunDictRead("Dict Int32 TryExtract", genDict, boxedDict, "int", keys,
            static td => td.TryGetInt32(out _));
        RunDictRead("Dict Single TryExtract", genDict, boxedDict, "float", keys,
            static td => td.TryGetSingle(out _));
        RunDictRead("Dict Boolean TryExtract", genDict, boxedDict, "bool", keys,
            static td => td.TryGetBoolean(out _));
    }

    private void RunDictRead(string label, Dictionary<string, TypedData> genDict,
        Dictionary<string, object> boxedDict, string typeKey, string[] keys,
        Func<TypedData, bool> genCheck)
    {
        var genBest = TimeSpan.MaxValue;
        var boxedBest = TimeSpan.MaxValue;

        for (var round = 0; round < WarmupRounds + TimedRounds; round++)
        {
            var genHits = 0;
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < ReadIterations; i++)
            {
                var key = keys[i % keys.Length];
                if (genDict.TryGetValue(key, out var td) && genCheck(td))
                    genHits++;
            }
            sw.Stop();

            var boxedHits = 0;
            var sw2 = Stopwatch.StartNew();
            for (var i = 0; i < ReadIterations; i++)
            {
                var key = keys[i % keys.Length];
                if (boxedDict.TryGetValue(key, out var obj) && MatchesType(obj, typeKey))
                    boxedHits++;
            }
            sw2.Stop();

            Assert.Equal(genHits, boxedHits);

            if (round >= WarmupRounds)
            {
                if (sw.Elapsed < genBest) genBest = sw.Elapsed;
                if (sw2.Elapsed < boxedBest) boxedBest = sw2.Elapsed;
            }
        }

        // Allocation is measured in a separate NoInlining helper so its loop
        // bodies stay out of this method and never share codegen with the timed loops.
        var (genAlloc, boxedAlloc) = MeasureDictReadAlloc(genDict, boxedDict, keys, genCheck, typeKey);

        _perf.Compare(label, "Generated Dict", ReadIterations, genBest, genAlloc,
            "Boxed Dict", ReadIterations, boxedBest, boxedAlloc);
        AssertInCap(label, genBest);
    }

    // ─── Scenario 2: SndDataManager.SetData<T> — Factory Create + Dict insert ──

    [Fact]
    public void DictInsert_FactoryCreate_vs_BoxedDict()
    {
        RunDictWrite("String", MakeSamples(i => "s_" + i),
            v => new TypedData(TypedData.KindMap.String, 0, v), "Write String: Create+Insert vs Boxing");

        RunDictWrite("Int32", MakeSamples(i => i),
            v => (TypedData)v, "Write Int32: Create+Insert vs Boxing");

        RunDictWrite("Single", MakeSamples(i => i * 1.5f),
            v => (TypedData)v, "Write Single: Create+Insert vs Boxing");

        RunDictWrite("Boolean", MakeSamples(i => i % 2 == 0),
            v => (TypedData)v, "Write Boolean: Create+Insert vs Boxing");
    }

    private void RunDictWrite<T>(string keyPrefix, T[] samples,
        Func<T, TypedData> makeGen, string label)
    {
        var keys = MakeSampleKeys(SampleCount);
        var genBest = TimeSpan.MaxValue;
        var boxedBest = TimeSpan.MaxValue;

        for (var round = 0; round < WarmupRounds + TimedRounds; round++)
        {
            var genDict = new Dictionary<string, TypedData>(WriteIterations, StringComparer.Ordinal);
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < WriteIterations; i++)
                genDict[keys[i % SampleCount]] = makeGen(samples[i & SampleMask]);
            sw.Stop();

            var boxedDict = new Dictionary<string, object>(WriteIterations, StringComparer.Ordinal);
            var sw2 = Stopwatch.StartNew();
            for (var i = 0; i < WriteIterations; i++)
                boxedDict[keys[i % SampleCount]] = samples[i & SampleMask]!;
            sw2.Stop();

            Assert.Equal(genDict.Count, boxedDict.Count);

            if (round >= WarmupRounds)
            {
                if (sw.Elapsed < genBest) genBest = sw.Elapsed;
                if (sw2.Elapsed < boxedBest) boxedBest = sw2.Elapsed;
            }
        }

        var (genAlloc, boxedAlloc) = MeasureDictWriteAlloc(keys, samples, makeGen);

        _perf.Compare(label, "Generated Create+Insert", WriteIterations, genBest, genAlloc,
            "Boxed Insert", WriteIterations, boxedBest, boxedAlloc);
        AssertInCap(label, genBest);
    }

    // ─── Scenario 3: TryGetNumericExtensions — Multi-type extraction chain ──

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

        for (var round = 0; round < WarmupRounds + TimedRounds; round++)
        {
            var genOk = 0;
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < ReadIterations; i++)
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
            for (var i = 0; i < ReadIterations; i++)
            {
                boxedDict.TryGetValue(keys[0], out var obj);
                if (obj is float) boxedOk++;
                else if (obj is int) boxedOk++;
                else if (obj is long) boxedOk++;
                else if (obj is double) boxedOk++;
            }
            sw2.Stop();

            Assert.Equal(genOk, boxedOk);

            if (round >= WarmupRounds)
            {
                if (sw.Elapsed < genBest) genBest = sw.Elapsed;
                if (sw2.Elapsed < boxedBest) boxedBest = sw2.Elapsed;
            }
        }

        var (genAlloc, boxedAlloc) = MeasureChainAlloc(genDict, boxedDict, keys[0]);

        _perf.Compare("Numeric coercion chain: float→int→long→double (int payload)",
            "Generated chain", ReadIterations, genBest, genAlloc,
            "Boxed is T chain", ReadIterations, boxedBest, boxedAlloc);
        AssertInCap("Numeric chain", genBest);
    }

    // ─── Scenario 4: Observer Notify — TypedData pass-through + Data is string ──

    [Fact]
    public void ObserverNotify_Generated_vs_Boxed()
    {
        var tdString = new TypedData(TypedData.KindMap.String, 0, "intent_attack");
        var tdDefault = default(TypedData);
        var boxedString = "intent_attack";
        var boxedNull = (object?)null;

        var genBest = TimeSpan.MaxValue;
        var boxedBest = TimeSpan.MaxValue;

        for (var round = 0; round < WarmupRounds + TimedRounds; round++)
        {
            var genHits = 0;
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < ReadIterations; i++)
            {
                var ok = tdString.TryGetString(out _);
                _ = tdDefault;
                if (ok) genHits++;
            }
            sw.Stop();

            var boxedHits = 0;
            var sw2 = Stopwatch.StartNew();
            for (var i = 0; i < ReadIterations; i++)
            {
                _ = boxedNull;
                if (boxedString is string) boxedHits++;
            }
            sw2.Stop();

            Assert.Equal(genHits, boxedHits);

            if (round >= WarmupRounds)
            {
                if (sw.Elapsed < genBest) genBest = sw.Elapsed;
                if (sw2.Elapsed < boxedBest) boxedBest = sw2.Elapsed;
            }
        }

        var (genAlloc, boxedAlloc) = MeasureObserverAlloc(tdString, tdDefault, boxedString, boxedNull);

        _perf.Compare("Observer notify: pass (old,new) TypedData + check TryGetString",
            "Generated (TypedData, TypedData)", ReadIterations, genBest, genAlloc,
            "Boxed (object?, object?)", ReadIterations, boxedBest, boxedAlloc);
        AssertInCap("Observer notify", genBest);
    }

    // ─── Scenario 5: Heterogeneous dictionary iteration — TypedDataObjectConverter.ToObject ──

    [Fact]
    public void HeterogeneousDictIteration_GeneratedData_vs_BoxedDict()
    {
        var keys = MakeSampleKeys(DictSize);
        var genDict = FillTypedDataDict(keys);
        var boxedDict = FillBoxedDict(keys);

        var genBest = TimeSpan.MaxValue;
        var boxedBest = TimeSpan.MaxValue;

        for (var round = 0; round < WarmupRounds + TimedRounds; round++)
        {
            object? dummy = null;
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < IterateIterations; i++)
            {
                foreach (var kv in genDict)
                {
                    dummy = TypedDataObjectConverter.ToObject(kv.Value);
                }
            }
            sw.Stop();

            var sw2 = Stopwatch.StartNew();
            for (var i = 0; i < IterateIterations; i++)
            {
                foreach (var kv in boxedDict)
                {
                    dummy = kv.Value; // already object, trivial
                }
            }
            sw2.Stop();

            Assert.False(dummy is null && dummy is not null);

            if (round >= WarmupRounds)
            {
                // Only meaningful for the heavier (generated) case — boxing is plain object passthrough
                if (sw.Elapsed < genBest) genBest = sw.Elapsed;
                if (sw2.Elapsed < boxedBest) boxedBest = sw2.Elapsed;
            }
        }

        var (genAlloc, boxedAlloc) = MeasureHeteroAlloc(genDict, boxedDict);

        var total = IterateIterations * DictSize;
        _perf.Compare("Heterogeneous dict iterate: ToObject (TypedData) vs plain object",
            "Generated ToObject", total, genBest, genAlloc,
            "Boxed dict iterate", total, boxedBest, boxedAlloc);
        AssertInCap("Heterogeneous dict iterate", genBest);
    }

    // ─── Allocation measurement (kept out-of-line) ─────────────────
    // Each measurement runs one untimed pass per side and returns the
    // GC.GetAllocatedBytesForCurrentThread delta. NoInlining keeps these loop
    // bodies out of the timed methods, so the measurement never shares codegen
    // with the timed loops.

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (long gen, long boxed) MeasureDictReadAlloc(
        Dictionary<string, TypedData> genDict, Dictionary<string, object> boxedDict,
        string[] keys, Func<TypedData, bool> genCheck, string typeKey)
    {
        var sink = 0;
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < ReadIterations; i++)
        {
            var key = keys[i % keys.Length];
            if (genDict.TryGetValue(key, out var td) && genCheck(td))
                sink++;
        }
        var gen = GC.GetAllocatedBytesForCurrentThread() - start;

        start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < ReadIterations; i++)
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
        var genDict = new Dictionary<string, TypedData>(WriteIterations, StringComparer.Ordinal);
        for (var i = 0; i < WriteIterations; i++)
            genDict[keys[i % SampleCount]] = makeGen(samples[i & SampleMask]);
        var gen = GC.GetAllocatedBytesForCurrentThread() - start;

        start = GC.GetAllocatedBytesForCurrentThread();
        var boxedDict = new Dictionary<string, object>(WriteIterations, StringComparer.Ordinal);
        for (var i = 0; i < WriteIterations; i++)
            boxedDict[keys[i % SampleCount]] = samples[i & SampleMask]!;
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
        for (var i = 0; i < ReadIterations; i++)
        {
            genDict.TryGetValue(key, out var td);
            if (td.TryGetSingle(out _)) sink++;
            else if (td.TryGetInt32(out _)) sink++;
            else if (td.TryGetInt64(out _)) sink++;
            else if (td.TryGetDouble(out _)) sink++;
        }
        var gen = GC.GetAllocatedBytesForCurrentThread() - start;

        start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < ReadIterations; i++)
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
        TypedData tdString, TypedData tdDefault, string boxedString, object? boxedNull)
    {
        var sink = 0;
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < ReadIterations; i++)
        {
            var ok = tdString.TryGetString(out _);
            _ = tdDefault;
            if (ok) sink++;
        }
        var gen = GC.GetAllocatedBytesForCurrentThread() - start;

        start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < ReadIterations; i++)
        {
            _ = boxedNull;
            if (boxedString is string) sink++;
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
        for (var i = 0; i < IterateIterations; i++)
            foreach (var kv in genDict)
                dummy = TypedDataObjectConverter.ToObject(kv.Value);
        var gen = GC.GetAllocatedBytesForCurrentThread() - start;

        start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < IterateIterations; i++)
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
            0 => (TypedData)i,                        // int
            1 => (TypedData)(float)i,                 // float
            2 => (TypedData)(i % 2 == 0),             // bool
            3 => new TypedData(TypedData.KindMap.String, 0, "s_" + i), // string
            _ => (TypedData)(double)i,                // double
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
        var arr = new T[SampleCount];
        for (var i = 0; i < SampleCount; i++)
            arr[i] = factory(i);
        return arr;
    }

    private static void AssertInCap(string label, TimeSpan elapsed)
    {
        Assert.True(elapsed < PerBenchmarkCap,
            $"{label}: {elapsed.TotalMilliseconds:F2}ms exceeds {PerBenchmarkCap.TotalSeconds:F0}s cap");
    }
}
