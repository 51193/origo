using System;
using System.Collections.Generic;
using System.Diagnostics;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

[Trait("Category", "Benchmark")]
public class ObserverTopologyBenchmarkTests(ITestOutputHelper output)
{
    private const int _warmupRounds = 1;
    private const int _timedRounds = 5;

    private readonly PerfReporter _perf = PerfReporter.ForTest(output);

    [Fact]
    public void ObserverMount_ScalingByBindingCount()
    {
        var bindingCounts = new[] { 10, 50, 200 };

        // JIT warmup
        {
            var ww = TestFactory.CreateSndWorld();
            ww.RegisterStrategy(() => new SimpleObserverStrategy());
            ww.RegisterStrategy(() => new LifecycleStubStrategy());
            var wt = new ObserverTopology(ww.StrategyPool, new TestLogger());
            wt.BindContext(NullSndContext.Instance);
            var wo = ww.CreateEntity(new NullNodeFactory(), NullSndContext.Instance, new TestLogger(), wt);
            var wt2 = ww.CreateEntity(new NullNodeFactory(), NullSndContext.Instance, new TestLogger(), wt);
            wo.BindSession(BenchmarkSession.Instance);
            wt2.BindSession(BenchmarkSession.Instance);
            ((IEntityLifecycle)wo).RecoverForLifecycle(
                new SndMetaData { Name = "_wo", NodeMetaData = new NodeMetaData(), StrategyMetaData = new StrategyMetaData { LifecycleIndices = ["perf.stub"] } });
            ((IEntityLifecycle)wt2).RecoverForLifecycle(
                new SndMetaData { Name = "_wt", NodeMetaData = new NodeMetaData(), StrategyMetaData = new StrategyMetaData { LifecycleIndices = ["perf.stub"] } });
            wt.Mount(wo, wt2, "perf.observer");
            wt.Unmount(wo, wt2, "perf.observer");
        }

        var rows = new List<(string, int, TimeSpan, long)>();

        foreach (var bc in bindingCounts)
        {
            var world = TestFactory.CreateSndWorld();
            world.RegisterStrategy(() => new SimpleObserverStrategy());
            world.RegisterStrategy(() => new LifecycleStubStrategy());
            var topology = new ObserverTopology(world.StrategyPool, new TestLogger());
            topology.BindContext(NullSndContext.Instance);

            var observer = world.CreateEntity(
                new NullNodeFactory(), NullSndContext.Instance, new TestLogger(), topology);
            ((Origo.Core.Snd.Entity.SndEntity)observer).BindSession(BenchmarkSession.Instance);
            ((IEntityLifecycle)observer).RecoverForLifecycle(
                new SndMetaData
                {
                    Name = "observer",
                    NodeMetaData = new NodeMetaData(),
                    StrategyMetaData = new StrategyMetaData { LifecycleIndices = ["perf.stub"] }
                });

            var targets = new ISndEntity[bc];
            for (var i = 0; i < bc; i++)
            {
                targets[i] = world.CreateEntity(
                    new NullNodeFactory(), NullSndContext.Instance, new TestLogger(), topology);
                ((Origo.Core.Snd.Entity.SndEntity)targets[i]).BindSession(BenchmarkSession.Instance);
                ((IEntityLifecycle)targets[i]).RecoverForLifecycle(
                    new SndMetaData
                    {
                        Name = $"target_{i}",
                        NodeMetaData = new NodeMetaData(),
                        StrategyMetaData = new StrategyMetaData { LifecycleIndices = ["perf.stub"] }
                    });
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < bc; i++)
                topology.Mount(observer, targets[i], "perf.observer");
            sw.Stop();
            var totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

            rows.Add(($"Mount × {bc}", bc, sw.Elapsed, totalAlloc));

            Assert.NotNull(observer.Name);
        }

        _perf.ReportTable("ObserverTopology.Mount — scaling by binding count", rows);
    }

