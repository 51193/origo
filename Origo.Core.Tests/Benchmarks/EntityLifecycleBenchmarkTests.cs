using System;
using System.Collections.Generic;
using System.Diagnostics;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

[Trait("Category", "Benchmark")]
public class EntityLifecycleBenchmarkTests(ITestOutputHelper output)
{
    private const int _warmupRounds = 1;
    private const int _timedRounds = 5;
    private static readonly TimeSpan _perBenchmarkCap = TimeSpan.FromSeconds(10);

    private readonly PerfReporter _perf = PerfReporter.ForTest(output);

    [Fact]
    public void EntityCreation_ScalingByEntityCount()
    {
        var host = CreateBoundHost(w => w.RegisterStrategy(() => new NoopStrategy()));
        var entityCounts = new[] { 100, 500, 2000 };

        // JIT warmup — compile all code paths before timed measurements
        WarmupEntityCreation(host);

        var rows = new List<(string, int, TimeSpan, long)>();

        foreach (var count in entityCounts)
        {
            var metas = new SndMetaData[count];
            for (var i = 0; i < count; i++)
                metas[i] = CreateMeta($"E_{i}", ["perf.lifecycle"]);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < count; i++)
            {
                var entity = host.CreateEntity(metas[i]);
                ((IEntityLifecycle)entity).FireAfterSpawnHooks();
            }
            sw.Stop();
            var totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

            rows.Add(($"{count} entities", count, sw.Elapsed, totalAlloc));

            Assert.NotNull(host.FindByName($"E_{count - 1}"));
        }

