using System;
using System.Linq;
using System.Reflection;
using Origo.ConsoleBridge;
using Origo.TestSupport;
using Xunit;

namespace Origo.ConsoleBridge.Tests;

public class ConsoleBridgeArchitectureGuardrailTests
{
    [Fact]
    public void PrivateFields_FollowUnderscoreCamelCase()
    {
        var violations = PrivateFieldNamingConvention.FindViolations(typeof(ConsoleBridgeServer).Assembly);
        Assert.Empty(violations);
    }

    [Fact]
    public void ConsoleBridge_ShouldNotReferenceGodot()
    {
        var asm = typeof(ConsoleBridgeServer).Assembly;
        var refs = asm.GetReferencedAssemblies();
        Assert.DoesNotContain(refs, r => r.Name!.StartsWith("Godot", StringComparison.Ordinal));
    }

    [Fact]
    public void ConsoleBridge_ShouldNotReferenceGodotAdapter()
    {
        var asm = typeof(ConsoleBridgeServer).Assembly;
        var refs = asm.GetReferencedAssemblies();
        Assert.DoesNotContain(refs, r => r.Name == "Origo.GodotAdapter");
    }

    [Fact]
    public void ConsoleBridge_ShouldOnlyReferenceContracts()
    {
        var asm = typeof(ConsoleBridgeServer).Assembly;
        var refs = asm.GetReferencedAssemblies();
        var allowedPrefixes = new[]
        {
            "Origo.Core.Contracts",
            "System.",
            "Microsoft.",
            "netstandard"
        };
        foreach (var r in refs)
        {
            Assert.True(allowedPrefixes.Any(p => r.Name!.StartsWith(p, StringComparison.Ordinal)),
                $"Unexpected assembly reference: {r.Name}");
        }
    }
}

public class ConsoleBridgeShellApiClassificationGuardTests
{
    [Fact]
    public void ShellApiClassification_CoversEveryConsoleBridgeExport()
    {
        var violations = ShellApiClassificationInventory.FindViolations(typeof(ConsoleBridgeServer).Assembly);
        Assert.Empty(violations);
    }
}