    [Fact]
    public void ObserverUnmount_ScalingByBindingCount()
    {
        var bindingCounts = new[] { 10, 50, 200 };

        // JIT warmup — same as Mount but call Unmount after Mount
        {
            var ww = TestFactory.CreateSndWorld();
            ww.RegisterStrategy(() => new SimpleObserverStrategy());
            ww.RegisterStrategy(() => new LifecycleStubStrategy());
            var wt = new ObserverTopology(ww.StrategyPool, new TestLogger());
            wt.BindContext(NullSndContext.Instance);
            var wo = ww.CreateEntity(new NullNodeFactory(), NullSndContext.Instance, new TestLogger(), wt);
            var wt2 = ww.CreateEntity(new NullNodeFactory(), NullSndContext.Instance, new TestLogger(), wt);
            wo.BindSession(BenchmarkSession.Instance);
            wt2.BindSession(BenchmarkSession.Instance);
            ((IEntityLifecycle)wo).RecoverForLifecycle(
                new SndMetaData { Name = "_wo", NodeMetaData = new NodeMetaData(), StrategyMetaData = new StrategyMetaData { LifecycleIndices = ["perf.stub"] } });
            ((IEntityLifecycle)wt2).RecoverForLifecycle(
                new SndMetaData { Name = "_wt", NodeMetaData = new NodeMetaData(), StrategyMetaData = new StrategyMetaData { LifecycleIndices = ["perf.stub"] } });
            wt.Mount(wo, wt2, "perf.observer");
            wt.Unmount(wo, wt2, "perf.observer");
        }

        var rows = new List<(string, int, TimeSpan, long)>();

        foreach (var bc in bindingCounts)
        {
            var world = TestFactory.CreateSndWorld();
            world.RegisterStrategy(() => new SimpleObserverStrategy());
            world.RegisterStrategy(() => new LifecycleStubStrategy());
            var topology = new ObserverTopology(world.StrategyPool, new TestLogger());
            topology.BindContext(NullSndContext.Instance);

            var observer = world.CreateEntity(
                new NullNodeFactory(), NullSndContext.Instance, new TestLogger(), topology);
            ((Origo.Core.Snd.Entity.SndEntity)observer).BindSession(BenchmarkSession.Instance);
            ((IEntityLifecycle)observer).RecoverForLifecycle(
                new SndMetaData
                {
                    Name = "observer",
                    NodeMetaData = new NodeMetaData(),
                    StrategyMetaData = new StrategyMetaData { LifecycleIndices = ["perf.stub"] }
                });

            var targets = new ISndEntity[bc];
            for (var i = 0; i < bc; i++)
            {
                targets[i] = world.CreateEntity(
                    new NullNodeFactory(), NullSndContext.Instance, new TestLogger(), topology);
                ((Origo.Core.Snd.Entity.SndEntity)targets[i]).BindSession(BenchmarkSession.Instance);
                ((IEntityLifecycle)targets[i]).RecoverForLifecycle(
                    new SndMetaData
                    {
                        Name = $"target_{i}",
                        NodeMetaData = new NodeMetaData(),
                        StrategyMetaData = new StrategyMetaData { LifecycleIndices = ["perf.stub"] }
                    });
                topology.Mount(observer, targets[i], "perf.observer");
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < bc; i++)
                topology.Unmount(observer, targets[i], "perf.observer");
            sw.Stop();
            var totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

            rows.Add(($"Unmount × {bc}", bc, sw.Elapsed, totalAlloc));
        }

        _perf.ReportTable("ObserverTopology.Unmount — scaling by binding count", rows);
    }

    /// <summary>
    ///     Minimal session binding for benchmark entities: real production
    ///     entities are always session-bound, so the benchmark must model
    ///     that (the Mount validation reads the owning session).
    /// </summary>
    private sealed class BenchmarkSession : Origo.Core.Abstractions.Lifecycle.ISessionRun
    {
        public static readonly BenchmarkSession Instance = new();

        public Origo.Core.Abstractions.Blackboard.IBlackboard SessionBlackboard =>
            throw new NotSupportedException();
        public string LevelId => "bench";
        public bool IsFrontSession => true;
        public Origo.Core.Abstractions.Lifecycle.ISessionManager SessionManager =>
            throw new NotSupportedException();

        public Origo.Core.Abstractions.Entity.ISndEntity? FindByName(string name) => null;
        public IReadOnlyCollection<Origo.Core.Abstractions.Entity.ISndEntity> GetEntities() => [];
        public Origo.Core.Abstractions.Entity.ISndEntity Spawn(Origo.Core.Snd.Metadata.SndMetaData meta) =>
            throw new NotSupportedException();
        public void SpawnMany(params Origo.Core.Snd.Metadata.SndMetaData[] metaList) =>
            throw new NotSupportedException();
        public void RequestKillEntity(string entityName) => throw new NotSupportedException();
        public Origo.Core.Abstractions.StateMachine.IStateMachineContainer GetSessionStateMachines() =>
            throw new NotSupportedException();
        public void Dispose() { }
    }

    [StrategyIndex("perf.observer")]
    private sealed class SimpleObserverStrategy : ObserverStrategyBase
    {
    }

    [StrategyIndex("perf.stub")]
    private sealed class LifecycleStubStrategy : LifecycleStrategyBase
    {
    }
}
