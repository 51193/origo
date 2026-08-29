using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using Xunit;

namespace Origo.TestSupport;

public class PerfReporter(TextWriter output, ITestOutputHelper? testOutput = null)
{
    private static readonly HashSet<string> _emittedMetricKeys = new(StringComparer.Ordinal);
    private static readonly Lock _emittedMetricKeysLock = new();

    private readonly TextWriter _output = output;
    private readonly ITestOutputHelper? _testOutput = testOutput;

    public static PerfReporter ToConsole { get; } = new(Console.Out);

    public static PerfReporter ForTest(ITestOutputHelper output) => new(Console.Out, output);

    private void WriteLine(string? line = null)
    {
        if (line == null)
        {
            _output.WriteLine();
            _testOutput?.WriteLine("");
            return;
        }
        _output.WriteLine(line);
        _testOutput?.WriteLine(line);
    }

    /// <summary>
    ///     Emits a machine-readable metric line (stable format consumed by
    ///     scripts/benchmark.sh for baseline comparison):
    ///     <c>BENCH|&lt;kind&gt;|&lt;label&gt;|&lt;side&gt;|&lt;ops/s&gt;|&lt;alloc bytes&gt;</c>.
    ///     <paramref name="side" /> is "A"/"B" for two-sided comparisons or empty for single rows.
    ///     The line is formatted with the invariant culture so the
    ///     <c>BENCH|</c> regex in scripts/benchmark.sh parses identically on
    ///     every locale (decimal separators / group separators must not vary).
    /// </summary>
    public void EmitMetric(string kind, string label, string side, double opsPerSec, long allocatedBytes)
    {
        var key = string.IsNullOrEmpty(side)
            ? $"{kind}|{label}"
            : $"{kind}|{label}|{side}";

        lock (_emittedMetricKeysLock)
        {
            if (!_emittedMetricKeys.Add(key))
                throw new InvalidOperationException(
                    $"Duplicate BENCH metric key '{key}' emitted in the current test process. " +
                    "Metric keys must be unique within a benchmark run; otherwise the run log " +
                    "cannot be compared against docs/benchmarks/baseline.json without one " +
                    "measurement overwriting another.");
        }

        var line = string.Create(CultureInfo.InvariantCulture,
            $"BENCH|{kind}|{label}|{side}|{opsPerSec:F2}|{allocatedBytes}");
        _output.WriteLine(line);
        _testOutput?.WriteLine(line);
    }

    public void Report(string title, int iterations, TimeSpan elapsed, long allocatedBytes,
        string? baselineName = null, double? baselineTimeMs = null, long? baselineAlloc = null)
    {
        var divider = new string('-', 70);
        var timeMs = elapsed.TotalMilliseconds;
        var opsPerSec = iterations / elapsed.TotalSeconds;
        var nsPerOp = elapsed.TotalNanoseconds() / iterations;
        var allocStr = FormatBytes(allocatedBytes);

        EmitMetric("Report", title, "", opsPerSec, allocatedBytes);

        WriteLine();
        WriteLine($"  === {title} ===");
        WriteLine($"  {divider}");
        WriteLine($"  Iterations   : {iterations:N0}");
        WriteLine($"  Time         : {FormatTime(elapsed)}");
        WriteLine($"  Ops/s        : {FormatRate(opsPerSec)}");
        WriteLine($"  ns/op        : {nsPerOp:F2}");
        WriteLine($"  Alloc        : {allocStr}");

        if (baselineTimeMs.HasValue)
        {
            var ratio = baselineTimeMs.Value / timeMs;
            var faster = ratio >= 1.0 ? "faster" : "slower";
            var absRatio = ratio >= 1.0 ? ratio : 1.0 / ratio;
            WriteLine($"  vs baseline  : {absRatio:F2}x {faster} ({baselineName})");
        }

        if (baselineAlloc.HasValue && allocatedBytes > 0)
        {
            var allocRatio = (double)baselineAlloc.Value / allocatedBytes;
            WriteLine($"  Alloc ratio  : {allocRatio:F2}x vs baseline");
        }

        WriteLine($"  {divider}");
    }

    public void Compare(string title, string nameA, int iterationsA, TimeSpan timeA, long allocA,
        string nameB, int iterationsB, TimeSpan timeB, long allocB)
    {
        var divider = new string('-', 70);
        var faster = timeA < timeB ? nameA : nameB;
        var ratio = timeA < timeB
            ? timeB.TotalMilliseconds / timeA.TotalMilliseconds
            : timeA.TotalMilliseconds / timeB.TotalMilliseconds;

        WriteLine();
        WriteLine($"  === {title} ===");
        WriteLine($"  {divider}");
        WriteLine($"  Method                 Iterations   Time         Ops/s         Alloc");
        WriteLine($"  {divider}");

        EmitMetric("Compare", title, "A", iterationsA / timeA.TotalSeconds, allocA);
        EmitMetric("Compare", title, "B", iterationsB / timeB.TotalSeconds, allocB);

        PrintRow(nameA, iterationsA, timeA, allocA);
        PrintRow(nameB, iterationsB, timeB, allocB);

        WriteLine($"  {divider}");
        WriteLine($"  Result: '{faster}' is {ratio:F2}x faster");
        WriteLine($"  {divider}");
    }

