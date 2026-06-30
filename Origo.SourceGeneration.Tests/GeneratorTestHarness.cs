using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Origo.SourceGeneration.Tests;

/// <summary>
///     Drives <see cref="TypedDataGenerator" /> over an in-memory compilation and
///     exposes the generated sources, the generator diagnostics, and the diagnostics
///     of the combined (original + generated) compilation.
/// </summary>
internal static class GeneratorTestHarness
{
    private static readonly MetadataReference[] RuntimeReferences = LoadRuntimeReferences();

    private static readonly CSharpParseOptions ParseOptions =
        new(LanguageVersion.Latest);

    private static MetadataReference[] LoadRuntimeReferences()
    {
        // Only the BCL/runtime assemblies are used. Origo.* assemblies are excluded:
        // the generator driver tests model Origo.Core.Snd.Metadata types via in-source
        // scaffolding, so the real Origo.Core (pulled in transitively for the
        // performance benchmark) must not appear in the compilation references — it
        // would collide with the scaffold's same-named types (CS0433).
        var tpa = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        return tpa.Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                        && !Path.GetFileName(p).StartsWith("Origo.", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToArray();
    }

    public static CSharpCompilation CreateCompilation(
        string assemblyName, string source, IEnumerable<MetadataReference>? extraReferences = null)
    {
        var references = RuntimeReferences.Concat(extraReferences ?? Array.Empty<MetadataReference>());
        return CSharpCompilation.Create(
            assemblyName,
            new[] { CSharpSyntaxTree.ParseText(source, ParseOptions) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    }

    public static GeneratorOutput Run(
        string assemblyName, string source, IEnumerable<MetadataReference>? extraReferences = null)
    {
        var compilation = CreateCompilation(assemblyName, source, extraReferences);

        var driver = CSharpGeneratorDriver.Create(
            new[] { new TypedDataGenerator().AsSourceGenerator() },
            parseOptions: ParseOptions);

        var ranDriver = driver.RunGeneratorsAndUpdateCompilation(
            compilation, out var outputCompilation, out var generatorDiagnostics);

        var runResult = ranDriver.GetRunResult();

        var generatedSources = runResult.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => s.SourceText.ToString())
            .ToImmutableArray();

        var compileErrors = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        return new GeneratorOutput(generatedSources, generatorDiagnostics, compileErrors);
    }

    public static GeneratorDriver CreateTrackedDriver()
    {
        return CSharpGeneratorDriver.Create(
            new[] { new TypedDataGenerator().AsSourceGenerator() },
            parseOptions: ParseOptions,
            driverOptions: new GeneratorDriverOptions(
                disabledOutputs: IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));
    }

    public static (GeneratorOutput Output, GeneratorDriver Driver) RunIncremental(
        GeneratorDriver driver, CSharpCompilation compilation)
    {
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation, out var outputCompilation, out var generatorDiagnostics);

        var runResult = driver.GetRunResult();

        var generatedSources = runResult.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => s.SourceText.ToString())
            .ToImmutableArray();

        var compileErrors = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        return (new GeneratorOutput(generatedSources, generatorDiagnostics, compileErrors), driver);
    }
}

internal sealed record GeneratorOutput(
    ImmutableArray<string> GeneratedSources,
    ImmutableArray<Diagnostic> GeneratorDiagnostics,
    ImmutableArray<Diagnostic> CompileErrors)
{
    public string AllGeneratedText => string.Join("\n", GeneratedSources);

    public bool HasGeneratorDiagnostic(string id) =>
        GeneratorDiagnostics.Any(d => d.Id == id);
}
