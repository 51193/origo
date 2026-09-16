using System;
using System.Collections.Generic;
using System.Diagnostics;
using Origo.Core.DataSource;
using Xunit;

namespace Origo.Core.Tests;

[Trait("Category", "Benchmark")]
public class DataSourceNodeBenchmarkTests(ITestOutputHelper output)
{
    private const int _warmupRounds = 1;
    private const int _timedRounds = 5;

    private readonly PerfReporter _perf = PerfReporter.ForTest(output);

    [Fact]
    public void TreeBuild_ScalingByDepthAndWidth()
    {
        var configs = new[] { (depth: 2, width: 5), (depth: 3, width: 8), (depth: 4, width: 8) };

        var rows = new List<(string, int, TimeSpan, long)>();

        foreach (var (depth, width) in configs)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            DataSourceNode root = BuildTree(depth, width);
            sw.Stop();
            var totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

            var nodeCount = EstimateNodeCount(depth, width);
            rows.Add(($"Build d={depth} w={width} (~{nodeCount} nodes)", nodeCount, sw.Elapsed, totalAlloc));

            root.Dispose();
        }

        _perf.ReportTable("DataSourceNode tree build — scaling by depth × width", rows);
    }

    [Fact]
    public void TreeTraversalAndHashCompute()
    {
        var configs = new[] { (depth: 3, width: 8), (depth: 4, width: 8) };

        var rows = new List<(string, int, TimeSpan, long)>();

        foreach (var (depth, width) in configs)
        {
            var root = BuildTree(depth, width);
            var nodeCount = EstimateNodeCount(depth, width);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            var hash = root.ComputeSha256Hash();
            sw.Stop();
            var totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

            Assert.NotNull(hash);
            Assert.NotEmpty(hash);

            rows.Add(($"d={depth} w={width} (~{nodeCount} nodes)", nodeCount, sw.Elapsed, totalAlloc));

            root.Dispose();
        }

        _perf.ReportTable("DataSourceNode ComputeSha256Hash — tree traversal + hash", rows);
    }

    [Fact]
    public void AsT_TypeDispatchThroughput()
    {
        const int iterations = 500_000;
        var root = BuildMixedTree(100);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        var total = 0.0;
        for (var i = 0; i < iterations; i++)
        {
            for (var j = 0; j < root.Count; j++)
            {
                var child = root[j];
                if (child.IsNull) continue;
                switch (child.Kind)
                {
                    case DataSourceNodeKind.Number:
                        total += child.As<double>();
                        break;
                    case DataSourceNodeKind.Text:
                        total += child.AsString()!.Length;
                        break;
                    case DataSourceNodeKind.Bool:
                        total += child.As<bool>() ? 1 : 0;
                        break;
                }
            }
        }
        sw.Stop();
        var totalAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        var totalOps = iterations * root.Count;
        _perf.ReportTable(
            $"DataSourceNode As<T> type dispatch ({totalOps:N0} ops)",
            [
                ("As<T> dispatch", totalOps, sw.Elapsed, totalAlloc)
            ]);

        GC.KeepAlive(total);
        root.Dispose();
    }

    private static DataSourceNode BuildTree(int depth, int width)
    {
        var root = DataSourceNode.CreateObject();
        BuildLevel(root, depth, width, "");
        return root;
    }

    private static void BuildLevel(DataSourceNode parent, int depth, int width, string prefix)
    {
        if (depth <= 0) return;

        for (var i = 0; i < width; i++)
        {
            var key = $"{prefix}k{i}";
            var child = DataSourceNode.CreateObject();
            child.Add("name", DataSourceNode.CreateString($"node_{key}"));
            child.Add("value", DataSourceNode.CreateNumber(i * 1.5));
            child.Add("flag", DataSourceNode.CreateBoolean(i % 2 == 0));
            parent.Add(key, child);
            BuildLevel(child, depth - 1, width, $"{key}_");
        }
    }

    private static DataSourceNode BuildMixedTree(int count)
    {
        var root = DataSourceNode.CreateArray();
        for (var i = 0; i < count; i++)
        {
            switch (i % 4)
            {
                case 0: root.Add(DataSourceNode.CreateNumber(i * 1.5)); break;
                case 1: root.Add(DataSourceNode.CreateString($"value_{i}")); break;
                case 2: root.Add(DataSourceNode.CreateBoolean(i % 2 == 0)); break;
                default: root.Add(DataSourceNode.CreateNull()); break;
            }
        }
        return root;
    }

    private static int EstimateNodeCount(int depth, int width)
    {
        var total = 0;
        var level = 1;
        for (var d = 0; d < depth; d++)
        {
            level *= width;
            total += level;
        }
        return total + 1;
    }
}
