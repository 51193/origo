using System;
using System.IO;
using Xunit;

namespace Origo.SourceGeneration.Tests.TestSupport;

/// <summary>
///     Prints performance comparison tables to both the console and the xUnit test
///     output so benchmark results are visible in every CI run.
/// </summary>
public class PerfReporter
{
    private readonly TextWriter _output;
    private readonly ITestOutputHelper? _testOutput;

    public PerfReporter(TextWriter output, ITestOutputHelper? testOutput = null)
    {
        _output = output;
        _testOutput = testOutput;
    }

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
