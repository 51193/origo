using System;
using System.Collections.Generic;
using System.Diagnostics;
using Origo.Core.DataSource;
using Origo.Core.Save;
using Origo.Core.Save.Storage;
using Xunit;

namespace Origo.Core.Tests;

[Trait("Category", "Benchmark")]
public class SavePayloadBenchmarkTests(ITestOutputHelper output)
{
    private const int _warmupRounds = 1;
    private const int _timedRounds = 5;

    private readonly PerfReporter _perf = PerfReporter.ForTest(output);

    [Fact]
    public void PayloadHashCompute_ScalingByEntityCount()
    {
        var entityCounts = new[] { 10, 100, 500 };

        // JIT warmup
        SavePayloadWriter.ComputePayloadHash(BuildPayload(1, "_w", "_w"));

        var rows = new List<(string, int, TimeSpan, long)>();

        foreach (var count in entityCounts)
        {
            var payload = BuildPayload(count, "save_1", "level_1");

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            var hash = SavePayloadWriter.ComputePayloadHash(payload);
            sw.Stop();
            var totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

            Assert.NotNull(hash);
            Assert.NotEmpty(hash);

            rows.Add(($"Hash {count} entities", count, sw.Elapsed, totalAlloc));
        }

        _perf.ReportTable("SavePayloadWriter.ComputePayloadHash — scaling by entity count", rows);
    }

    [Fact]
    public void PayloadWriteAndRead_Roundtrip()
    {
        var entityCounts = new[] { 10, 100, 300 };

        // JIT warmup
        {
            var wfs = new TestMemoryFileSystem();
            var wh = new SaveFileHandle(TestFactory.CreateFileMetaAccess(wfs), TestFactory.CreateIoGateway(wfs),
                TestFactory.CreatePathResolver(wfs), "_warmup");
            var wp = BuildPayload(1, "_w", "_w");
            SavePayloadWriter.WriteToCurrent(wh, wp);
            SavePayloadReader.ReadFromCurrent(wh, "_w", "_w");
        }

        var rows = new List<(string, int, TimeSpan, long)>();

        foreach (var count in entityCounts)
        {
            var payload = BuildPayload(count, "save_1", "level_1");
            var fs = new TestMemoryFileSystem();
            var io = TestFactory.CreateIoGateway(fs);
            var metaAccess = TestFactory.CreateFileMetaAccess(fs);
            var pathResolver = TestFactory.CreatePathResolver(fs);
            var handle = new SaveFileHandle(metaAccess, io, pathResolver, "root");

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            SavePayloadWriter.WriteToCurrent(handle, payload);
            var loaded = SavePayloadReader.ReadFromCurrent(handle, "save_1", "level_1");
            sw.Stop();
            var totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

            Assert.Equal(payload.SaveId, loaded.SaveId);
            Assert.Equal(payload.Levels.Count, loaded.Levels.Count);

            rows.Add(($"PayloadIO {count} entities", count, sw.Elapsed, totalAlloc));
        }

        _perf.ReportTable("SavePayload WriteToCurrent + ReadFromCurrent roundtrip", rows);
    }

    [Fact]
    public void PayloadSnapshotWriteAndRead_Roundtrip()
    {
        var entityCounts = new[] { 10, 100, 300 };

        // JIT warmup
        {
            var wfs = new TestMemoryFileSystem();
            var wh = new SaveFileHandle(TestFactory.CreateFileMetaAccess(wfs), TestFactory.CreateIoGateway(wfs),
                TestFactory.CreatePathResolver(wfs), "_warmup");
            var wp = BuildPayload(1, "_w", "_w");
            SaveStorageFacade.WriteSavePayloadToCurrentThenSnapshot(wh, wp, "_w", new TestLogger());
            SaveStorageFacade.ReadSavePayloadFromSnapshot(wh, "_w", "_w");
        }

        var rows = new List<(string, int, TimeSpan, long)>();

        foreach (var count in entityCounts)
        {
            var payload = BuildPayload(count, "save_2", "level_1");
            var fs = new TestMemoryFileSystem();
            var io = TestFactory.CreateIoGateway(fs);
            var metaAccess = TestFactory.CreateFileMetaAccess(fs);
            var pathResolver = TestFactory.CreatePathResolver(fs);
            var handle = new SaveFileHandle(metaAccess, io, pathResolver, "root");

            var logger = new TestLogger();
            SaveStorageFacade.WriteSavePayloadToCurrentThenSnapshot(handle, payload, "save_2", logger);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            var loaded = SaveStorageFacade.ReadSavePayloadFromSnapshot(handle, "save_2", "level_1");
            sw.Stop();
            var totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

            Assert.Equal(payload.SaveId, loaded.SaveId);

            rows.Add(($"SnapshotIO {count} entities", count, sw.Elapsed, totalAlloc));
        }

        _perf.ReportTable("SaveStorageFacade WriteThenSnapshot + ReadFromSnapshot roundtrip", rows);
    }

    private static SaveGamePayload BuildPayload(int entityCount, string saveId, string levelId)
    {
        var entitiesNode = DataSourceNode.CreateArray();
        for (var i = 0; i < entityCount; i++)
        {
            var entityNode = DataSourceNode.CreateObject();
            entityNode.Add("name", DataSourceNode.CreateString($"entity_{i}"));
            entityNode.Add("index", DataSourceNode.CreateNumber(i));
            entityNode.Add("active", DataSourceNode.CreateBoolean(i % 2 == 0));

            var dataNode = DataSourceNode.CreateObject();
            dataNode.Add("health", DataSourceNode.CreateNumber(100.0));
            dataNode.Add("speed", DataSourceNode.CreateNumber(i * 1.5));
            dataNode.Add("label", DataSourceNode.CreateString($"e{i}"));
            entityNode.Add("data", dataNode);

            entitiesNode.Add(entityNode);
        }

        var levelPayload = new LevelPayload
        {
            LevelId = levelId,
            SndSceneNode = entitiesNode,
            SessionNode = DataSourceNode.CreateObject(),
            SessionStateMachinesNode = DataSourceNode.CreateObject()
        };

        return new SaveGamePayload
        {
            SaveId = saveId,
            ActiveLevelId = levelId,
            ProgressNode = DataSourceNode.CreateObject(),
            ProgressStateMachinesNode = DataSourceNode.CreateObject(),
            Levels = new Dictionary<string, LevelPayload> { [levelId] = levelPayload }
        };
    }
}
