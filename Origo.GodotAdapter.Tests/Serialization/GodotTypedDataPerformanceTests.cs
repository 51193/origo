using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;
using Origo.Core.Snd.Metadata;
using Origo.GodotAdapter.Snd;
using Xunit;
using Xunit;

namespace Origo.GodotAdapter.Tests.Serialization;

public class GodotTypedDataPerformanceTests
{
    private const int Iterations = 200_000;
    private readonly ITestOutputHelper _output;

    static GodotTypedDataPerformanceTests()
    {
        _ = TypedDataInitializer.IsLoaded;
    }

    public GodotTypedDataPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void WriteThroughput_Registered_Outperforms_Unregistered()
    {
        var v = new Vector3(1.0f, 2.0f, 3.0f);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < Iterations; i++)
            _ = TypedData.FromObject(typeof(Vector3), v);
        sw.Stop();
        var timeRegistered = sw.Elapsed;
        long allocRegistered = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        GC.Collect();
        GC.WaitForPendingFinalizers();

        allocBefore = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        for (var i = 0; i < Iterations; i++)
            _ = new TypedData(255, 0, v);
        sw.Stop();
        var timeUnregistered = sw.Elapsed;
        long allocUnregistered = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        PrintCompare(
            "Godot Vector3 Write: Registered (FromObject) vs Unregistered (kind=255)",
            "FromObject (kind=130)", Iterations, timeRegistered, allocRegistered,
            "new TypedData(255)", Iterations, timeUnregistered, allocUnregistered);
    }

    [Fact]
    public void ReadThroughput_TryGetVector3_Outperforms_IsT()
    {
        var source = new Vector3(1.0f, 2.0f, 3.0f);
        var td = TypedData.FromObject(typeof(Vector3), source);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        var sumKind = 0.0f;
        for (var i = 0; i < Iterations; i++)
        {
            if (td.TryGetVector3(out var r)) sumKind += r.X;
        }
        sw.Stop();
        var timeKind = sw.Elapsed;
        long allocKind = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        GC.Collect();
        GC.WaitForPendingFinalizers();

        allocBefore = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        var sumIsT = 0.0f;
        for (var i = 0; i < Iterations; i++)
        {
            if (td.Data is Vector3 r) sumIsT += r.X;
        }
        sw.Stop();
        var timeIsT = sw.Elapsed;
        long allocIsT = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        Assert.Equal(sumKind, sumIsT, 0.01f);

        PrintCompare(
            "Godot Vector3 Read: TryGetVector3 (kind+is) vs Data is Vector3",
            "TryGetVector3 (kind)", Iterations, timeKind, allocKind,
            "Data is Vector3 (is T)", Iterations, timeIsT, allocIsT);
    }

    [Fact]
    public void ObjectConverter_ToObject_GodotSwitch_Outperforms_Data()
    {
        var v = new Vector3(1, 2, 3);
        var td = new TypedData(130, 0, v);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < Iterations; i++)
            _ = TypedDataObjectConverter.ToObject(td);
        sw.Stop();
        var timeSwitch = sw.Elapsed;
        long allocSwitch = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        GC.Collect();
        GC.WaitForPendingFinalizers();

        allocBefore = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        for (var i = 0; i < Iterations; i++)
            _ = td.Data;
        sw.Stop();
        var timeData = sw.Elapsed;
        long allocData = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        PrintCompare(
            "Godot ToObject: Switch dispatch vs Data property",
            "ToObject (switch)", Iterations, timeSwitch, allocSwitch,
            "Data property", Iterations, timeData, allocData);
    }

    [Fact]
    public void ObjectConverter_FromObject_GodotSwitch_Outperforms_Fallback()
    {
        var v = new Color(0.2f, 0.4f, 0.6f, 0.8f);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < Iterations; i++)
            _ = TypedDataObjectConverter.FromObject(137, v);
        sw.Stop();
        var timeSwitch = sw.Elapsed;
        long allocSwitch = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        GC.Collect();
        GC.WaitForPendingFinalizers();

        allocBefore = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        for (var i = 0; i < Iterations; i++)
            _ = TypedDataObjectConverter.FromObject(255, v);
        sw.Stop();
        var timeFallback = sw.Elapsed;
        long allocFallback = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        PrintCompare(
            "Godot FromObject: Kind-switch vs unregistered fallback",
            "FromObject (kind=137)", Iterations, timeSwitch, allocSwitch,
            "FromObject (kind=255)", Iterations, timeFallback, allocFallback);
    }

    [Fact]
    public void Factory_CreateExtract_Vector3_RegisteredVsUnregistered()
    {
        var v = new Vector3(5, 6, 7);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < Iterations; i++)
        {
            var created = TypedDataFactory<Vector3>.Create(v);
            TypedDataFactory<Vector3>.TryExtract(created, out _);
        }
        sw.Stop();
        var timeRegistered = sw.Elapsed;
        long allocRegistered = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        PrintReport("Godot Vector3 Factory Create+Extract (kind-based path)", Iterations * 2,
            timeRegistered, allocRegistered);
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
                ["position"] = TypedData.FromObject(typeof(Vector3), new Vector3(e * 2, 0, 0)),
                ["color"] = TypedData.FromObject(typeof(Color), new Color(0.5f, 0.3f, 0.1f, 1.0f)),
                ["alive"] = (TypedData)true,
                ["speed"] = (TypedData)(float)(e % 10 + 1)
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
                    if (dict.TryGetValue("position", out var pos) && pos.TryGetVector3(out var p))
                        _ = p.X + p.Y + p.Z;
                    if (dict.TryGetValue("color", out var col) && col.TryGetColor(out var c))
                        _ = c.R + c.G + c.B;
                    if (dict.TryGetValue("alive", out var al) && al.TryGetBoolean(out var a))
                        _ = a;
                }

                for (var w = 0; w < writesPerFrame; w++)
                {
                    dict["position"] = TypedData.FromObject(typeof(Vector3),
                        new Vector3(e * 2 + w, f, w));
                    dict["speed"] = (TypedData)(float)(f + w);
                }
            }
        }

        sw.Stop();
        long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        PrintReport(
            $"Godot Entity Simulation: {entityCount} entities × {frames} frames, {readsPerFrame}r+{writesPerFrame}w",
            (int)totalOps, sw.Elapsed, totalAlloc);
    }

    private void PrintReport(string title, int iterations, TimeSpan elapsed, long allocated)
    {
        var opsPerSec = iterations / elapsed.TotalSeconds;
        var separator = new string('-', 60);
        _output.WriteLine("");
        _output.WriteLine($"  === {title} ===");
        _output.WriteLine($"  {separator}");
        _output.WriteLine($"  Iterations: {iterations:N0} | Time: {elapsed.TotalMilliseconds:F2} ms | " +
                          $"Ops/s: {opsPerSec / 1_000_000:F2} M | " +
                          $"Alloc: {allocated / 1024.0:F2} KB");
        _output.WriteLine($"  {separator}");
    }

    private void PrintCompare(string title,
        string nameA, int itersA, TimeSpan timeA, long allocA,
        string nameB, int itersB, TimeSpan timeB, long allocB)
    {
        var ratio = timeA < timeB
            ? timeB.TotalMilliseconds / timeA.TotalMilliseconds
            : timeA.TotalMilliseconds / timeB.TotalMilliseconds;
        var faster = timeA < timeB ? nameA : nameB;

        var separator = new string('-', 60);

        _output.WriteLine("");
        _output.WriteLine($"  === {title} ===");
        _output.WriteLine($"  {separator}");
        _output.WriteLine($"  Method                        Time         Alloc");
        _output.WriteLine($"  {separator}");
        _output.WriteLine($"  {nameA,-30} {timeA.TotalMilliseconds,-12:F2} ms {(allocA / 1024.0),-12:F2} KB");
        _output.WriteLine($"  {nameB,-30} {timeB.TotalMilliseconds,-12:F2} ms {(allocB / 1024.0),-12:F2} KB");
        _output.WriteLine($"  {separator}");
        _output.WriteLine($"  '{faster}' is {ratio:F2}x faster");
        _output.WriteLine($"  {separator}");
    }
}
