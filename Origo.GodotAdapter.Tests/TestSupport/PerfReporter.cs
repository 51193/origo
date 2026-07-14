using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Origo.GodotAdapter.Tests;

public class PerfReporter(TextWriter output, ITestOutputHelper? testOutput = null)
{
    private readonly TextWriter _output = output;
    private readonly ITestOutputHelper? _testOutput = testOutput;

    public static PerfReporter ForTest(ITestOutputHelper output) => new(System.Console.Out, output);

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
            return $"{elapsed.Ticks / 10.0:F2} us";
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
