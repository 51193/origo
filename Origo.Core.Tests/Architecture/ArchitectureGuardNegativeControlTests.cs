using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Origo.Core;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Negative controls for the architecture-guard helpers. Every positive
///     guard asserts an empty violation list, so a detector that silently
///     stopped reporting violations would pass all of them; these controls feed
///     known-bad inputs and require the detector to report them.
/// </summary>
public class ArchitectureGuardNegativeControlTests
{
    private const string _classificationTableStartMarker = "<!-- shell-api-classification:start -->";
    private const string _classificationTableEndMarker = "<!-- shell-api-classification:end -->";
    private const int _classificationColumnCount = 7;

    // The deliberately non-conforming private fields below exist so the
    // detector can be proven to report violations. dotnet format cannot verify
    // fix-only naming rules (see .editorconfig), and no production guard scans
    // this test assembly, so the fixture does not weaken a real gate. Each field
    // is read through a member so the dead-code analyzers keep it.
    private sealed class CleanFieldFixture
    {
        private readonly int _value = 1;
        private readonly int _other = 2;

        public int Total => _value + _other;
    }

    private sealed class ViolatingFieldFixture
    {
        private static readonly int staticName = 4;
        private readonly int violatingName = 3;

        public int InstanceValue => violatingName;

        public static int StaticValue => staticName;
    }

    [Fact]
    public void PrivateFieldNamingConvention_ReportsNonConformingPrivateFields()
    {
        Assert.Equal(3, new CleanFieldFixture().Total);
        Assert.Equal(3, new ViolatingFieldFixture().InstanceValue);
        Assert.Equal(4, ViolatingFieldFixture.StaticValue);

        var violations = PrivateFieldNamingConvention.FindViolations(
            [typeof(CleanFieldFixture), typeof(ViolatingFieldFixture)]);

        Assert.Equal(
            [
                $"{typeof(ViolatingFieldFixture).FullName}.staticName",
                $"{typeof(ViolatingFieldFixture).FullName}.violatingName"
            ],
            violations);
    }

    [Fact]
    public void PrivateFieldNamingConvention_CleanFixture_ReportsNoViolations() =>
        Assert.Empty(PrivateFieldNamingConvention.FindViolations([typeof(CleanFieldFixture)]));

    [Fact]
    public void PrivateFieldNamingConvention_AssemblyScan_ReportsFixtureViolations()
    {
        var violations = PrivateFieldNamingConvention.FindViolations(
            typeof(ArchitectureGuardNegativeControlTests).Assembly);

        Assert.Contains($"{typeof(ViolatingFieldFixture).FullName}.staticName", violations);
        Assert.Contains($"{typeof(ViolatingFieldFixture).FullName}.violatingName", violations);
    }

    [Fact]
    public void ShellApiClassification_ValidTable_ReportsNoViolations() =>
        WithModifiedTable(_ => { }, tablePath =>
        {
            var violations = ShellApiClassificationInventory.FindViolations(typeof(OrigoHost).Assembly, tablePath);
            Assert.Empty(violations);
        });

