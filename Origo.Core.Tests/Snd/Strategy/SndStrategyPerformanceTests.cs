using System;
using System.Collections.Generic;
using System.Diagnostics;
using Origo.Core.Abstractions.Entity;
using Origo.Core.DataSource;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Origo.Core.Tests.TestSupport;
using Xunit;

namespace Origo.Core.Tests;

[Collection("StrategyStateTests")]
public class SndStrategyPerformanceTests
{
    private readonly PerfReporter _perf;

    public SndStrategyPerformanceTests(ITestOutputHelper output)
    {
        _perf = PerfReporter.ForTest(output);
    }

    private const string PoolIdx = "perf.pool.test";
    private const string Process1Idx = "perf.process.1";

    // ── Strategy pool Get/Release throughput ────────────────────────────

    [Fact]
    public void StrategyPool_GetRelease_Throughput()
    {
        const int iterations = 100_000;
        var pool = new SndStrategyPool(new TestLogger());
        pool.Register<PerfPoolStrategy>(() => new PerfPoolStrategy());

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            var s = pool.GetStrategy<PerfPoolStrategy>(PoolIdx);
            pool.ReleaseStrategy(PoolIdx);
        }
        sw.Stop();
        long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        _perf.Report(
            "StrategyPool Get+Release roundtrip",
            iterations,
            sw.Elapsed,
            totalAlloc);

