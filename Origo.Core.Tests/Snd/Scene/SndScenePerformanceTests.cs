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
public class SndScenePerformanceTests
{
    private readonly PerfReporter _perf;

    public SndScenePerformanceTests(ITestOutputHelper output)
    {
        _perf = PerfReporter.ForTest(output);
    }

    private const string NoopIdx = "perf.scene.noop";
    private const string Read5Idx = "perf.scene.read5";
    private const string Write3Idx = "perf.scene.write3";

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

    private static SndMetaData CreateMeta(string name, string?[] indices) => new()
    {
        Name = name,
        NodeMetaData = new NodeMetaData(),
        StrategyMetaData = new StrategyMetaData
        {
            EntityIndices = new List<string>(indices!)
        },
        DataMetaData = new DataMetaData()
    };

    private static void SpawnNEntities(FullMemorySndSceneHost host, int count, string idx)
    {
        var metaList = new List<SndMetaData>(count);
        for (var i = 0; i < count; i++)
            metaList.Add(CreateMeta($"e_{i}", new[] { idx }));
        foreach (var meta in metaList)
        {
            var entity = host.CreateEntity(meta);
            ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        }
    }

    // ── ProcessAll scaling ──────────────────────────────────────────────

    [Fact]
    public void ProcessAll_Scaling_ByEntityCount()
    {
        var entityCounts = new[] { 100, 500, 2000 };
        const int frames = 60;

        foreach (var count in entityCounts)
        {
            var host = CreateBoundHost(w => w.RegisterStrategy(() => new NoopStrategy()));
            SpawnNEntities(host, count, NoopIdx);

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
                $"ProcessAll ({count} entities × {frames} frames)",
                count * frames,
                sw.Elapsed,
                totalAlloc);

            Assert.True(totalAlloc < 50_000_000,
                $"ProcessAll {count} × {frames}: allocated {totalAlloc} bytes (unexpected, possible logic bug)");
        }
    }

    [Fact]
    public void ProcessAll_WithDataReads_FrameSimulation()
    {
        const int entityCount = 1000;
        const int frames = 60;

        var host = CreateBoundHost(w => w.RegisterStrategy(() => new Read5Strategy()));
        SpawnNEntities(host, entityCount, Read5Idx);

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
            $"ProcessAll + 5 reads ({entityCount} entities × {frames} frames)",
            entityCount * frames,
            sw.Elapsed,
            totalAlloc);

        Assert.True(totalAlloc < 50_000_000,
            $"ProcessAll + reads: allocated {totalAlloc} bytes (unexpected, possible logic bug)");
    }

    [Fact]
    public void ProcessAll_WithDataWrites_FrameSimulation()
    {
        const int entityCount = 1000;
        const int frames = 60;

        var host = CreateBoundHost(w => w.RegisterStrategy(() => new Write3Strategy()));
        SpawnNEntities(host, entityCount, Write3Idx);

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
            $"ProcessAll + 3 writes ({entityCount} entities × {frames} frames)",
            entityCount * frames,
            sw.Elapsed,
            totalAlloc);

        Assert.True(totalAlloc < 50_000_000,
            $"ProcessAll + writes: allocated {totalAlloc} bytes (unexpected, possible logic bug)");
    }

    [Fact]
    public void ProcessAll_ToArraySnapshot_AllocationByEntityCount()
    {
        var entityCounts = new[] { 100, 1000, 5000 };

        foreach (var count in entityCounts)
        {
            var host = CreateBoundHost(w => w.RegisterStrategy(() => new NoopStrategy()));
            SpawnNEntities(host, count, NoopIdx);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long allocBefore = GC.GetAllocatedBytesForCurrentThread();
            host.ProcessAll(0.016);
            long singleFrameAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

            _perf.Report(
                $"ProcessAll single frame _entries.ToArray() allocation ({count} entities)",
                1,
                TimeSpan.Zero,
                singleFrameAlloc);

            Assert.True(singleFrameAlloc < 5_000_000,
                $"ProcessAll {count} entities single frame: {singleFrameAlloc} bytes (unexpected)");
        }
    }

    // ── Batch spawn scaling ─────────────────────────────────────────────

    [Fact]
    public void Spawn_BatchScaling()
    {
        var counts = new[] { 50, 200, 1000 };

        foreach (var count in counts)
        {
            var host = CreateBoundHost(w => w.RegisterStrategy(() => new NoopStrategy()));
            var metaList = new List<SndMetaData>(count);
            for (var i = 0; i < count; i++)
                metaList.Add(CreateMeta($"e_{i}", new[] { NoopIdx }));

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            foreach (var meta in metaList)
            {
                var entity = host.CreateEntity(meta);
                ((IEntityLifecycle)entity).FireAfterSpawnHooks();
            }
            sw.Stop();
            long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

            _perf.Report(
                $"Spawn ({count} entities)",
                count,
                sw.Elapsed,
                totalAlloc);

            Assert.True(totalAlloc < 50_000_000,
                $"Spawn {count}: allocated {totalAlloc} bytes (unexpected, possible logic bug)");
        }
    }

    [Fact]
    public void Spawn_TwoPhase_AllocationBreakdown()
    {
        const int count = 500;

        var host = CreateBoundHost(w => w.RegisterStrategy(() => new NoopStrategy()));
        var metaList = new List<SndMetaData>(count);
        for (var i = 0; i < count; i++)
            metaList.Add(CreateMeta($"e_{i}", new[] { NoopIdx }));

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long allocCreate = GC.GetAllocatedBytesForCurrentThread();
        var swCreate = Stopwatch.StartNew();
        var staged = new List<ISndEntity>(count);
        foreach (var meta in metaList)
            staged.Add(host.CreateEntity(meta));
        swCreate.Stop();
        allocCreate = GC.GetAllocatedBytesForCurrentThread() - allocCreate;

        long allocHooks = GC.GetAllocatedBytesForCurrentThread();
        var swHooks = Stopwatch.StartNew();
        foreach (var entity in staged)
            if (entity is IEntityLifecycle lc)
                lc.FireAfterSpawnHooks();
        swHooks.Stop();
        allocHooks = GC.GetAllocatedBytesForCurrentThread() - allocHooks;

        _perf.Report("Spawn Phase 1: CreateEntity", count, swCreate.Elapsed, allocCreate);
        _perf.Report("Spawn Phase 2: FireAfterSpawnHooks", count, swHooks.Elapsed, allocHooks);
    }

    // ── Kill batch scaling ──────────────────────────────────────────────

    [Fact]
    public void KillPendingEntities_BatchScaling()
    {
        var counts = new[] { 50, 200, 1000 };

        foreach (var count in counts)
        {
            var host = CreateBoundHost(w => w.RegisterStrategy(() => new NoopStrategy()));
            SpawnNEntities(host, count, NoopIdx);

            foreach (var e in host.GetEntities())
                host.RequestKillEntity(e.Name);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var runtime = new SndRuntime(TestFactory.CreateSndWorld(), host);

            long allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            runtime.KillPendingEntities();
            sw.Stop();
            long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

            _perf.Report(
                $"KillPendingEntities ({count} entities)",
                count,
                sw.Elapsed,
                totalAlloc);

            Assert.True(totalAlloc < 50_000_000,
                $"KillPendingEntities {count}: allocated {totalAlloc} bytes (unexpected)");
        }
    }

    [Fact]
    public void ClearAll_BatchScaling()
    {
        var counts = new[] { 100, 500, 1000 };

        foreach (var count in counts)
        {
            var host = CreateBoundHost(w => w.RegisterStrategy(() => new NoopStrategy()));
            SpawnNEntities(host, count, NoopIdx);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var runtime = new SndRuntime(TestFactory.CreateSndWorld(), host);

            long allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            runtime.ClearAll();
            sw.Stop();
            long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

            _perf.Report(
                $"ClearAll ({count} entities)",
                count,
                sw.Elapsed,
                totalAlloc);

            Assert.True(totalAlloc < 50_000_000,
                $"ClearAll {count}: allocated {totalAlloc} bytes (unexpected)");
        }
    }

    // ── Strategy stubs ──────────────────────────────────────────────────

    [StrategyIndex(NoopIdx)]
    private sealed class NoopStrategy : EntityStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
        }
    }

    [StrategyIndex(Read5Idx)]
    private sealed class Read5Strategy : EntityStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            entity.TryGetData<int>("hp");
            entity.TryGetData<int>("max_hp");
            entity.TryGetData<float>("speed");
            entity.TryGetData<bool>("alive");
            entity.TryGetData<int>("counter");
        }
    }

    [StrategyIndex(Write3Idx)]
    private sealed class Write3Strategy : EntityStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            entity.SetData("hp", 100);
            entity.SetData("counter", 1);
            entity.SetData("x", 10);
        }
    }
}
