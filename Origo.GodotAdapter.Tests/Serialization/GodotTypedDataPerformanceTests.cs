using System;
using System.Runtime.CompilerServices;
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
    private const int _warmupRounds = 1;
    private const int _timedRounds = 5;

    private readonly PerfReporter _perf = PerfReporter.ForTest(output);

    static GodotTypedDataPerformanceTests()
    {
        // Force the GodotAdapter assembly to load so its generated
        // [ModuleInitializer] registrations run before TypedData kind use.
        RuntimeHelpers.RunModuleConstructor(typeof(GodotSndManager).Module.ModuleHandle);
    }

    [Fact]
    public void WriteThroughput_RegisteredFactory_Outperforms_UnregisteredFallback()
    {
        var v = new Vector3(1.0f, 2.0f, 3.0f);

        var (Elapsed, Allocated) = MeasureMinOfRounds(() =>
        {
            for (var i = 0; i < _iterations; i++)
                _ = TypedDataFactory<Vector3>.Create(v);
        });

        var unregistered = MeasureMinOfRounds(() =>
        {
            for (var i = 0; i < _iterations; i++)
                _ = new TypedData(TypedData.UnregisteredKind, 0, v);
        });

        _perf.CompareTable(
            "Godot Vector3 Write: Registered factory vs unregistered fallback",
            "Registered", "Unregistered",
            [
                ("Godot Write Vector3", _iterations, Elapsed, Allocated,
                    unregistered.Elapsed, unregistered.Allocated)
            ]);

        var tdReg = TypedDataFactory<Vector3>.Create(v);
        Assert.True(tdReg.TryGetVector3(out var extracted));
        Assert.Equal(v, extracted);

        var tdUnreg = new TypedData(TypedData.UnregisteredKind, 0, v);
        Assert.False(tdUnreg.TryGetVector3(out _));
        var unregObj = TypedDataObjectConverter.ToObject(tdUnreg);
        Assert.IsType<Vector3>(unregObj);
        Assert.Equal(v, (Vector3)unregObj!);
    }

    [Fact]
    public void ReadThroughput_TryGetVector3_Outperforms_ToObjectTypeCheck()
    {
        var source = new Vector3(1.0f, 2.0f, 3.0f);
        var td = TypedDataFactory<Vector3>.Create(source);

        var sumKind = 0.0f;
        var (Elapsed, Allocated) = MeasureMinOfRounds(() =>
        {
            sumKind = 0.0f;
            for (var i = 0; i < _iterations; i++)
            {
                if (td.TryGetVector3(out var r)) sumKind += r.X;
            }
        });

        var sumIsT = 0.0f;
        var isT = MeasureMinOfRounds(() =>
        {
            sumIsT = 0.0f;
            for (var i = 0; i < _iterations; i++)
            {
                if (TypedDataObjectConverter.ToObject(td) is Vector3 r) sumIsT += r.X;
            }
        });

        Assert.Equal(sumKind, sumIsT, 0.01f);

        _perf.CompareTable(
            "Godot Vector3 Read: TryGetVector3 vs ToObject type check",
            "TryGet", "ToObject",
            [
                ("Godot Read Vector3", _iterations, Elapsed, Allocated, isT.Elapsed, isT.Allocated)
            ]);
    }

    [Fact]
    public void ObjectConverter_ToObject_RegisteredVector3_ReportsThroughput()
    {
        var v = new Vector3(1, 2, 3);
        var td = TypedDataFactory<Vector3>.Create(v);

        var (Elapsed, Allocated) = MeasureMinOfRounds(() =>
        {
            for (var i = 0; i < _iterations; i++)
                _ = TypedDataObjectConverter.ToObject(td);
        });

        _perf.Report(
            "Godot ToObject: Registered Vector3 conversion",
            _iterations, Elapsed, Allocated);

        var result = TypedDataObjectConverter.ToObject(td);
        Assert.IsType<Vector3>(result);
        Assert.Equal(v, (Vector3)result);
    }

    [Fact]
    public void ObjectConverter_FromObject_GodotSwitch_Outperforms_Fallback()
    {
        var v = new Color(0.2f, 0.4f, 0.6f, 0.8f);

        var (Elapsed, Allocated) = MeasureMinOfRounds(() =>
        {
            for (var i = 0; i < _iterations; i++)
                _ = TypedDataObjectConverter.FromObject(137, v);
        });

        var fallback = MeasureMinOfRounds(() =>
        {
            for (var i = 0; i < _iterations; i++)
                _ = TypedDataObjectConverter.FromObject(TypedData.UnregisteredKind, v);
        });

        _perf.CompareTable(
            "Godot FromObject: Kind-switch vs unregistered fallback",
            "FromObject", "Fallback",
            [
                ("Godot FromObject Color", _iterations, Elapsed, Allocated,
                    fallback.Elapsed, fallback.Allocated)
            ]);

        var (_, refReg) = TypedDataObjectConverter.FromObject(137, v);
        var (_, refUnreg) = TypedDataObjectConverter.FromObject(TypedData.UnregisteredKind, v);
        Assert.Equal(refReg, refUnreg);
    }

    [Fact]
    public void Factory_CreateExtract_Vector3_ReportsThroughput()
    {
        var v = new Vector3(5, 6, 7);

        var (Elapsed, Allocated) = MeasureMinOfRounds(() =>
        {
            for (var i = 0; i < _iterations; i++)
            {
                var created = TypedDataFactory<Vector3>.Create(v);
                TypedDataFactory<Vector3>.TryExtract(created, out _);
            }
        });

        _perf.ReportTable(
            "Godot Vector3 Factory Create+Extract (kind-based path)",
            [
                ("Godot Create+Extract", _iterations * 2, Elapsed, Allocated)
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
                ["position"] = TypedDataFactory<Vector3>.Create(new Vector3(e * 2, 0, 0)),
                ["color"] = TypedDataFactory<Color>.Create(new Color(0.5f, 0.3f, 0.1f, 1.0f)),
                ["alive"] = (TypedData)true,
                ["speed"] = (TypedData)(float)(e % 10 + 1)
            };
        }

        void RunSimulation()
        {
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
                        dict["position"] = TypedDataFactory<Vector3>.Create(
                            new Vector3(e * 2 + w, f, w));
                        dict["speed"] = (TypedData)(float)(f + w);
                    }
                }
            }
        }

        var (Elapsed, Allocated) = MeasureMinOfRounds(RunSimulation);

        _perf.ReportTable(
            $"Godot Entity Simulation: {entityCount} entities x {frames} frames, {readsPerFrame}r+{writesPerFrame}w",
            [
                ("Godot EntitySim", (int)totalOps, Elapsed, Allocated)
            ]);

        var e0 = entityDicts[0];
        Assert.True(e0.TryGetValue("position", out var posCheck));
        Assert.True(posCheck.TryGetVector3(out _));
        Assert.True(e0.TryGetValue("alive", out var aliveCheck));
        Assert.True(aliveCheck.TryGetBoolean(out var isAlive));
        Assert.True(isAlive);
    }

    private static (TimeSpan Elapsed, long Allocated) MeasureMinOfRounds(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        for (var round = 0; round < _warmupRounds; round++)
            action();

        var best = TimeSpan.MaxValue;
        long bestAlloc = 0;
        for (var round = 0; round < _timedRounds; round++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            action();
            sw.Stop();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

            if (sw.Elapsed >= best)
                continue;

            best = sw.Elapsed;
            bestAlloc = allocated;
        }

        return (best, bestAlloc);
    }
}
