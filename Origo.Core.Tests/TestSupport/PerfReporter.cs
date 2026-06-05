using System;
using System.IO;

namespace Origo.Core.Tests.TestSupport;

public class PerfReporter
{
    private readonly TextWriter _output;

    public PerfReporter(TextWriter output)
    {
        _output = output;
    }

    public static PerfReporter ToConsole { get; } = new(Console.Out);

    public void Report(string title, int iterations, TimeSpan elapsed, long allocatedBytes,
        string? baselineName = null, double? baselineTimeMs = null, long? baselineAlloc = null)
    {
        var divider = new string('-', 70);
        var timeMs = elapsed.TotalMilliseconds;
        var opsPerSec = iterations / elapsed.TotalSeconds;
        var nsPerOp = elapsed.TotalNanoseconds() / iterations;
        var allocStr = FormatBytes(allocatedBytes);

        _output.WriteLine();
        _output.WriteLine($"  === {title} ===");
        _output.WriteLine($"  {divider}");
        _output.WriteLine($"  Iterations   : {iterations:N0}");
        _output.WriteLine($"  Time         : {FormatTime(elapsed)}");
        _output.WriteLine($"  Ops/s        : {FormatRate(opsPerSec)}");
        _output.WriteLine($"  ns/op        : {nsPerOp:F2}");
        _output.WriteLine($"  Alloc        : {allocStr}");

        if (baselineTimeMs.HasValue)
        {
            var ratio = baselineTimeMs.Value / timeMs;
            var faster = ratio >= 1.0 ? "faster" : "slower";
            var absRatio = ratio >= 1.0 ? ratio : 1.0 / ratio;
            _output.WriteLine($"  vs baseline  : {absRatio:F2}x {faster} ({baselineName})");
        }

        if (baselineAlloc.HasValue && allocatedBytes > 0)
        {
            var allocRatio = (double)baselineAlloc.Value / allocatedBytes;
            _output.WriteLine($"  Alloc ratio  : {allocRatio:F2}x vs baseline");
        }

        _output.WriteLine($"  {divider}");
    }

    public void Compare(string title, string nameA, int iterationsA, TimeSpan timeA, long allocA,
        string nameB, int iterationsB, TimeSpan timeB, long allocB)
    {
        var divider = new string('-', 70);
        var faster = timeA < timeB ? nameA : nameB;
        var ratio = timeA < timeB
            ? timeB.TotalMilliseconds / timeA.TotalMilliseconds
            : timeA.TotalMilliseconds / timeB.TotalMilliseconds;

        _output.WriteLine();
        _output.WriteLine($"  === {title} ===");
        _output.WriteLine($"  {divider}");
        _output.WriteLine($"  Method                 Iterations   Time         Ops/s         Alloc");
        _output.WriteLine($"  {divider}");

        PrintRow(nameA, iterationsA, timeA, allocA);
        PrintRow(nameB, iterationsB, timeB, allocB);

        _output.WriteLine($"  {divider}");
        _output.WriteLine($"  Result: '{faster}' is {ratio:F2}x faster");
        _output.WriteLine($"  {divider}");
    }

    private void PrintRow(string name, int iterations, TimeSpan elapsed, long alloc)
    {
        var opsPerSec = iterations / elapsed.TotalSeconds;
        _output.WriteLine($"  {name,-23} {iterations,-12:N0} {FormatTime(elapsed),-11}  {FormatRate(opsPerSec),-13} {FormatBytes(alloc),-12}");
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

internal static class TimeSpanExtensions
{
    internal static double TotalNanoseconds(this TimeSpan ts) => ts.Ticks * 100.0;
    internal static double TotalMicroseconds(this TimeSpan ts) => ts.Ticks / 10.0;
}
