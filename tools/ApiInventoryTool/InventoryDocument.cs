using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiInventoryTool;

/// <summary>Serializable shell API inventory document.</summary>
internal sealed class InventoryDocument
{
    /// <summary>
    ///     Schema version of this document; bump when the serialized shape changes.
    ///     Version 2 records generic variance and nullable-flow attributes on top of
    ///     the version-1 member lines.
    /// </summary>
    public int SchemaVersion { get; set; } = 2;

    /// <summary>Inventories keyed by shell assembly simple name.</summary>
    public List<AssemblyInventory> Assemblies { get; set; } = [];
}

/// <summary>API lines for one shell assembly.</summary>
internal sealed class AssemblyInventory
{
    /// <summary>Shell assembly simple name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Canonical, sorted API signature lines.</summary>
    public List<string> Api { get; set; } = [];
}

/// <summary>Canonical JSON serialization helpers for the inventory.</summary>
internal static class InventoryJson
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>Serializes to canonical JSON with LF line endings and a trailing newline.</summary>
    public static string Serialize(InventoryDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.Serialize(document, _options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    /// <summary>Deserializes an inventory document.</summary>
    public static InventoryDocument Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize<InventoryDocument>(json, _options)
            ?? throw new InvalidOperationException("The inventory JSON produced no document.");
    }
}

/// <summary>One removed-or-changed API line reported by the previous-package comparison.</summary>
internal sealed record ApiRemoval(string Assembly, string Api);

/// <summary>Structured previous-package comparison result.</summary>
/// <param name="MissingAssemblies">Assemblies present in the previous baseline but absent from current.</param>
/// <param name="RemovedOrChanged">Previous-baseline lines that no longer exist in the current surface.</param>
/// <param name="PreviousApiByAssembly">
///     Projected previous-baseline lines keyed by shell assembly. The approved-break
///     filter uses this to distinguish a dormant approval (the line is still part of
///     the previous baseline) from an obsolete approval (a newer release removed the
///     line, so the approval has expired).
/// </param>
internal sealed record PreviousPackageComparison(
    IReadOnlyList<string> MissingAssemblies,
    IReadOnlyList<ApiRemoval> RemovedOrChanged,
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> PreviousApiByAssembly);

