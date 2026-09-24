using System;
using System.IO;
using Xunit;

namespace ApiInventoryTool.Tests;

public class ProgramTests
{
    [Fact]
    public void GenerateVerifyAndCompare_ReturnExpectedExitCodes()
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var assemblyPath = TestAssemblyEmitter.Emit(directory, "Sample.Api",
                "namespace Sample { public class A { public void M() { } } }");
            var assemblyArg = $"Sample.Api={assemblyPath}";
            var baselinePath = Path.Combine(directory, "baseline.json");
            var currentPath = Path.Combine(directory, "current.json");

            var (generateExit, _, generateError) = Capture(() => Program.Main(
            [
                "generate",
                "--assembly", assemblyArg,
                "--reference-dir", directory,
                "--output", baselinePath,
            ]));
            Assert.Equal(0, generateExit);
            Assert.True(File.Exists(baselinePath));
            Assert.Empty(generateError);

            var (verifyExit, verifyOut, _) = Capture(() => Program.Main(
            [
                "verify",
                "--assembly", assemblyArg,
                "--reference-dir", directory,
                "--baseline", baselinePath,
            ]));
            Assert.Equal(0, verifyExit);
            Assert.Contains("baseline: OK", verifyOut, StringComparison.OrdinalIgnoreCase);

            var (compareOkExit, compareOkOut, _) = Capture(() => Program.Main(
                ["compare", "--previous", baselinePath, "--current", baselinePath]));
            Assert.Equal(0, compareOkExit);
            Assert.Contains("validation: OK", compareOkOut, StringComparison.OrdinalIgnoreCase);

            var changedDirectory = TestAssemblyEmitter.CreateDirectory();
            try
            {
                var changedPath = TestAssemblyEmitter.Emit(changedDirectory, "Sample.Api",
                    "namespace Sample { public class A { public void M(int value) { } } }");
                var (mismatchExit, _, mismatchError) = Capture(() => Program.Main(
                [
                    "verify",
                    "--assembly", $"Sample.Api={changedPath}",
                    "--reference-dir", changedDirectory,
                    "--baseline", baselinePath,
                ]));
                Assert.Equal(1, mismatchExit);
                Assert.Contains("baseline mismatch", mismatchError, StringComparison.OrdinalIgnoreCase);

                var (currentExit, _, _) = Capture(() => Program.Main(
                [
                    "generate",
                    "--assembly", $"Sample.Api={changedPath}",
                    "--reference-dir", changedDirectory,
                    "--output", currentPath,
                ]));
                Assert.Equal(0, currentExit);

                var (compareExit, _, compareError) = Capture(() => Program.Main(
                    ["compare", "--previous", baselinePath, "--current", currentPath]));
                Assert.Equal(1, compareExit);
                Assert.Contains("Previous-package", compareError, StringComparison.Ordinal);
            }
            finally
            {
                TestAssemblyEmitter.TryDelete(changedDirectory);
            }
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }

    [Fact]
    public void CommandLineValidation_ReturnsFailureForBadInput()
    {
        Assert.Equal(1, Program.Main([]));
        Assert.Equal(0, Program.Main(["--help"]));
        Assert.Equal(1, Program.Main(["unknown"]));
        Assert.Equal(1, Program.Main(["verify"]));
        Assert.Equal(1, Program.Main(["verify", "--assembly", "broken"]));
        Assert.Equal(1, Program.Main(["verify", "--assembly", "A=missing"]));
    }

    private static (int ExitCode, string Output, string Error) Capture(Func<int> action)
    {
        var previousOut = Console.Out;
        var previousError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            var exitCode = action();
            return (exitCode, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousError);
        }
    }
}
