using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Origo.TestSupport;

/// <summary>
///     Reads the tracked shell API classification table and verifies that every
///     exported type of an Origo assembly has exactly one classified entry. The
///     guard is intentionally type-level: the member-level Roslyn baseline is a
///     separate follow-up, while an unclassified exported type must already fail
///     architecture tests.
/// </summary>
public static class ShellApiClassificationInventory
{
    private const string _startMarker = "<!-- shell-api-classification:start -->";
    private const string _endMarker = "<!-- shell-api-classification:end -->";
    private const int _columnCount = 7;

    private static readonly HashSet<string> _allowedClassifications = new(StringComparer.Ordinal)
    {
        "Shell contract",
        "Tooling extension",
        "Kernel implementation",
        "Test-only"
    };

    private static readonly HashSet<string> _allowedAssemblies = new(StringComparer.Ordinal)
    {
        "Origo.Core",
        "Origo.GodotAdapter",
        "Origo.ConsoleBridge"
    };

    private static readonly HashSet<string> _allowedPackageOwners = new(StringComparer.Ordinal)
    {
        "Origo.Core.Contracts",
        "Origo.Core",
        "Origo.Core.Kernel",
        "Origo.GodotAdapter",
        "Origo.ConsoleBridge",
        "Origo.TestSupport"
    };

