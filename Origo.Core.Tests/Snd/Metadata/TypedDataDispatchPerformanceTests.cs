using System;
using System.Collections.Generic;
using System.Diagnostics;
using Origo.Core.Snd.Metadata;
using Origo.Core.Tests.TestSupport;
using Xunit;

namespace Origo.Core.Tests.Snd.Metadata;

public class TypedDataDispatchPerformanceTests
{
    private const int ReadCount = 1_000_000;

    [Fact]
    public void KindCheckReadVsIsTPattern_Int32()
    {
        var dict = new Dictionary<string, TypedData>();
        for (var i = 0; i < ReadCount; i++)
            dict[$"key_{i}"] = (TypedData)i;

        GC.Collect();
        GC.WaitForPendingFinalizers();

        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        var sumKind = 0;
        for (var i = 0; i < ReadCount; i++)
        {
            if (dict.TryGetValue($"key_{i}", out var td) && td.TryGetInt32(out var v))
                sumKind += v;
        }
        sw.Stop();
        long allocKind = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
        var timeKind = sw.Elapsed;

        GC.Collect();
        GC.WaitForPendingFinalizers();

        allocBefore = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        var sumIsT = 0;
        for (var i = 0; i < ReadCount; i++)
        {
            if (dict.TryGetValue($"key_{i}", out var td) && td.Data is int v)
                sumIsT += v;
        }
        sw.Stop();
        long allocIsT = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
        var timeIsT = sw.Elapsed;

        Assert.Equal(sumKind, sumIsT);

        PerfReporter.ToConsole.Compare(
            "Int32 Read: Kind-based TryGet vs 'is T' pattern",
            "TryGetInt32 (kind)", ReadCount, timeKind, allocKind,
            "Data is int (is T)", ReadCount, timeIsT, allocIsT);
    }

    [Fact]
    public void FactoryTryExtractVsIsT_Int32()
    {
        var td = (TypedData)42;

        GC.Collect();
        GC.WaitForPendingFinalizers();

        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        var sumFactory = 0;
        for (var i = 0; i < ReadCount; i++)
        {
            if (TypedDataFactory<int>.TryExtract(td, out var v))
                sumFactory += v;
        }
        sw.Stop();
        long allocFactory = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
        var timeFactory = sw.Elapsed;

        GC.Collect();
        GC.WaitForPendingFinalizers();

        allocBefore = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        var sumIsT = 0;
        for (var i = 0; i < ReadCount; i++)
        {
            if (td.Data is int v)
                sumIsT += v;
        }
        sw.Stop();
        long allocIsT = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
        var timeIsT = sw.Elapsed;

        Assert.Equal(ReadCount * 42, sumFactory);
        Assert.Equal(sumFactory, sumIsT);

        PerfReporter.ToConsole.Compare(
            "Int32 Extract: TypedDataFactory<T>.TryExtract vs 'is T' pattern",
            "Factory.TryExtract (kind)", ReadCount, timeFactory, allocFactory,
            "Data is int (is T)", ReadCount, timeIsT, allocIsT);
    }

    [Fact]
    public void ObjectConverterToObject_SwitchVsIsT()
    {
        var td = (TypedData)99;

        GC.Collect();
        GC.WaitForPendingFinalizers();

        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < ReadCount; i++)
            _ = TypedDataObjectConverter.ToObject(td);
        sw.Stop();
        long allocSwitch = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
        var timeSwitch = sw.Elapsed;

        GC.Collect();
        GC.WaitForPendingFinalizers();

        allocBefore = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        for (var i = 0; i < ReadCount; i++)
            _ = td.Data;
        sw.Stop();
        long allocData = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
        var timeData = sw.Elapsed;

        PerfReporter.ToConsole.Compare(
            "ToObject: Switch-based dispatch vs generic Data property",
            "ToObject (switch)", ReadCount, timeSwitch, allocSwitch,
            "Data property", ReadCount, timeData, allocData);
    }

    [Fact]
    public void Write_PrimitiveVsBoxedWrapper()
    {
        const int count = 1_000_000;

        GC.Collect();
        GC.WaitForPendingFinalizers();

        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < count; i++)
            _ = (TypedData)i;
        sw.Stop();
        long allocStruct = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
        var timeStruct = sw.Elapsed;

        GC.Collect();
        GC.WaitForPendingFinalizers();

        allocBefore = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        for (var i = 0; i < count; i++)
            _ = new OldTypedData(typeof(int), i);
        sw.Stop();
        long allocClass = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
        var timeClass = sw.Elapsed;

        PerfReporter.ToConsole.Compare(
            "Write: TypedData struct vs OldTypedData class",
            "TypedData struct", count, timeStruct, allocStruct,
            "OldTypedData class", count, timeClass, allocClass);
    }

    [Fact]
    public void MixedDispatch_MultipleTypes()
    {
        const int iterations = 200_000;
        var types = new[] { typeof(int), typeof(float), typeof(bool), typeof(string), typeof(double) };
        var dict = new Dictionary<string, TypedData>();
        for (var i = 0; i < iterations; i++)
        {
            var typeIdx = i % types.Length;
            dict[$"key_{i}"] = typeIdx switch
            {
                0 => (TypedData)i,
                1 => (TypedData)(float)i,
                2 => (TypedData)(i % 2 == 0),
                3 => TypedData.FromObject(typeof(string), $"s_{i}"),
                4 => (TypedData)(double)i,
                _ => default
            };
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();

        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            if (dict.TryGetValue($"key_{i}", out var td))
            {
                td.TryGetInt32(out var iv);
                td.TryGetSingle(out var fv);
                td.TryGetBoolean(out var bv);
                td.TryGetString(out var sv);
                td.TryGetDouble(out var dv);
            }
        }
        sw.Stop();
        long allocKind = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
        var timeKind = sw.Elapsed;

        GC.Collect();
        GC.WaitForPendingFinalizers();

        allocBefore = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        for (var i = 0; i < iterations; i++)
        {
            if (dict.TryGetValue($"key_{i}", out var td))
            {
                if (td.Data is int iv) _ = iv;
                if (td.Data is float fv) _ = fv;
                if (td.Data is bool bv) _ = bv;
                if (td.Data is string sv) _ = sv;
                if (td.Data is double dv) _ = dv;
            }
        }
        sw.Stop();
        long allocIsT = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
        var timeIsT = sw.Elapsed;

        PerfReporter.ToConsole.Compare(
            "Mixed Dispatch (int/float/bool/string/double): Kind-based vs is T",
            "Kind-based (TryGet)", iterations * 5, timeKind, allocKind,
            "is T pattern", iterations * 5, timeIsT, allocIsT);
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
}
