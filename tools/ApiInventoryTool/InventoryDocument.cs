using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiInventoryTool;

/// <summary>Serializable shell API inventory document.</summary>
internal sealed class InventoryDocument
{
    /// <summary>Schema version of this document; bump when the serialized shape changes.</summary>
    public int SchemaVersion { get; set; } = 1;

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
    /// </summary>
    public static IReadOnlyList<string> ComparePrevious(InventoryDocument previous, InventoryDocument current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        var failures = new List<string>();
        var previousAssemblies = previous.Assemblies.ToDictionary(a => a.Name, StringComparer.Ordinal);
        var currentAssemblies = current.Assemblies.ToDictionary(a => a.Name, StringComparer.Ordinal);

        foreach (var name in previousAssemblies.Keys.Except(currentAssemblies.Keys, StringComparer.Ordinal).Order())
            failures.Add($"assembly missing from current packages: {name}");

        foreach (var name in previousAssemblies.Keys.Intersect(currentAssemblies.Keys, StringComparer.Ordinal).Order())
        {
            var currentApi = currentAssemblies[name].Api.ToHashSet(StringComparer.Ordinal);
            foreach (var line in previousAssemblies[name].Api.Where(line => !currentApi.Contains(line)).Order(StringComparer.Ordinal))
                failures.Add($"{name}: removed or changed API: {line}");
        }

        return failures;
    }
}