    /// <summary>
    ///     Returns classification-coverage violations for <paramref name="assembly" />.
    ///     An empty list means the table is valid and covers every exported type.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assembly" /> is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the tracked classification table is absent.</exception>
    public static IReadOnlyList<string> FindViolations(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var repositoryRoot = FindRepositoryRoot();
        var englishEntries = ParseTable(Path.Combine(
            repositoryRoot, "docs", "architecture", "shell-api-classification.en.md"));
        var chineseEntries = ParseTable(Path.Combine(
            repositoryRoot, "docs", "architecture", "shell-api-classification.zh.md"));

        var violations = new List<string>();
        ValidateRows(englishEntries, "en", violations);
        ValidateRows(chineseEntries, "zh", violations);
        CompareLanguageTables(englishEntries, chineseEntries, violations);

        var assemblyName = assembly.GetName().Name
            ?? throw new InvalidOperationException($"Assembly '{assembly.FullName}' has no simple name.");
        var exportedTypeNames = assembly.GetExportedTypes()
            .Select(type => type.FullName ?? type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var classifiedTypeNames = englishEntries
            .Where(entry => string.Equals(entry.Assembly, assemblyName, StringComparison.Ordinal))
            .Select(entry => entry.Type)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        foreach (var missing in exportedTypeNames.Except(classifiedTypeNames, StringComparer.Ordinal))
            violations.Add($"{assemblyName}: exported type '{missing}' is not classified.");

        foreach (var unexpected in classifiedTypeNames.Except(exportedTypeNames, StringComparer.Ordinal))
            violations.Add($"{assemblyName}: classified type '{unexpected}' is not exported at this commit.");

        return violations;
    }

    private static List<ShellApiClassificationEntry> ParseTable(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(
                "The shell API classification table is missing. Add both language files before updating the classification inventory.",
                path);

        var lines = File.ReadAllLines(path);
        var startIndex = Array.FindIndex(lines,
            line => line.Contains(_startMarker, StringComparison.Ordinal));
        var endIndex = Array.FindIndex(lines,
            line => line.Contains(_endMarker, StringComparison.Ordinal));

        if (startIndex < 0 || endIndex <= startIndex)
            throw new InvalidDataException(
                $"The shell API classification markers are missing or out of order in '{path}'.");

        var headerIndex = -1;
        for (var index = startIndex + 1; index < endIndex; index++)
        {
            if (!lines[index].TrimStart().StartsWith('|'))
                continue;

            headerIndex = index;
            break;
        }

        if (headerIndex < 0)
            throw new InvalidDataException($"The shell API classification table has no header in '{path}'.");

        var entries = new List<ShellApiClassificationEntry>();
        for (var index = headerIndex + 2; index < endIndex; index++)
        {
            var line = lines[index].Trim();
            if (line.Length == 0)
                continue;
            if (!line.StartsWith('|'))
                continue;

            var cells = SplitTableRow(line, path, index + 1);
            if (cells.Length != _columnCount)
            {
                throw new InvalidDataException(
                    $"'{path}' line {index + 1} has {cells.Length} columns; expected {_columnCount}.");
            }

            entries.Add(new ShellApiClassificationEntry(
                StripInlineCode(cells[0]),
                StripInlineCode(cells[1]),
                StripInlineCode(cells[2]),
                StripInlineCode(cells[3]),
                StripInlineCode(cells[4]),
                cells[5].Trim(),
                cells[6].Trim()));
        }

        return entries;
    }

    private static string[] SplitTableRow(string line, string path, int lineNumber)
    {
        if (!line.EndsWith('|'))
            throw new InvalidDataException($"'{path}' line {lineNumber} does not close its Markdown table row.");

        return [.. line[1..^1].Split('|').Select(cell => cell.Trim())];
    }

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

    private static void ValidateRows(
        IReadOnlyList<ShellApiClassificationEntry> entries,
        string language,
        List<string> violations)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Type)
                || string.IsNullOrWhiteSpace(entry.Assembly)
                || string.IsNullOrWhiteSpace(entry.Classification)
                || string.IsNullOrWhiteSpace(entry.PackageOwner)
                || string.IsNullOrWhiteSpace(entry.CapabilityGroup)
                || string.IsNullOrWhiteSpace(entry.Rationale)
                || string.IsNullOrWhiteSpace(entry.OwnerRemovalCondition))
            {
                violations.Add($"{language}: classification row has an empty required field: {entry}");
                continue;
            }

            if (!_allowedAssemblies.Contains(entry.Assembly))
                violations.Add($"{language}: unknown assembly '{entry.Assembly}' for '{entry.Type}'.");
            if (!_allowedClassifications.Contains(entry.Classification))
                violations.Add($"{language}: unknown classification '{entry.Classification}' for '{entry.Type}'.");
            if (!_allowedPackageOwners.Contains(entry.PackageOwner))
                violations.Add($"{language}: unknown package owner '{entry.PackageOwner}' for '{entry.Type}'.");

            var key = $"{entry.Assembly}|{entry.Type}";
            if (!seen.Add(key))
                violations.Add($"{language}: duplicate classification row for '{key}'.");

            if (string.Equals(entry.Classification, "Shell contract", StringComparison.Ordinal)
                || string.Equals(entry.Classification, "Tooling extension", StringComparison.Ordinal))
            {
                if (!entry.OwnerRemovalCondition.Contains("R-", StringComparison.Ordinal))
                {
                    violations.Add(
                        $"{language}: compatible API '{entry.Type}' must reference a concrete removal condition.");
                }
            }
        }
    }

    private static void CompareLanguageTables(
        IReadOnlyList<ShellApiClassificationEntry> englishEntries,
        IReadOnlyList<ShellApiClassificationEntry> chineseEntries,
        List<string> violations)
    {
        static string[] Keys(IEnumerable<ShellApiClassificationEntry> entries) =>
            [.. entries.Select(entry =>
                    $"{entry.Assembly}|{entry.Type}|{entry.Classification}|{entry.PackageOwner}|" +
                    $"{entry.CapabilityGroup}|{entry.OwnerRemovalCondition}")
                .OrderBy(key => key, StringComparer.Ordinal)];

        var englishKeys = Keys(englishEntries);
        var chineseKeys = Keys(chineseEntries);
        if (!englishKeys.SequenceEqual(chineseKeys, StringComparer.Ordinal))
        {
            var missingInChinese = englishKeys.Except(chineseKeys, StringComparer.Ordinal);
            var missingInEnglish = chineseKeys.Except(englishKeys, StringComparer.Ordinal);
            violations.Add(
                "Bilingual classification tables do not carry the same type metadata. " +
                $"Missing in zh: [{string.Join(", ", missingInChinese)}]; " +
                $"missing in en: [{string.Join(", ", missingInEnglish)}].");
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Origo.sln"))
                && File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate the Origo repository root from the test output directory. " +
            "Architecture classification guards require the tracked docs tree.");
    }

    private sealed record ShellApiClassificationEntry(
        string Type,
        string Assembly,
        string Classification,
        string PackageOwner,
        string CapabilityGroup,
        string Rationale,
        string OwnerRemovalCondition);
}
