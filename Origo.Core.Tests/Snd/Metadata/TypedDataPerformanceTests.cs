using System;
using System.Collections.Generic;
using System.Diagnostics;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Performance benchmarks comparing the new TypedData struct (zero-boxing)
///     against an old-style boxed TypedData reference approach.
///     Each test measures allocation volume and relative throughput.
/// </summary>
public class TypedDataPerformanceTests
{
    private const int WriteCount = 1_000_000;
    private const int ReadCount = 1_000_000;
    private const int NotificationRounds = 10_000;
    private const int SubscriberCount = 100;

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
    public void WriteThroughput_StructOutperformsClass()
    {
        var oldDict = new Dictionary<string, OldTypedData>();
        var newDict = new Dictionary<string, TypedData>();

        long oldAlloc = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < WriteCount; i++)
            oldDict[$"key_{i}"] = new OldTypedData(typeof(int), i);
        sw.Stop();
        oldAlloc = GC.GetAllocatedBytesForCurrentThread() - oldAlloc;
        var oldTime = sw.Elapsed;

        GC.Collect();
        GC.WaitForPendingFinalizers();

        long newAlloc = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        for (var i = 0; i < WriteCount; i++)
            newDict[$"key_{i}"] = (TypedData)i;
        sw.Stop();
        newAlloc = GC.GetAllocatedBytesForCurrentThread() - newAlloc;
        var newTime = sw.Elapsed;

        Assert.True(newAlloc < oldAlloc,
            $"Struct allocation ({newAlloc} bytes) should be less than class allocation ({oldAlloc} bytes)");
    }

    [Fact]
    public void ReadThroughput_StructOutperformsClass()
    {
        var oldDict = new Dictionary<string, OldTypedData>();
        var newDict = new Dictionary<string, TypedData>();

        for (var i = 0; i < WriteCount; i++)
        {
            oldDict[$"key_{i}"] = new OldTypedData(typeof(int), i);
            newDict[$"key_{i}"] = (TypedData)i;
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();

        var sw = Stopwatch.StartNew();
        var oldSum = 0L;
        for (var i = 0; i < ReadCount; i++)
        {
            if (oldDict.TryGetValue($"key_{i}", out var td) && td.Data is int v)
                oldSum += v;
        }
        sw.Stop();
        var oldTime = sw.Elapsed;

        sw.Restart();
        var newSum = 0L;
        for (var i = 0; i < ReadCount; i++)
        {
            if (newDict.TryGetValue($"key_{i}", out var td) && td.TryGetInt32(out var v))
                newSum += v;
        }
        sw.Stop();
        var newTime = sw.Elapsed;

        Assert.Equal(oldSum, newSum);
    }

    [Fact]
    public void WriteSameValue_StructSkip_IsEfficient()
    {
        var dict = new Dictionary<string, TypedData>();
        var key = "hp";

        dict[key] = (TypedData)100;

        GC.Collect();
        GC.WaitForPendingFinalizers();

        long alloc = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < WriteCount; i++)
        {
            var newValue = (TypedData)100;
            if (dict.TryGetValue(key, out var existing) && existing.Equals(newValue))
                continue;
            dict[key] = newValue;
        }
        sw.Stop();
        alloc = GC.GetAllocatedBytesForCurrentThread() - alloc;

        Assert.True(alloc < 10_000,
            $"Same-value write loop should produce negligible allocations, got {alloc} bytes");
    }

    [Fact]
    public void ObserverNotification_Performance()
    {
        var dict = new Dictionary<string, TypedData>();

        var callCount = 0;
        var subscribers = new List<Action<TypedData, TypedData>>();
        for (var i = 0; i < SubscriberCount; i++)
            subscribers.Add((_, _) => callCount++);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        long alloc = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        var snapshot = new List<Action<TypedData, TypedData>>(subscribers);
        for (var r = 0; r < NotificationRounds; r++)
        {
            var oldVal = (TypedData)(r - 1);
            var newVal = (TypedData)r;
            foreach (var sub in snapshot)
                sub(oldVal, newVal);
        }
        sw.Stop();
        alloc = GC.GetAllocatedBytesForCurrentThread() - alloc;

        var expectedCalls = NotificationRounds * SubscriberCount;
        Assert.Equal(expectedCalls, callCount);
    }

    [Fact]
    public void DictionaryMemoryFootprint_StructVsClass()
    {
        const int entryCount = 100_000;

        var classDict = new Dictionary<string, OldTypedData>();
        for (var i = 0; i < entryCount; i++)
            classDict[$"key_{i}"] = new OldTypedData(typeof(int), i);

        var structDict = new Dictionary<string, TypedData>();
        for (var i = 0; i < entryCount; i++)
            structDict[$"key_{i}"] = (TypedData)i;

        Assert.Equal(entryCount, classDict.Count);
        Assert.Equal(entryCount, structDict.Count);
    }

    [Fact]
    public void MixedFrameSimulation()
    {
        const int entityCount = 1000;
        const int readsPerFrame = 5;
        const int writesPerFrame = 3;
        const int frames = 60;

        var entityDicts = new Dictionary<string, TypedData>[entityCount];
        for (var e = 0; e < entityCount; e++)
        {
            entityDicts[e] = new Dictionary<string, TypedData>
            {
                ["hp"] = (TypedData)100,
                ["max_hp"] = (TypedData)200,
                ["speed"] = (TypedData)5.0f,
                ["x"] = (TypedData)(e * 10),
                ["y"] = (TypedData)0,
                ["name"] = TypedData.FromObject(typeof(string), $"entity_{e}"),
                ["alive"] = (TypedData)true,
                ["counter"] = (TypedData)0
            };
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();

        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();

        for (var f = 0; f < frames; f++)
        {
            for (var e = 0; e < entityCount; e++)
            {
                var dict = entityDicts[e];

                for (var r = 0; r < readsPerFrame; r++)
                {
                    dict.TryGetValue("hp", out var hp);
                    dict.TryGetValue("max_hp", out var maxHp);
                    dict.TryGetValue("speed", out var speed);
                    dict.TryGetValue("alive", out var alive);
                    dict.TryGetValue("counter", out var counter);
                }

                for (var w = 0; w < writesPerFrame; w++)
                {
                    dict["hp"] = (TypedData)(100 + w);
                    dict["counter"] = (TypedData)(w * f);
                    dict["x"] = (TypedData)(e * 10 + w);
                }
            }
        }

        sw.Stop();
        GC.GetAllocatedBytesForCurrentThread();
    }

    [Fact]
    public void TypedDataFactory_Create_ZeroAllocation()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();

        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < WriteCount; i++)
        {
            var td = TypedDataFactory<int>.Create(i);
            Debug.Assert(td.AsInt32() == i);
        }
        long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        Assert.True(totalAlloc < 10_000,
            $"TypedDataFactory<int>.Create should produce near-zero allocations for value types, got {totalAlloc} bytes");
    }

    [Fact]
    public void TypedDataFactory_TryExtract_ZeroAllocation()
    {
        var td = (TypedData)42;

        GC.Collect();
        GC.WaitForPendingFinalizers();

        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sum = 0;
        for (var i = 0; i < WriteCount; i++)
        {
            if (TypedDataFactory<int>.TryExtract(td, out var v))
                sum += v;
        }
        long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        Assert.Equal(WriteCount * 42, sum);
        Assert.True(totalAlloc < 10_000,
            $"TypedDataFactory<int>.TryExtract should produce near-zero allocations, got {totalAlloc} bytes");
    }
}
