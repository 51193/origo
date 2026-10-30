using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace ApiInventoryTool.Tests;

/// <summary>
///     Verifies the approved-breaks file: validation and deterministic
///     matching against previous-package failures, including the dormant,
///     obsolete, and wrong-assembly classifications.
/// </summary>
public class ApprovedBreaksTests
{
    [Fact]
    public void Load_EmptyBreaksArray_IsValid()
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var path = WriteFile(directory, "approved.json", """{ "breaks": [] }""");

            var result = ApprovedBreaksFile.Load(path);

            Assert.True(result.Succeeded, string.Join('\n', result.Errors));
            Assert.Empty(result.Breaks);
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }

    [Fact]
    public void Load_ValidEntry_IsReturned()
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var path = WriteFile(directory, "approved.json",
                """
                {
                  "breaks": [
                    {
                      "assembly": "Origo.Core.Contracts",
                      "api": "M:Origo.Core.Abstractions.Blackboard.IBlackboard.(bool found, T value) TryGet<T>(string key)",
                      "reason": "Pre-0.1 nullable annotation correction"
                    }
                  ]
                }
                """);

            var result = ApprovedBreaksFile.Load(path);

            Assert.True(result.Succeeded, string.Join('\n', result.Errors));
            var entry = Assert.Single(result.Breaks);
            Assert.Equal("Origo.Core.Contracts", entry.Assembly);
            Assert.Contains("TryGet<T>", entry.Api, StringComparison.Ordinal);
            Assert.Equal("Pre-0.1 nullable annotation correction", entry.Reason);
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }

    [Theory]
    [InlineData("""{ "breaks": [ { "assembly": "", "api": "M:A.B()", "reason": "r" } ] }""", "empty assembly")]
    [InlineData("""{ "breaks": [ { "assembly": "A", "api": "", "reason": "r" } ] }""", "empty api line")]
    [InlineData("""{ "breaks": [ { "assembly": "A", "api": "M:A.B()", "reason": "" } ] }""", "non-empty reason")]
    [InlineData("""{ "breaks": [ { "assembly": "A", "api": "M:A.B()" } ] }""", "non-empty reason")]
    public void Load_InvalidEntry_FailsExplicitly(string json, string expectedError)
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var path = WriteFile(directory, "approved.json", json);

            var result = ApprovedBreaksFile.Load(path);

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors,
                error => error.Contains(expectedError, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }

    [Fact]
    public void Load_DuplicateEntry_FailsExplicitly()
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var path = WriteFile(directory, "approved.json",
                """
                {
                  "breaks": [
                    { "assembly": "A", "api": "M:A.B()", "reason": "first" },
                    { "assembly": "A", "api": "M:A.B()", "reason": "second" }
                  ]
                }
                """);

            var result = ApprovedBreaksFile.Load(path);

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, error => error.Contains("duplicates", StringComparison.Ordinal));
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }

    [Fact]
    public void Load_InvalidJson_FailsExplicitly()
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var path = WriteFile(directory, "approved.json", "{ not json");

            var result = ApprovedBreaksFile.Load(path);

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, error => error.Contains("not valid JSON", StringComparison.Ordinal));
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "breaks": null }""")]
    public void Load_MissingBreaksArray_FailsExplicitly(string json)
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var path = WriteFile(directory, "approved.json", json);

            var result = ApprovedBreaksFile.Load(path);

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors,
                error => error.Contains("missing its 'breaks' array", StringComparison.Ordinal));
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }

    [Fact]
    public void Load_MissingFile_FailsExplicitly()
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var result = ApprovedBreaksFile.Load(Path.Combine(directory, "missing.json"));

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, error => error.Contains("was not found", StringComparison.Ordinal));
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }

    [Fact]
    public void Apply_FiltersMatchingRemovalAndKeepsUnapprovedFailures()
    {
        var comparison = CreateComparison(
            [("A", "M:A.Removed()"), ("A", "M:A.Changed()")]);
        var approved = new[]
        {
            new ApprovedBreak { Assembly = "A", Api = "M:A.Removed()", Reason = "approved before 0.1.0" },
        };

        var result = ApprovedBreakFilter.Apply(comparison, approved);

        Assert.False(result.Succeeded);
        Assert.Equal(1, result.AppliedCount);
        var failure = Assert.Single(result.Failures);
        Assert.Contains("M:A.Changed()", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_AllFailuresApproved_Succeeds()
    {
        var comparison = CreateComparison(
            [("A", "M:A.Removed()"), ("B", "M:B.Changed()")]);
        var approved = new[]
        {
            new ApprovedBreak { Assembly = "A", Api = "M:A.Removed()", Reason = "approved" },
            new ApprovedBreak { Assembly = "B", Api = "M:B.Changed()", Reason = "approved" },
        };

        var result = ApprovedBreakFilter.Apply(comparison, approved);

        Assert.True(result.Succeeded, string.Join('\n', result.Failures));
        Assert.Equal(2, result.AppliedCount);
        Assert.Empty(result.ObsoleteApprovals);
    }

    [Fact]
    public void Apply_DormantApprovedBreak_FailsDeterministically()
    {
        // The previous baseline still contains the line and the current surface
        // still contains it too, so the approval approves nothing.
        var comparison = CreateComparison([], ("A", "M:A.Kept()"));
        var approved = new[]
        {
            new ApprovedBreak { Assembly = "A", Api = "M:A.Kept()", Reason = "approved" },
        };

        var result = ApprovedBreakFilter.Apply(comparison, approved);

        Assert.False(result.Succeeded);
        Assert.Equal(0, result.AppliedCount);
        var failure = Assert.Single(result.Failures);
        Assert.Contains("dormant", failure, StringComparison.Ordinal);
        Assert.Contains("M:A.Kept()", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_ObsoleteApprovedBreak_IsIgnoredAndReported()
    {
        // The line vanished from the previous baseline (a newer release absorbed
        // the change), so the approval is obsolete and must not fail the gate.
        var comparison = CreateComparison([], ("A", "M:A.Kept()"));
        var approved = new[]
        {
            new ApprovedBreak { Assembly = "A", Api = "M:A.Gone()", Reason = "approved" },
        };

        var result = ApprovedBreakFilter.Apply(comparison, approved);

        Assert.True(result.Succeeded, string.Join('\n', result.Failures));
        Assert.Equal(0, result.AppliedCount);
        var obsolete = Assert.Single(result.ObsoleteApprovals);
        Assert.Equal("A", obsolete.Assembly);
        Assert.Equal("M:A.Gone()", obsolete.Api);
    }

    [Fact]
    public void Apply_WrongAssemblyApprovedBreak_Fails()
    {
        var comparison = CreateComparison([], ("A", "M:A.Kept()"));
        var approved = new[]
        {
            new ApprovedBreak { Assembly = "B", Api = "M:A.Kept()", Reason = "approved" },
        };

        var result = ApprovedBreakFilter.Apply(comparison, approved);

        Assert.False(result.Succeeded);
        var failure = Assert.Single(result.Failures);
        Assert.Contains("names assembly 'B'", failure, StringComparison.Ordinal);
        Assert.Contains("belongs to 'A'", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_OneApprovalMatchesOnlyOneFailure()
    {
        var comparison = new PreviousPackageComparison(
            [],
            [new ApiRemoval("A", "M:A.Removed()"), new ApiRemoval("A", "M:A.Removed()")],
            new Dictionary<string, IReadOnlyCollection<string>>
            {
                ["A"] = ["M:A.Removed()"],
            });
        var approved = new[]
        {
            new ApprovedBreak { Assembly = "A", Api = "M:A.Removed()", Reason = "approved" },
        };

        var result = ApprovedBreakFilter.Apply(comparison, approved);

        Assert.False(result.Succeeded);
        Assert.Equal(1, result.AppliedCount);
        Assert.Single(result.Failures);
    }

    [Fact]
    public void Apply_MatchesFailureReportedThroughSchemaProjection()
    {
        // A version-1 previous baseline is compared through the schema-1
        // projection; the approved entry must still match the old line the
        // reviewer sees in the previous release baseline.
        var previous = new InventoryDocument
        {
            SchemaVersion = 1,
            Assemblies =
            [
                new AssemblyInventory { Name = "A", Api = ["M:A.Removed()"] },
            ],
        };
        var current = new InventoryDocument
        {
            SchemaVersion = 2,
            Assemblies = [new AssemblyInventory { Name = "A", Api = [] }],
        };
        var comparison = InventoryComparer.ComparePreviousDetailed(previous, current);
        var approved = new[]
        {
            new ApprovedBreak { Assembly = "A", Api = "M:A.Removed()", Reason = "approved" },
        };

        var result = ApprovedBreakFilter.Apply(comparison, approved);

        Assert.True(result.Succeeded, string.Join('\n', result.Failures));
        Assert.Equal(1, result.AppliedCount);
    }

    [Fact]
    public void Apply_MissingAssemblyFailure_CannotBeApproved()
    {
        var comparison = new PreviousPackageComparison(
            ["Origo.Core.Contracts"],
            [],
            new Dictionary<string, IReadOnlyCollection<string>>
            {
                ["Origo.Core.Contracts"] = ["T:public Interface X"],
            });
        var approved = new[]
        {
            new ApprovedBreak { Assembly = "Origo.Core.Contracts", Api = "T:public Interface X", Reason = "approved" },
        };

        var result = ApprovedBreakFilter.Apply(comparison, approved);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures,
            failure => failure.Contains("assembly missing", StringComparison.Ordinal));
        Assert.Contains(result.Failures,
            failure => failure.Contains("dormant", StringComparison.Ordinal));
    }

    [Fact]
    public void TrackedApprovedBreaksFile_IsValid()
    {
        var path = Path.Combine(FindRepoRoot(), ApprovedBreaksFile.DefaultRelativePath);
        Assert.True(File.Exists(path), $"Approved-breaks file not found: {path}");

        var result = ApprovedBreaksFile.Load(path);

        Assert.True(result.Succeeded, string.Join('\n', result.Errors));
    }

    /// <summary>
    ///     Builds a comparison whose reported failures are
    ///     <paramref name="removals" /> and whose projected previous-baseline
    ///     lines are <paramref name="previousLines" /> (defaulting to the
    ///     removals).
    /// </summary>
    private static PreviousPackageComparison CreateComparison(
        (string Assembly, string Api)[] removals,
        params (string Assembly, string Api)[] previousLines)
    {
        var previous = previousLines.Length == 0 ? removals : previousLines;
        var byAssembly = previous
            .GroupBy(line => line.Assembly, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<string>)[.. group.Select(line => line.Api)],
                StringComparer.Ordinal);
        return new PreviousPackageComparison(
            [],
            [.. removals.Select(removal => new ApiRemoval(removal.Assembly, removal.Api))],
            byAssembly);
    }

    private static string WriteFile(string directory, string name, string content)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Origo.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
    }
}
