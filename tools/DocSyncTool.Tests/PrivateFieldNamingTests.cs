using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace DocSyncTool.Tests;

public class PrivateFieldNamingTests
{
    [Fact]
    public void PrivateFields_FollowUnderscoreCamelCase()
    {
        var assembly = typeof(DocSyncTool.Program).Assembly;
        var violations = new List<string>();
        foreach (var type in assembly.GetTypes())
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

        Assert.Empty(violations);
    }
}