        _perf.ReportTable("Entity create + FireAfterSpawnHooks — scaling by count", rows);
    }

    [Fact]
    public void FrameProcessing_ScalingByEntityAndStrategyCount()
    {
        const int frames = 200;
        var configs = new[] { (entities: 10, strategies: 1), (entities: 50, strategies: 5), (entities: 200, strategies: 10) };

        // JIT warmup
        {
            var wh = CreateBoundHost(w => { RegisterPerfProcess(w, 1); w.RegisterStrategy(() => new NoopStrategy()); });
            var we = wh.CreateEntity(CreateMeta("_w", ["perf.el.process.1"]));
            ((IEntityLifecycle)we).FireAfterSpawnHooks();
            wh.ProcessAll(0.016);
        }

        var rows = new List<(string, int, TimeSpan, long)>();

        foreach (var (entityCount, strategyCount) in configs)
        {
            var host = CreateBoundHost(w =>
            {
                for (var s = 0; s < strategyCount; s++)
                    RegisterPerfProcess(w, s + 1);
            });
            var indices = BuildIndices(strategyCount);

            for (var e = 0; e < entityCount; e++)
            {
                var entity = host.CreateEntity(CreateMeta($"E_{e}", indices));
                ((IEntityLifecycle)entity).FireAfterSpawnHooks();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var totalOps = entityCount * strategyCount * frames;
            var allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            for (var f = 0; f < frames; f++)
                host.ProcessAll(0.016);
            sw.Stop();
            var totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

            rows.Add(($"{entityCount}e × {strategyCount}s", totalOps, sw.Elapsed, totalAlloc));

            Assert.NotNull(host.FindByName("E_0"));
        }

        _perf.ReportTable(
            $"Frame processing: ProcessAll {frames} frames — entity × strategy scaling",
            rows);
    }

    [Fact]
    public void EntitySaveSingle_ScalingByEntityCount()
    {
        var entityCounts = new[] { 10, 100, 500 };

        // JIT warmup
        {
            var wh = CreateBoundHost(w =>
            {
                w.RegisterStrategy(() => new NoopStrategy());
                w.RegisterStrategy(() => new SaveHookStrategy());
            });
            var we = wh.CreateEntity(CreateMeta("_w", ["perf.lifecycle", "perf.savehook"]));
            ((IEntityLifecycle)we).FireAfterSpawnHooks();
            ((SndEntity)we).SaveSingle();
        }

        var rows = new List<(string, int, TimeSpan, long)>();

        foreach (var count in entityCounts)
        {
            var host = CreateBoundHost(w =>
            {
                w.RegisterStrategy(() => new NoopStrategy());
                w.RegisterStrategy(() => new SaveHookStrategy());
            });
            var indices = new[] { "perf.lifecycle", "perf.savehook" };

            var entities = new ISndEntity[count];
            for (var i = 0; i < count; i++)
            {
                entities[i] = host.CreateEntity(CreateMeta($"E_{i}", indices));
                ((IEntityLifecycle)entities[i]).FireAfterSpawnHooks();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < count; i++)
                ((SndEntity)entities[i]).SaveSingle();
            sw.Stop();
            var totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

            rows.Add(($"{count} entities", count, sw.Elapsed, totalAlloc));
        }

        _perf.ReportTable("Entity SaveSingle — scaling by entity count", rows);
    }

    private static string[] BuildIndices(int count)
    {
        var indices = new string[count];
        for (var i = 0; i < count; i++)
            indices[i] = $"perf.el.process.{i + 1}";
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
            LifecycleIndices = [.. indices ?? []]
        },
        DataMetaData = new DataMetaData()
    };

    private static void WarmupEntityCreation(FullMemorySndSceneHost host)
    {
        var m = CreateMeta("_w", ["perf.lifecycle"]);
        var e = host.CreateEntity(m);
        ((IEntityLifecycle)e).FireAfterSpawnHooks();
    }

    [StrategyIndex("perf.lifecycle")]
    private sealed class NoopStrategy : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx) { }
    }

    [StrategyIndex("perf.savehook")]
    private sealed class SaveHookStrategy : LifecycleStrategyBase
    {
    }

    private static void RegisterPerfProcess(SndWorld world, int index)
    {
        switch (index)
        {
            case 1: world.RegisterStrategy(() => new ElPerfProcess1()); break;
            case 2: world.RegisterStrategy(() => new ElPerfProcess2()); break;
            case 3: world.RegisterStrategy(() => new ElPerfProcess3()); break;
            case 4: world.RegisterStrategy(() => new ElPerfProcess4()); break;
            case 5: world.RegisterStrategy(() => new ElPerfProcess5()); break;
            case 6: world.RegisterStrategy(() => new ElPerfProcess6()); break;
            case 7: world.RegisterStrategy(() => new ElPerfProcess7()); break;
            case 8: world.RegisterStrategy(() => new ElPerfProcess8()); break;
            case 9: world.RegisterStrategy(() => new ElPerfProcess9()); break;
            case 10: world.RegisterStrategy(() => new ElPerfProcess10()); break;
        }
    }

    private abstract class ElProcessBase : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx) { }
    }

    [StrategyIndex("perf.el.process.1")] private sealed class ElPerfProcess1 : ElProcessBase { }
    [StrategyIndex("perf.el.process.2")] private sealed class ElPerfProcess2 : ElProcessBase { }
    [StrategyIndex("perf.el.process.3")] private sealed class ElPerfProcess3 : ElProcessBase { }
    [StrategyIndex("perf.el.process.4")] private sealed class ElPerfProcess4 : ElProcessBase { }
    [StrategyIndex("perf.el.process.5")] private sealed class ElPerfProcess5 : ElProcessBase { }
    [StrategyIndex("perf.el.process.6")] private sealed class ElPerfProcess6 : ElProcessBase { }
    [StrategyIndex("perf.el.process.7")] private sealed class ElPerfProcess7 : ElProcessBase { }
    [StrategyIndex("perf.el.process.8")] private sealed class ElPerfProcess8 : ElProcessBase { }
    [StrategyIndex("perf.el.process.9")] private sealed class ElPerfProcess9 : ElProcessBase { }
    [StrategyIndex("perf.el.process.10")] private sealed class ElPerfProcess10 : ElProcessBase { }
}
