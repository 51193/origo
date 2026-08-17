using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Origo.TestSupport;

/// <summary>
///     Enforces the repository's private-field naming convention
///     (<c>_camelCase</c>) through reflection. The convention is defined in
///     .editorconfig but dotnet format cannot verify fix-only naming rules, so
///     architecture tests use this helper as the CI-enforceable gate.
/// </summary>
public static class PrivateFieldNamingConvention
{
    /// <summary>Returns fully qualified names of private fields that violate the convention.</summary>
    public static IReadOnlyList<string> FindViolations(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var violations = new List<string>();
        foreach (var type in GetLoadableTypes(assembly))
        {
            if (ShouldSkipType(type))
                continue;
            foreach (var field in type.GetFields(
                         BindingFlags.Instance | BindingFlags.Static |
                         BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (!field.IsPrivate)
                    continue;
                if (field.GetCustomAttribute<CompilerGeneratedAttribute>() is not null)
                    continue;

                var name = field.Name;
                if (name.Length < 2 || name[0] != '_' || !char.IsLower(name[1]))
                    violations.Add($"{type.FullName}.{name}");
            }
        }

        violations.Sort(StringComparer.Ordinal);
        return violations;
    }

    private static bool ShouldSkipType(Type type)
    {
        if (type.FullName?.StartsWith("Coverlet.", StringComparison.Ordinal) == true)
            return true;

        // Compiler-generated iterator/async state machines, anonymous types,
        // and closure display classes are not source-authored fields.
        if (type.GetCustomAttribute<CompilerGeneratedAttribute>() is not null
            || type.Name.Contains('<', StringComparison.Ordinal))
            return true;

        return false;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            var loaded = new List<Type>();
            foreach (var type in ex.Types)
                if (type is not null)
                    loaded.Add(type);
            return loaded;
        }
    }
}
