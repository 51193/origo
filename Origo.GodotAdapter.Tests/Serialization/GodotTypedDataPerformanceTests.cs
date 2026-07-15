using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;
using Origo.Core.Snd.Metadata;
using Origo.GodotAdapter.Snd;
using Origo.GodotAdapter;
using Origo.TestSupport;
using Xunit;

namespace Origo.GodotAdapter.Tests;

[Trait("Category", "Benchmark")]
public class GodotTypedDataPerformanceTests(ITestOutputHelper output)
{
    private const int _iterations = 200_000;
    private readonly PerfReporter _perf = PerfReporter.ForTest(output);

    static GodotTypedDataPerformanceTests()
    {
        _ = TypedDataInitializer.IsLoaded;
    }

    [Fact]
    public void WriteThroughput_Registered_Outperforms_Unregistered()
    {
        var v = new Vector3(1.0f, 2.0f, 3.0f);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        var allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < _iterations; i++)
            _ = new TypedData(130, 0, v);
        sw.Stop();
        var timeRegistered = sw.Elapsed;
        var allocRegistered = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        GC.Collect();
        GC.WaitForPendingFinalizers();

        allocBefore = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        for (var i = 0; i < _iterations; i++)
            _ = new TypedData(255, 0, v);
        sw.Stop();
        var timeUnregistered = sw.Elapsed;
        var allocUnregistered = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        _perf.CompareTable(
            "Godot Vector3 Write: Registered vs Unregistered",
            "Registered", "Unregistered",
            [
                ("Vector3", _iterations, timeRegistered, allocRegistered, timeUnregistered, allocUnregistered)
            ]);

