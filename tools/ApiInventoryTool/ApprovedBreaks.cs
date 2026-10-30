using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ApiInventoryTool;

/// <summary>
///     One reviewed and approved shell API removal or signature change. The
///     <see cref="Api" /> value is the exact previous-baseline line that the
///     previous-package comparison reports as removed or changed; reviewers use
///     <see cref="Reason" /> to record why the break is acceptable before 0.1.0.
///     The entry stays required while the previous formal release still contains
///     the line; once a release that carries the new surface becomes the previous
///     baseline the entry is obsolete, is reported for cleanup, and no longer
///     fails the gate.
/// </summary>
internal sealed class ApprovedBreak
{
    /// <summary>Shell assembly simple name the removed line belongs to.</summary>
    public string Assembly { get; set; } = string.Empty;

    /// <summary>Exact previous-baseline API line that was removed or changed.</summary>
    public string Api { get; set; } = string.Empty;

    /// <summary>Why this break was approved by the maintainer; must be non-empty.</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Serialized approved-breaks document.</summary>
internal sealed class ApprovedBreaksDocument
{
    /// <summary>Approved breaks, ordered as they are reviewed.</summary>
    public List<ApprovedBreak>? Breaks { get; set; }
}

/// <summary>Result of loading and validating an approved-breaks file.</summary>
internal sealed record ApprovedBreaksLoadResult(IReadOnlyList<ApprovedBreak> Breaks, IReadOnlyList<string> Errors)
{
    /// <summary>True when the file parsed and every entry passed validation.</summary>
    public bool Succeeded => Errors.Count == 0;
}

/// <summary>
///     Loads and validates the tracked approved-breaks file. Only the compare
///     command consumes a loaded document; the tracked file is never rewritten
///     by the tool (approved breaks are review input, not generated output).
/// </summary>
internal static class ApprovedBreaksFile
{
    /// <summary>Repository-relative path of the tracked approved-breaks file.</summary>
    public const string DefaultRelativePath = "tools/ApiInventoryTool/shell-api-approved-breaks.json";

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Loads and validates the approved-breaks file at <paramref name="path" />.</summary>
    public static ApprovedBreaksLoadResult Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            return new ApprovedBreaksLoadResult([], [$"approved-breaks file was not found: '{path}'."]);

        ApprovedBreaksDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<ApprovedBreaksDocument>(
                File.ReadAllText(path),
                _options);
        }
        catch (JsonException exception)
        {
            return new ApprovedBreaksLoadResult(
                [],
                [$"approved-breaks file '{path}' is not valid JSON: {exception.Message}"]);
        }

        if (document is null)
            return new ApprovedBreaksLoadResult([], [$"approved-breaks file '{path}' contains no document."]);
        var entries = document.Breaks;
        if (entries is null)
            return new ApprovedBreaksLoadResult([], [$"approved-breaks file '{path}' is missing its 'breaks' array."]);

        var errors = new List<string>();
        var breaks = new List<ApprovedBreak>();
        var seen = new HashSet<(string Assembly, string Api)>();
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var label = $"approved break #{index + 1}";
            if (entry is null)
            {
                errors.Add($"{label} is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.Assembly))
                errors.Add($"{label} has an empty assembly.");
            if (string.IsNullOrWhiteSpace(entry.Api))
                errors.Add($"{label} has an empty api line.");
            if (string.IsNullOrWhiteSpace(entry.Reason))
                errors.Add($"{label} must have a non-empty reason.");

            if (string.IsNullOrWhiteSpace(entry.Assembly) || string.IsNullOrWhiteSpace(entry.Api))
                continue;

            if (!seen.Add((entry.Assembly, entry.Api)))
            {
                errors.Add($"{label} duplicates assembly '{entry.Assembly}' and api '{entry.Api}'.");
                continue;
            }

            breaks.Add(entry);
        }

        return errors.Count == 0
            ? new ApprovedBreaksLoadResult(breaks, [])
            : new ApprovedBreaksLoadResult([], errors);
    }
}

