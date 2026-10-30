using System;
using System.IO;
using System.Text.Json;
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

    [Fact]
    public void Generate_RejectsApprovedBreaksOption()
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var approved = WriteApprovedBreaks(directory, "A", "M:A.B()", "approved");
            var (exitCode, _, error) = Capture(() => Program.Main(
            [
                "generate",
                "--assembly", "A=/missing/A.dll",
                "--approved-breaks", approved,
                "--output", Path.Combine(directory, "baseline.json"),
            ]));

            Assert.Equal(1, exitCode);
            Assert.Contains("only valid for the compare", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }

    [Fact]
    public void Verify_RejectsApprovedBreaksOption()
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var approved = WriteApprovedBreaks(directory, "A", "M:A.B()", "approved");
            var (exitCode, _, error) = Capture(() => Program.Main(
            [
                "verify",
                "--assembly", "A=/missing/A.dll",
                "--approved-breaks", approved,
                "--baseline", Path.Combine(directory, "baseline.json"),
            ]));

            Assert.Equal(1, exitCode);
            Assert.Contains("only valid for the compare", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }

    [Fact]
    public void Compare_ApprovedBreakFiltersFailure()
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var previous = WriteInventory(directory, "previous.json", "M:A.Removed()");
            var current = WriteInventory(directory, "current.json");
            var approved = WriteApprovedBreaks(directory, "A", "M:A.Removed()", "pre-0.1 removal");

            var (exitCode, output, error) = Capture(() => Program.Main(
                ["compare", "--previous", previous, "--current", current, "--approved-breaks", approved]));

            Assert.Equal(0, exitCode);
            Assert.Contains("1 approved break", output, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(error);
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }

    [Fact]
    public void Compare_DormantApproval_Fails()
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            // The approval names a line that is still present on both sides, so
            // it suppresses nothing and must not stay in the file unnoticed.
            var previous = WriteInventory(directory, "previous.json", "M:A.Kept()");
            var current = WriteInventory(directory, "current.json", "M:A.Kept()");
            var approved = WriteApprovedBreaks(directory, "A", "M:A.Kept()", "dormant approval");

            var (exitCode, _, error) = Capture(() => Program.Main(
                ["compare", "--previous", previous, "--current", current, "--approved-breaks", approved]));

            Assert.Equal(1, exitCode);
            Assert.Contains("dormant", error, StringComparison.Ordinal);
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }

    [Fact]
    public void Compare_ObsoleteApproval_IsIgnoredAndReported()
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            // The line is gone from the previous baseline (a newer release
            // absorbed the change): the approval is obsolete but not a failure.
            var previous = WriteInventory(directory, "previous.json", "M:A.Kept()");
            var current = WriteInventory(directory, "current.json", "M:A.Kept()");
            var approved = WriteApprovedBreaks(directory, "A", "M:A.Gone()", "already released");

            var (exitCode, output, error) = Capture(() => Program.Main(
                ["compare", "--previous", previous, "--current", current, "--approved-breaks", approved]));

            Assert.Equal(0, exitCode);
            Assert.Contains("1 obsolete approval", output, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("obsolete approval ignored", output, StringComparison.Ordinal);
            Assert.Empty(error);
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }

    [Fact]
    public void Compare_MissingApprovedBreaksFile_Fails()
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var previous = WriteInventory(directory, "previous.json");
            var current = WriteInventory(directory, "current.json");

            var (exitCode, _, error) = Capture(() => Program.Main(
            [
                "compare", "--previous", previous, "--current", current,
                "--approved-breaks", Path.Combine(directory, "missing-approved.json"),
            ]));

            Assert.Equal(1, exitCode);
            Assert.Contains("was not found", error, StringComparison.Ordinal);
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }

    [Fact]
    public void Compare_InvalidApprovedBreaksFile_FailsValidationBeforeComparison()
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var previous = WriteInventory(directory, "previous.json", "M:A.Removed()");
            var current = WriteInventory(directory, "current.json");
            var approved = Path.Combine(directory, "approved.json");
            File.WriteAllText(
                approved,
                """{ "breaks": [ { "assembly": "A", "api": "M:A.Removed()", "reason": "" } ] }""");

            var (exitCode, output, error) = Capture(() => Program.Main(
                ["compare", "--previous", previous, "--current", current, "--approved-breaks", approved]));

            Assert.Equal(1, exitCode);
            Assert.Contains("failed validation", error, StringComparison.Ordinal);
            Assert.Contains("reason", error, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("validation: OK", output, StringComparison.Ordinal);
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }

    private static string WriteInventory(string directory, string name, params string[] apiLines)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, InventoryJson.Serialize(new InventoryDocument
        {
            Assemblies =
            [
                new AssemblyInventory { Name = "A", Api = [.. apiLines] },
            ],
        }));
        return path;
    }

    private static string WriteApprovedBreaks(string directory, string assembly, string api, string reason)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"approved-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            breaks = new[] { new { assembly, api, reason } },
        }));
        return path;
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