        var tdReg = new TypedData(130, 0, v);
        var obj = TypedDataObjectConverter.ToObject(tdReg);
        Assert.IsType<Vector3>(obj);
        Assert.Equal(v, (Vector3)obj!);
    }

    [Fact]
    public void ReadThroughput_TryGetVector3_Outperforms_IsT()
    {
        var source = new Vector3(1.0f, 2.0f, 3.0f);
        var td = new TypedData(130, 0, source);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        var allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        var sumKind = 0.0f;
        for (var i = 0; i < _iterations; i++)
        {
            if (td.TryGetVector3(out var r)) sumKind += r.X;
        }
        sw.Stop();
        var timeKind = sw.Elapsed;
        var allocKind = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        GC.Collect();
        GC.WaitForPendingFinalizers();

        allocBefore = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        var sumIsT = 0.0f;
        for (var i = 0; i < _iterations; i++)
        {
            if (TypedDataObjectConverter.ToObject(td) is Vector3 r) sumIsT += r.X;
        }
        sw.Stop();
        var timeIsT = sw.Elapsed;
        var allocIsT = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        Assert.Equal(sumKind, sumIsT, 0.01f);

        _perf.CompareTable(
            "Godot Vector3 Read: TryGetVector3 vs Data is Vector3",
            "TryGet", "Data is T",
            [
                ("Vector3", _iterations, timeKind, allocKind, timeIsT, allocIsT)
            ]);
    }

    [Fact]
    public void ObjectConverter_ToObject_GodotSwitch_Outperforms_Data()
    {
        var v = new Vector3(1, 2, 3);
        var td = new TypedData(130, 0, v);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        var allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < _iterations; i++)
            _ = TypedDataObjectConverter.ToObject(td);
        sw.Stop();
        var timeSwitch = sw.Elapsed;
        var allocSwitch = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        GC.Collect();
        GC.WaitForPendingFinalizers();

        allocBefore = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        for (var i = 0; i < _iterations; i++)
            _ = TypedDataObjectConverter.ToObject(td);
        sw.Stop();
        var timeData = sw.Elapsed;
        var allocData = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        _perf.CompareTable(
            "Godot ToObject: Switch dispatch vs Data property",
            "Switch", "Data",
            [
                ("ToObject", _iterations, timeSwitch, allocSwitch, timeData, allocData)
            ]);

        var result = TypedDataObjectConverter.ToObject(td);
        Assert.IsType<Vector3>(result);
        Assert.Equal(v, (Vector3)result);
    }

    [Fact]
    public void ObjectConverter_FromObject_GodotSwitch_Outperforms_Fallback()
    {
        var v = new Color(0.2f, 0.4f, 0.6f, 0.8f);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        var allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < _iterations; i++)
            _ = TypedDataObjectConverter.FromObject(137, v);
        sw.Stop();
        var timeSwitch = sw.Elapsed;
        var allocSwitch = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        GC.Collect();
        GC.WaitForPendingFinalizers();

        allocBefore = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        for (var i = 0; i < _iterations; i++)
            _ = TypedDataObjectConverter.FromObject(255, v);
        sw.Stop();
        var timeFallback = sw.Elapsed;
        var allocFallback = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        _perf.CompareTable(
            "Godot FromObject: Kind-switch vs unregistered fallback",
            "FromObject", "Fallback",
            [
                ("Color", _iterations, timeSwitch, allocSwitch, timeFallback, allocFallback)
            ]);

        var (_, refReg) = TypedDataObjectConverter.FromObject(137, v);
        var (_, refUnreg) = TypedDataObjectConverter.FromObject(255, v);
        Assert.Equal(refReg, refUnreg);
    }

    [Fact]
    public void Factory_CreateExtract_Vector3_RegisteredVsUnregistered()
    {
        var v = new Vector3(5, 6, 7);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        var allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < _iterations; i++)
        {
            var created = TypedDataFactory<Vector3>.Create(v);
            TypedDataFactory<Vector3>.TryExtract(created, out _);
        }
        sw.Stop();
        var timeRegistered = sw.Elapsed;
        var allocRegistered = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        _perf.ReportTable(
            "Godot Vector3 Factory Create+Extract (kind-based path)",
            [
                ("Create+Extract", _iterations * 2, timeRegistered, allocRegistered)
            ]);

        var tdCreated = TypedDataFactory<Vector3>.Create(v);
        var extracted = TypedDataFactory<Vector3>.TryExtract(tdCreated, out var ev);
        Assert.True(extracted);
        Assert.Equal(v, ev);
    }

    [Fact]
    public void MixedEntitySimulation_GodotTypes()
    {
        const int entityCount = 500;
        const int readsPerFrame = 3;
        const int writesPerFrame = 2;
        const int frames = 60;
        const long totalOps = (long)entityCount * frames * (readsPerFrame + writesPerFrame);

        var entityDicts = new Dictionary<string, TypedData>[entityCount];
        for (var e = 0; e < entityCount; e++)
        {
            entityDicts[e] = new Dictionary<string, TypedData>
            {
                ["position"] = new TypedData(130, 0, new Vector3(e * 2, 0, 0)),
                ["color"] = new TypedData(137, 0, new Color(0.5f, 0.3f, 0.1f, 1.0f)),
                ["alive"] = (TypedData)true,
                ["speed"] = (TypedData)(float)(e % 10 + 1)
            };
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();

        var allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();

        for (var f = 0; f < frames; f++)
        {
            for (var e = 0; e < entityCount; e++)
            {
                var dict = entityDicts[e];
                for (var r = 0; r < readsPerFrame; r++)
                {
                    if (dict.TryGetValue("position", out var pos) && pos.TryGetVector3(out var p))
                        _ = p.X + p.Y + p.Z;
                    if (dict.TryGetValue("color", out var col) && col.TryGetColor(out var c))
                        _ = c.R + c.G + c.B;
                    if (dict.TryGetValue("alive", out var al) && al.TryGetBoolean(out var a))
                        _ = a;
                }

                for (var w = 0; w < writesPerFrame; w++)
                {
                    dict["position"] = new TypedData(130, 0,
                        new Vector3(e * 2 + w, f, w));
                    dict["speed"] = (TypedData)(float)(f + w);
                }
            }
        }

        sw.Stop();
        var totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        _perf.ReportTable(
            $"Godot Entity Simulation: {entityCount} entities x {frames} frames, {readsPerFrame}r+{writesPerFrame}w",
            [
                ("EntitySim", (int)totalOps, sw.Elapsed, totalAlloc)
            ]);

        var e0 = entityDicts[0];
        Assert.True(e0.TryGetValue("position", out var posCheck));
        Assert.True(posCheck.TryGetVector3(out _));
        Assert.True(e0.TryGetValue("alive", out var aliveCheck));
        Assert.True(aliveCheck.TryGetBoolean(out var isAlive));
        Assert.True(isAlive);
    }
}