/// <summary>Outcome of applying the approved breaks to a previous-package comparison.</summary>
/// <param name="Failures">Unapproved failures plus dormant/misattributed approvals.</param>
/// <param name="AppliedCount">Approvals that matched one removed-or-changed line.</param>
/// <param name="ObsoleteApprovals">
///     Approvals whose line is no longer part of the previous baseline. They are
///     ignored (a newer release already absorbed the change) but reported so the
///     tracked file can be cleaned up.
/// </param>
internal sealed record ApprovedBreakResult(
    IReadOnlyList<string> Failures,
    int AppliedCount,
    IReadOnlyList<ApiRemoval> ObsoleteApprovals)
{
    /// <summary>True when no unapproved, dormant, or misattributed approval remains.</summary>
    public bool Succeeded => Failures.Count == 0;
}

/// <summary>
///     Applies approved breaks to a structured previous-package comparison. Each
///     approved entry matches at most one removed-or-changed line. An unmatched
///     approval is classified against the previous baseline: if the line is still
///     present the approval is dormant (it approves nothing) and fails the gate; if
///     the line is gone from the previous baseline the approval is obsolete (a newer
///     release already contains the change) and is ignored but reported for cleanup;
///     if the line belongs to a different assembly the entry is invalid and fails.
///     Missing-assembly failures cannot be approved.
/// </summary>
internal static class ApprovedBreakFilter
{
    /// <summary>Filters <paramref name="comparison" /> failures through the approved breaks.</summary>
    public static ApprovedBreakResult Apply(
        PreviousPackageComparison comparison,
        IReadOnlyList<ApprovedBreak> approvedBreaks)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentNullException.ThrowIfNull(approvedBreaks);

        var failures = new List<string>();
        foreach (var name in comparison.MissingAssemblies)
            failures.Add($"assembly missing from current packages: {name}");

        var matched = new bool[approvedBreaks.Count];
        foreach (var removal in comparison.RemovedOrChanged)
        {
            var matchedIndex = -1;
            for (var index = 0; index < approvedBreaks.Count; index++)
            {
                if (matched[index])
                    continue;
                if (string.Equals(approvedBreaks[index].Assembly, removal.Assembly, StringComparison.Ordinal)
                    && string.Equals(approvedBreaks[index].Api, removal.Api, StringComparison.Ordinal))
                {
                    matchedIndex = index;
                    break;
                }
            }

            if (matchedIndex >= 0)
            {
                matched[matchedIndex] = true;
                continue;
            }

            failures.Add($"{removal.Assembly}: removed or changed API: {removal.Api}");
        }

        var obsolete = new List<ApiRemoval>();
        var assembliesByLine = BuildAssembliesByLine(comparison.PreviousApiByAssembly);
        for (var index = 0; index < approvedBreaks.Count; index++)
        {
            if (matched[index])
                continue;

            var approved = approvedBreaks[index];
            if (comparison.PreviousApiByAssembly.TryGetValue(approved.Assembly, out var previousLines)
                && previousLines.Contains(approved.Api))
            {
                // The previous release still carries the line, but the current
                // surface does too, so the approval suppresses nothing.
                failures.Add(
                    "approved break is dormant (the previous baseline still contains the line): " +
                    $"{approved.Assembly}: {approved.Api}");
                continue;
            }

            if (assembliesByLine.TryGetValue(approved.Api, out var owningAssemblies)
                && owningAssemblies.Count > 0
                && !owningAssemblies.Contains(approved.Assembly))
            {
                failures.Add(
                    $"approved break names assembly '{approved.Assembly}', but the previous baseline line belongs to " +
                    $"'{owningAssemblies[0]}': {approved.Api}");
                continue;
            }

            obsolete.Add(new ApiRemoval(approved.Assembly, approved.Api));
        }

        return new ApprovedBreakResult(failures, matched.Count(value => value), obsolete);
    }

    private static Dictionary<string, List<string>> BuildAssembliesByLine(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> previousApiByAssembly)
    {
        var byLine = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (assembly, lines) in previousApiByAssembly)
        {
            foreach (var line in lines)
            {
                if (!byLine.TryGetValue(line, out var owningAssemblies))
                {
                    owningAssemblies = [];
                    byLine[line] = owningAssemblies;
                }

                owningAssemblies.Add(assembly);
            }
        }

        foreach (var owningAssemblies in byLine.Values)
            owningAssemblies.Sort(StringComparer.Ordinal);
        return byLine;
    }
}
