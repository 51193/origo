using System;
using System.IO;
using System.Linq;
using Xunit;

namespace ApiInventoryTool.Tests;

/// <summary>
///     Guards the tracked baseline itself: generated Source Generator members
///     and generated Godot signal types must be present, and kernel
///     implementation assemblies must never leak into the shell surface.
/// </summary>
public class BaselineContentTests
{
    [Fact]
    public void TrackedBaseline_ContainsGeneratedSurfaceAndExcludesKernel()
    {
        var baselinePath = Path.Combine(FindRepoRoot(), "tools", "ApiInventoryTool", "shell-api-baseline.json");
        Assert.True(File.Exists(baselinePath), $"Baseline not found: {baselinePath}");

        var document = InventoryJson.Deserialize(File.ReadAllText(baselinePath));
        Assert.Equal(4, document.Assemblies.Count);
        var contracts = Assert.Single(document.Assemblies, a => a.Name == "Origo.Core.Contracts");
        var adapter = Assert.Single(document.Assemblies, a => a.Name == "Origo.GodotAdapter");
        var bridge = Assert.Single(document.Assemblies, a => a.Name == "Origo.ConsoleBridge");

        // Source Generator output: nullable generated accessor on TypedData.
        Assert.Contains(contracts.Api, line => line.Contains("TryGetString(out string? value)", StringComparison.Ordinal));

        // Godot Source Generator output: nested MethodName/PropertyName/SignalName types.
        Assert.Contains(adapter.Api, line =>
            line.StartsWith("T:public Class Origo.GodotAdapter.Bootstrap.OrigoAutoHost.MethodName", StringComparison.Ordinal));
        Assert.Contains(adapter.Api, line =>
            line.StartsWith("T:public Class Origo.GodotAdapter.Bootstrap.OrigoAutoHost.PropertyName", StringComparison.Ordinal));
        Assert.Contains(adapter.Api, line =>
            line.StartsWith("T:public Class Origo.GodotAdapter.Bootstrap.OrigoAutoHost.SignalName", StringComparison.Ordinal));

        // ConsoleBridge is a shell package: its public server/options surface
        // must stay in the reviewed baseline as well.
        Assert.Contains(bridge.Api, line =>
            line.StartsWith("T:public Class Origo.ConsoleBridge.ConsoleBridgeServer", StringComparison.Ordinal));
        Assert.Contains(bridge.Api, line =>
            line.Contains("ConsoleBridgeServer(Origo.Core.Abstractions.Console.IConsoleInputSource input", StringComparison.Ordinal));
        Assert.Contains(bridge.Api, line =>
            line.StartsWith("P:Origo.ConsoleBridge.ConsoleBridgeOptions.public int Port", StringComparison.Ordinal));

        foreach (var assembly in document.Assemblies)
        {
            Assert.DoesNotContain(assembly.Api, line =>
                line.Contains("Origo.Core.Kernel", StringComparison.Ordinal));
        }
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
