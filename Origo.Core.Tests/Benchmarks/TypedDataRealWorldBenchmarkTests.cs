using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Origo.Core.Snd.Metadata;
using Origo.Core.Tests.TestSupport;
using Xunit;

namespace Origo.Core.Tests.Benchmarks;

[Trait("Category", "Benchmark")]
public class TypedDataRealWorldBenchmarkTests
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

    private readonly PerfReporter _perf;

    public TypedDataRealWorldBenchmarkTests(ITestOutputHelper output)
    {
        _perf = PerfReporter.ForTest(output);
    }

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

        _perf.Compare(label, "Generated Dict", ReadIterations, genBest, 0,
            "Boxed Dict", ReadIterations, boxedBest, 0);
        AssertInCap(label, genBest);
    }

    // ─── Scenario 2: SndDataManager.SetData<T> — Factory Create + Dict insert ──

    [Fact]
    public void DictInsert_FactoryCreate_vs_BoxedDict()
    {
        RunDictWrite("String", MakeSamples(i => "s_" + i),
            v => TypedData.FromObject(typeof(string), v), "Write String: Create+Insert vs Boxing");

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

        _perf.Compare(label, "Generated Create+Insert", WriteIterations, genBest, 0,
            "Boxed Insert", WriteIterations, boxedBest, 0);
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

        _perf.Compare("Numeric coercion chain: float→int→long→double (int payload)",
            "Generated chain", ReadIterations, genBest, 0,
            "Boxed is T chain", ReadIterations, boxedBest, 0);
        AssertInCap("Numeric chain", genBest);
    }

    // ─── Scenario 4: Observer Notify — TypedData pass-through + Data is string ──

    [Fact]
    public void ObserverNotify_Generated_vs_Boxed()
    {
        var tdString = TypedData.FromObject(typeof(string), "intent_attack");
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

        _perf.Compare("Observer notify: pass (old,new) TypedData + check .Data is string",
            "Generated (TypedData, TypedData)", ReadIterations, genBest, 0,
            "Boxed (object?, object?)", ReadIterations, boxedBest, 0);
        AssertInCap("Observer notify", genBest);
    }

    // ─── Scenario 5: Heterogeneous dictionary iteration — .Data property ──

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
                    dummy = kv.Value.Data; // TypedDataObjectConverter.ToObject
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

        var total = IterateIterations * DictSize;
        _perf.Compare("Heterogeneous dict iterate: .Data (TypedData) vs plain object",
            "Generated .Data", total, genBest, 0,
            "Boxed dict iterate", total, boxedBest, 0);
        AssertInCap("Heterogeneous dict iterate", genBest);
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
            3 => TypedData.FromObject(typeof(string), "s_" + i), // string
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
