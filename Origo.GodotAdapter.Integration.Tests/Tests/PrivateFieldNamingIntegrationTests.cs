using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Origo.GodotAdapter.Integration.Tests.Runner;

namespace Origo.GodotAdapter.Integration.Tests;

public class PrivateFieldNamingIntegrationTests
{
    [IntegrationTest(Description = "Integration test assembly private fields follow the repository _camelCase convention")]
    public void PrivateFields_FollowUnderscoreCamelCase()
    {
        var violations = FindViolations(typeof(IntegrationTestRunner).Assembly);
        IntegrationTestRunner.Assert(
            violations.Count == 0,
            "Private fields violating _camelCase: " + string.Join(", ", violations));
    }

    private static List<string> FindViolations(Assembly assembly)
    {
        var violations = new List<string>();
        foreach (var type in GetLoadableTypes(assembly))
        {
            if (type.FullName?.StartsWith("Coverlet.", StringComparison.Ordinal) == true
                || type.GetCustomAttribute<CompilerGeneratedAttribute>() is not null
                || type.Name.Contains('<', StringComparison.Ordinal))
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
