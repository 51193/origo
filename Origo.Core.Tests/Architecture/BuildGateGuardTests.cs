using System;
using System.IO;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Pins the build configuration that makes the dead-code analyzers real.
/// </summary>
/// <remarks>
///     The build is the gate: <c>EnforceCodeStyleInBuild</c> makes the IDE analyzers
///     run during the build, <c>IDE0051</c> and <c>IDE0052</c> are errors, and
///     warnings are errors, so an unused or unread private member fails
///     <c>dotnet build</c>, the <c>scripts/format.sh</c> analyzer pass, and CI.
///     Removing any of those settings would reopen the hole silently.
/// </remarks>
public class BuildGateGuardTests
{
    /// <summary>Verifies that the build enables the code-style analyzers and rejects warnings.</summary>
    [Fact]
    public void BuildProperties_EnableCodeStyleAnalyzers_AndTreatWarningsAsErrors()
    {
        var properties = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Directory.Build.props"));

        Assert.Contains("<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>", properties, StringComparison.Ordinal);
        Assert.Contains("<TreatWarningsAsErrors>true</TreatWarningsAsErrors>", properties, StringComparison.Ordinal);
    }

    /// <summary>Verifies that the unused-member analyzers are errors in the editor configuration.</summary>
    [Fact]
    public void EditorConfig_MakesDeadCodeAnalyzersErrors()
    {
        var editorConfig = File.ReadAllText(Path.Combine(FindRepositoryRoot(), ".editorconfig"));

        Assert.Contains("dotnet_diagnostic.IDE0051.severity = error", editorConfig, StringComparison.Ordinal);
        Assert.Contains("dotnet_diagnostic.IDE0052.severity = error", editorConfig, StringComparison.Ordinal);
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

        throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
    }
}