    public void ReportTable(string title, List<(string label, int iterations, TimeSpan elapsed, long alloc)> rows)
    {
        var divider = new string('-', 86);
        WriteLine();
        WriteLine($"  === {title} ===");
        WriteLine($"  {divider}");
        WriteLine($"  {"Scenario",-26} {"Iters",-14} {"Time",-14} {"Ops/s",-16} {"Alloc"}");
        WriteLine($"  {divider}");

        foreach (var (label, iterations, elapsed, alloc) in rows)
        {
            var opsPerSec = iterations / elapsed.TotalSeconds;
            EmitMetric("ReportTable", label, "", opsPerSec, alloc);
            WriteLine($"  {label,-26} {iterations,-14:N0} {FormatTime(elapsed),-14} {FormatRate(opsPerSec),-16} {FormatBytes(alloc)}");
        }

        WriteLine($"  {divider}");
    }

    public void CompareTable(string title, string nameA, string nameB,
        List<(string label, int iterations, TimeSpan timeA, long allocA, TimeSpan timeB, long allocB)> rows)
    {
        var divider = new string('-', 114);
        var aHeader = $"{nameA}(ops/s)";
        var bHeader = $"{nameB}(ops/s)";
        var allocHeader = $"Alloc ({nameA} / {nameB})";
        WriteLine();
        WriteLine($"  === {title} ===");
        WriteLine($"  {divider}");
        WriteLine($"  {"Type",-12} {"Iters",-12} {aHeader,-18} {bHeader,-18} {"Ratio",-9} {"Winner",-14} {allocHeader}");
        WriteLine($"  {divider}");

        foreach (var (label, iterations, timeA, allocA, timeB, allocB) in rows)
        {
            var opsA = iterations / timeA.TotalSeconds;
            var opsB = iterations / timeB.TotalSeconds;
            EmitMetric("CompareTable", label, "A", opsA, allocA);
            EmitMetric("CompareTable", label, "B", opsB, allocB);
            var faster = timeA < timeB;
            var ratio = faster
                ? timeB.TotalMilliseconds / timeA.TotalMilliseconds
                : timeA.TotalMilliseconds / timeB.TotalMilliseconds;
            var winner = faster ? nameA : nameB;
            var allocStr = $"{FormatBytes(allocA)} / {FormatBytes(allocB)}";

            WriteLine($"  {label,-12} {iterations,-12:N0} {FormatRate(opsA),-18} {FormatRate(opsB),-18} {ratio,-9:F2}x {winner,-14} {allocStr}");
        }

        WriteLine($"  {divider}");
    }

    private void PrintRow(string name, int iterations, TimeSpan elapsed, long alloc)
    {
        var opsPerSec = iterations / elapsed.TotalSeconds;
        WriteLine($"  {name,-23} {iterations,-12:N0} {FormatTime(elapsed),-11}  {FormatRate(opsPerSec),-13} {FormatBytes(alloc),-12}");
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 0) return "~0 B";
        if (bytes == 0) return "0 B";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F2} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }

    private static string FormatTime(TimeSpan elapsed)
    {
        if (elapsed.TotalMilliseconds < 1)
            return $"{elapsed.TotalMicroseconds():F2} us";
        if (elapsed.TotalMilliseconds < 1000)
            return $"{elapsed.TotalMilliseconds:F2} ms";
        return $"{elapsed.TotalSeconds:F2} s";
    }

    private static string FormatRate(double opsPerSec)
    {
        if (opsPerSec >= 1_000_000_000)
            return $"{opsPerSec / 1_000_000_000:F2} Gops/s";
        if (opsPerSec >= 1_000_000)
            return $"{opsPerSec / 1_000_000:F2} Mops/s";
        if (opsPerSec >= 1_000)
            return $"{opsPerSec / 1_000:F2} Kops/s";
        return $"{opsPerSec:F2} ops/s";
    }
}

public static class TimeSpanExtensions
{
    public static double TotalNanoseconds(this TimeSpan ts) => ts.Ticks * 100.0;
    public static double TotalMicroseconds(this TimeSpan ts) => ts.Ticks / 10.0;
}