        Assert.True(totalAlloc < 500_000_000,
            $"Pool Get+Release × {iterations}: allocated {totalAlloc} bytes (unexpected)");
    }

    // ── StrategyManager Process scaling by strategy count ───────────────

    [Fact]
    public void StrategyManager_Process_StrategyCountScaling()
    {
        const int frames = 10_000;
        var strategyCounts = new[] { 1, 5, 10, 20 };

        foreach (var sc in strategyCounts)
        {
            var host = CreateBoundHost(w =>
            {
                w.RegisterStrategy(() => new PerfProcess1Strategy());
                w.RegisterStrategy(() => new PerfProcess2Strategy());
                w.RegisterStrategy(() => new PerfProcess3Strategy());
                w.RegisterStrategy(() => new PerfProcess4Strategy());
                w.RegisterStrategy(() => new PerfProcess5Strategy());
                w.RegisterStrategy(() => new PerfProcess6Strategy());
                w.RegisterStrategy(() => new PerfProcess7Strategy());
                w.RegisterStrategy(() => new PerfProcess8Strategy());
                w.RegisterStrategy(() => new PerfProcess9Strategy());
                w.RegisterStrategy(() => new PerfProcess10Strategy());
                w.RegisterStrategy(() => new PerfProcess11Strategy());
                w.RegisterStrategy(() => new PerfProcess12Strategy());
                w.RegisterStrategy(() => new PerfProcess13Strategy());
                w.RegisterStrategy(() => new PerfProcess14Strategy());
                w.RegisterStrategy(() => new PerfProcess15Strategy());
                w.RegisterStrategy(() => new PerfProcess16Strategy());
                w.RegisterStrategy(() => new PerfProcess17Strategy());
                w.RegisterStrategy(() => new PerfProcess18Strategy());
                w.RegisterStrategy(() => new PerfProcess19Strategy());
                w.RegisterStrategy(() => new PerfProcess20Strategy());
            });
            var indices = BuildIndices(sc);
            var entity = host.CreateEntity(CreateMeta("E", indices));
            ((IEntityLifecycle)entity).FireAfterSpawnHooks();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            for (var f = 0; f < frames; f++)
                host.ProcessAll(0.016);
            sw.Stop();
            long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

            _perf.Report(
                $"Process {frames} frames with {sc} strategies",
                frames * sc,
                sw.Elapsed,
                totalAlloc);
        }
    }

    // ── TriggerAll ToArray allocation ───────────────────────────────────

    [Fact]
    public void TriggerAll_AfterSpawn_AllocationByStrategyCount()
    {
        var strategyCounts = new[] { 1, 10 };

        foreach (var sc in strategyCounts)
        {
            var world = TestFactory.CreateSndWorld();
            world.RegisterStrategy(() => new PerfProcess1Strategy());
            if (sc >= 2) world.RegisterStrategy(() => new PerfProcess2Strategy());
            if (sc >= 3) world.RegisterStrategy(() => new PerfProcess3Strategy());
            if (sc >= 4) world.RegisterStrategy(() => new PerfProcess4Strategy());
            if (sc >= 5) world.RegisterStrategy(() => new PerfProcess5Strategy());
            if (sc >= 6) world.RegisterStrategy(() => new PerfProcess6Strategy());
            if (sc >= 7) world.RegisterStrategy(() => new PerfProcess7Strategy());
            if (sc >= 8) world.RegisterStrategy(() => new PerfProcess8Strategy());
            if (sc >= 9) world.RegisterStrategy(() => new PerfProcess9Strategy());
            if (sc >= 10) world.RegisterStrategy(() => new PerfProcess10Strategy());

            var entity = world.CreateEntity(new NullNodeFactory(), NullSndContext.Instance, new TestLogger());
            ((IEntityLifecycle)entity).RecoverForLifecycle(CreateMeta("E", BuildIndices(sc)));

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long allocBefore = GC.GetAllocatedBytesForCurrentThread();
            ((IEntityLifecycle)entity).FireAfterSpawnHooks();
            long singleTriggerAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

            _perf.Report(
                $"TriggerAll AfterSpawn _strategies.ToArray() — {sc} strategies",
                1,
                TimeSpan.Zero,
                singleTriggerAlloc);
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static string[] BuildIndices(int count)
    {
        var indices = new string[count];
        for (var i = 0; i < count; i++)
            indices[i] = $"perf.process.{i + 1}";
        return indices;
    }

    private static FullMemorySndSceneHost CreateBoundHost(Action<SndWorld> configureWorld)
    {
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        configureWorld(world);
        host.BindWorld(world);
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var runtime = TestFactory.CreateRuntime(logger, host);
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial",
            "res://entry/entry.json"));
        host.BindContext(ctx);
        return host;
    }

    private static SndMetaData CreateMeta(string name, string[]? indices) => new()
    {
        Name = name,
        NodeMetaData = new NodeMetaData(),
        StrategyMetaData = new StrategyMetaData
        {
            EntityIndices = new List<string>(indices ?? Array.Empty<string>())
        },
        DataMetaData = new DataMetaData()
    };

    // ── Strategy stubs ──────────────────────────────────────────────────

    [StrategyIndex(PoolIdx)]
    private sealed class PerfPoolStrategy : LifecycleStrategyBase
    {
    }

    private abstract class PerfProcessBase : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx) { }
    }

    [StrategyIndex(Process1Idx)] private sealed class PerfProcess1Strategy : PerfProcessBase { }
    [StrategyIndex("perf.process.2")] private sealed class PerfProcess2Strategy : PerfProcessBase { }
    [StrategyIndex("perf.process.3")] private sealed class PerfProcess3Strategy : PerfProcessBase { }
    [StrategyIndex("perf.process.4")] private sealed class PerfProcess4Strategy : PerfProcessBase { }
    [StrategyIndex("perf.process.5")] private sealed class PerfProcess5Strategy : PerfProcessBase { }
    [StrategyIndex("perf.process.6")] private sealed class PerfProcess6Strategy : PerfProcessBase { }
    [StrategyIndex("perf.process.7")] private sealed class PerfProcess7Strategy : PerfProcessBase { }
    [StrategyIndex("perf.process.8")] private sealed class PerfProcess8Strategy : PerfProcessBase { }
    [StrategyIndex("perf.process.9")] private sealed class PerfProcess9Strategy : PerfProcessBase { }
    [StrategyIndex("perf.process.10")] private sealed class PerfProcess10Strategy : PerfProcessBase { }
    [StrategyIndex("perf.process.11")] private sealed class PerfProcess11Strategy : PerfProcessBase { }
    [StrategyIndex("perf.process.12")] private sealed class PerfProcess12Strategy : PerfProcessBase { }
    [StrategyIndex("perf.process.13")] private sealed class PerfProcess13Strategy : PerfProcessBase { }
    [StrategyIndex("perf.process.14")] private sealed class PerfProcess14Strategy : PerfProcessBase { }
    [StrategyIndex("perf.process.15")] private sealed class PerfProcess15Strategy : PerfProcessBase { }
    [StrategyIndex("perf.process.16")] private sealed class PerfProcess16Strategy : PerfProcessBase { }
    [StrategyIndex("perf.process.17")] private sealed class PerfProcess17Strategy : PerfProcessBase { }
    [StrategyIndex("perf.process.18")] private sealed class PerfProcess18Strategy : PerfProcessBase { }
    [StrategyIndex("perf.process.19")] private sealed class PerfProcess19Strategy : PerfProcessBase { }
    [StrategyIndex("perf.process.20")] private sealed class PerfProcess20Strategy : PerfProcessBase { }
}