/// <summary>Reports API differences between two inventories.</summary>
internal static class InventoryComparer
{
    /// <summary>
    ///     Exact compare used by the shell baseline gate: any addition,
    ///     removal, or signature change fails until the baseline is updated in
    ///     the same reviewed change.
    /// </summary>
    public static IReadOnlyList<string> Verify(InventoryDocument baseline, InventoryDocument current)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);

        var changes = new List<string>();
        var baselineAssemblies = baseline.Assemblies.ToDictionary(a => a.Name, StringComparer.Ordinal);
        var currentAssemblies = current.Assemblies.ToDictionary(a => a.Name, StringComparer.Ordinal);

        foreach (var name in baselineAssemblies.Keys.Except(currentAssemblies.Keys, StringComparer.Ordinal).Order())
            changes.Add($"assembly removed: {name}");
        foreach (var name in currentAssemblies.Keys.Except(baselineAssemblies.Keys, StringComparer.Ordinal).Order())
            changes.Add($"assembly added: {name}");

        foreach (var name in baselineAssemblies.Keys.Intersect(currentAssemblies.Keys, StringComparer.Ordinal).Order())
        {
            var before = baselineAssemblies[name].Api.ToHashSet(StringComparer.Ordinal);
            var after = currentAssemblies[name].Api.ToHashSet(StringComparer.Ordinal);
            foreach (var line in before.Except(after, StringComparer.Ordinal).Order())
                changes.Add($"{name}: removed or changed API: {line}");
            foreach (var line in after.Except(before, StringComparer.Ordinal).Order())
                changes.Add($"{name}: added API: {line}");
        }

        return changes;
    }

    /// <summary>
    ///     Previous-package compare: removals and signature changes fail because
    ///     0.1.x promises source compatibility; documented additions are allowed.
    ///     A previous baseline written with an older schema is projected onto the
    ///     version-1 line shape first, so the enriched variance/attribute segments
    ///     do not read as false removals. That projection hides changes confined
    ///     to the enriched segments for one release window; the committed-baseline
    ///     <see cref="Verify" /> gate enforces the enriched shape immediately.
    /// </summary>
    public static IReadOnlyList<string> ComparePrevious(InventoryDocument previous, InventoryDocument current) =>
        FormatFailures(ComparePreviousDetailed(previous, current));

    /// <summary>
    ///     Structured form of <see cref="ComparePrevious" />; the approved-breaks
    ///     filter needs the assembly/api pair instead of the rendered message.
    /// </summary>
    internal static PreviousPackageComparison ComparePreviousDetailed(
        InventoryDocument previous,
        InventoryDocument current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        if (previous.SchemaVersion != current.SchemaVersion)
        {
            previous = ProjectToSchemaOne(previous);
            current = ProjectToSchemaOne(current);
        }

        var missingAssemblies = previous.Assemblies
            .Select(assembly => assembly.Name)
            .Except(current.Assemblies.Select(assembly => assembly.Name), StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var removedOrChanged = new List<ApiRemoval>();
        var previousAssemblies = previous.Assemblies.ToDictionary(a => a.Name, StringComparer.Ordinal);
        var currentAssemblies = current.Assemblies.ToDictionary(a => a.Name, StringComparer.Ordinal);

        foreach (var name in previousAssemblies.Keys.Intersect(currentAssemblies.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var currentApi = currentAssemblies[name].Api.ToHashSet(StringComparer.Ordinal);
            foreach (var line in previousAssemblies[name].Api.Where(line => !currentApi.Contains(line)).Order(StringComparer.Ordinal))
                removedOrChanged.Add(new ApiRemoval(name, line));
        }

        var previousApiByAssembly = previousAssemblies.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyCollection<string>)pair.Value.Api,
            StringComparer.Ordinal);

        return new PreviousPackageComparison(missingAssemblies, removedOrChanged, previousApiByAssembly);
    }

    /// <summary>Renders a structured comparison into the stable failure-message list.</summary>
    internal static IReadOnlyList<string> FormatFailures(PreviousPackageComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        var failures = new List<string>();
        foreach (var name in comparison.MissingAssemblies)
            failures.Add($"assembly missing from current packages: {name}");
        foreach (var removal in comparison.RemovedOrChanged)
            failures.Add($"{removal.Assembly}: removed or changed API: {removal.Api}");
        return failures;
    }

    /// <summary>
    ///     Projects a line onto the schema-1 shape by removing the nullable-flow
    ///     attribute segments and generic variance markers. Exposed for focused
    ///     tests of the previous-package compatibility path.
    /// </summary>
    internal static string ProjectLineToSchemaOne(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return RemoveGenericVariance(RemoveFlowAttributeSegments(line));
    }

    private static InventoryDocument ProjectToSchemaOne(InventoryDocument document) => new()
    {
        SchemaVersion = 1,
        Assemblies =
        [
            .. document.Assemblies.Select(assembly => new AssemblyInventory
            {
                Name = assembly.Name,
                Api = [.. assembly.Api.Select(ProjectLineToSchemaOne)],
            }),
        ],
    };

    private static string RemoveFlowAttributeSegments(string line)
    {
        var builder = new StringBuilder(line.Length);
        var index = 0;
        while (index < line.Length)
        {
            if (line[index] == '[')
            {
                var closing = line.IndexOf(']', index);
                if (closing > index)
                {
                    var segment = line[index..(closing + 1)];
                    if (segment.StartsWith("[return: ", StringComparison.Ordinal)
                        || segment.StartsWith("[System.Diagnostics.CodeAnalysis.", StringComparison.Ordinal))
                    {
                        index = closing + 1;
                        if (index < line.Length && line[index] == ' ')
                            index++;
                        continue;
                    }
                }
            }

            builder.Append(line[index]);
            index++;
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Removes <c>out</c>/<c>in</c> variance markers from the rendered lines.
    ///     Parameter modifiers such as <c>out string? value</c> stay untouched.
    ///     The inventory itself renders variance only on type declarations and
    ///     member prefixes, so a constructed use never carries a marker today;
    ///     the projection is textual and strips any marker it sees at generic
    ///     depth, which is the safe shape for comparing against schema 1.
    /// </summary>
    internal static string RemoveGenericVariance(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var builder = new StringBuilder(line.Length);
        var depth = 0;
        for (var index = 0; index < line.Length; index++)
        {
            var current = line[index];
            if (current == '<')
            {
                depth++;
                builder.Append(current);
                continue;
            }

            if (current == '>')
            {
                if (depth > 0)
                    depth--;
                builder.Append(current);
                continue;
            }

            if (depth > 0 && IsVarianceMarkerAt(line, index))
            {
                index += line[index] == 'o' ? 3 : 2;
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    private static bool IsVarianceMarkerAt(string line, int index)
    {
        var previous = index - 1;
        while (previous >= 0 && line[previous] == ' ')
            previous--;
        if (previous < 0 || line[previous] is not ('<' or ','))
            return false;

        return (index + 4 <= line.Length && string.CompareOrdinal(line, index, "out ", 0, 4) == 0)
            || (index + 3 <= line.Length && string.CompareOrdinal(line, index, "in ", 0, 3) == 0);
    }
}
