using System;
using System.IO;
using Xunit;

namespace Origo.Core.Tests.TestSupport;

public class PerfReporter(TextWriter output, ITestOutputHelper? testOutput = null)
{
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

    public void Report(string title, int iterations, TimeSpan elapsed, long allocatedBytes,
        string? baselineName = null, double? baselineTimeMs = null, long? baselineAlloc = null)
    {
        var divider = new string('-', 70);
        var timeMs = elapsed.TotalMilliseconds;
        var opsPerSec = iterations / elapsed.TotalSeconds;
        var nsPerOp = elapsed.TotalNanoseconds() / iterations;
        var allocStr = FormatBytes(allocatedBytes);

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

        PrintRow(nameA, iterationsA, timeA, allocA);
        PrintRow(nameB, iterationsB, timeB, allocB);

        WriteLine($"  {divider}");
        WriteLine($"  Result: '{faster}' is {ratio:F2}x faster");
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

internal static class TimeSpanExtensions
{
    internal static double TotalNanoseconds(this TimeSpan ts) => ts.Ticks * 100.0;
    internal static double TotalMicroseconds(this TimeSpan ts) => ts.Ticks / 10.0;
}