    [Fact]
    public void ShellApiClassification_MissingRow_ReportsUnclassifiedType()
    {
        var removedType = string.Empty;
        WithModifiedTable(lines =>
        {
            var rowIndex = FindCoreRowIndex(lines);
            removedType = StripInlineCode(SplitTableRow(lines[rowIndex])![0]);
            lines.RemoveAt(rowIndex);
        }, tablePath =>
        {
            var violations = ShellApiClassificationInventory.FindViolations(typeof(OrigoHost).Assembly, tablePath);
            Assert.Contains(violations, violation =>
                violation.Contains(removedType, StringComparison.Ordinal)
                && violation.Contains("is not classified", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void ShellApiClassification_UnknownClassification_IsReported() =>
        WithModifiedTable(lines =>
        {
            var rowIndex = FindCoreRowIndex(lines);
            var cells = SplitTableRow(lines[rowIndex])!;
            cells[2] = "Bogus classification";
            lines[rowIndex] = FormatTableRow(cells);
        }, tablePath =>
        {
            var violations = ShellApiClassificationInventory.FindViolations(typeof(OrigoHost).Assembly, tablePath);
            Assert.Contains(violations, violation =>
                violation.Contains("unknown classification 'Bogus classification'", StringComparison.Ordinal));
        });

    [Fact]
    public void ShellApiClassification_NotExportedRow_IsReported() =>
        WithModifiedTable(lines =>
        {
            var endIndex = lines.FindIndex(line =>
                line.Contains(_classificationTableEndMarker, StringComparison.Ordinal));
            lines.Insert(endIndex,
                "| <code>Origo.Core.BogusNegativeControlType</code> | Origo.Core | Shell contract | " +
                "Origo.Core | guard-control | Fixture row proving stale rows are reported. | core-shell; R-HOST |");
        }, tablePath =>
        {
            var violations = ShellApiClassificationInventory.FindViolations(typeof(OrigoHost).Assembly, tablePath);
            Assert.Contains(violations, violation =>
                violation.Contains("Origo.Core.BogusNegativeControlType", StringComparison.Ordinal)
                && violation.Contains("is not exported at this commit", StringComparison.Ordinal));
        });

    [Fact]
    public void ShellApiClassification_DuplicateRow_IsReported() =>
        WithModifiedTable(lines =>
        {
            var rowIndex = FindCoreRowIndex(lines);
            lines.Insert(rowIndex + 1, lines[rowIndex]);
        }, tablePath =>
        {
            var violations = ShellApiClassificationInventory.FindViolations(typeof(OrigoHost).Assembly, tablePath);
            Assert.Contains(violations, violation =>
                violation.Contains("duplicate classification row", StringComparison.Ordinal));
        });

    [Fact]
    public void ShellApiClassification_MissingTable_Throws()
    {
        var missingPath = Path.Combine(
            Path.GetTempPath(), "origo-classification-missing-" + Guid.NewGuid().ToString("N") + ".md");

        Assert.Throws<FileNotFoundException>(() =>
            ShellApiClassificationInventory.FindViolations(typeof(OrigoHost).Assembly, missingPath));
    }

    private static void WithModifiedTable(Action<List<string>> mutate, Action<string> verify)
    {
        var sourcePath = Path.Combine(
            FindRepositoryRoot(), "docs", "architecture", "shell-api-classification.en.md");
        var lines = File.ReadAllLines(sourcePath).ToList();
        mutate(lines);

        var directory = Path.Combine(
            Path.GetTempPath(), "origo-classification-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var tablePath = Path.Combine(directory, "shell-api-classification.en.md");
        try
        {
            File.WriteAllLines(tablePath, lines);
            verify(tablePath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static int FindCoreRowIndex(List<string> lines)
    {
        var startIndex = lines.FindIndex(line =>
            line.Contains(_classificationTableStartMarker, StringComparison.Ordinal));
        var endIndex = lines.FindIndex(line =>
            line.Contains(_classificationTableEndMarker, StringComparison.Ordinal));
        if (startIndex < 0 || endIndex <= startIndex)
            throw new InvalidOperationException("Classification table markers are missing or out of order.");

        for (var index = startIndex + 1; index < endIndex; index++)
        {
            var cells = SplitTableRow(lines[index]);
            if (cells is not null && cells.Length == _classificationColumnCount && cells[1] == "Origo.Core")
                return index;
        }

        throw new InvalidOperationException("The tracked classification table has no Origo.Core row.");
    }

    private static string[]? SplitTableRow(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length < 2 || trimmed[0] != '|' || trimmed[^1] != '|')
            return null;

        return [.. trimmed[1..^1].Split('|').Select(cell => cell.Trim())];
    }

    private static string FormatTableRow(string[] cells) => $"| {string.Join(" | ", cells)} |";

    private static string StripInlineCode(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("<code>", StringComparison.Ordinal)
            && trimmed.EndsWith("</code>", StringComparison.Ordinal))
            return trimmed[6..^7].Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '`' && trimmed[^1] == '`')
            return trimmed[1..^1].Trim();
        return trimmed;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Origo.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate the repository root from the test output directory.");
    }
}
