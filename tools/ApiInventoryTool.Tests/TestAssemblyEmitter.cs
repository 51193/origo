using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace ApiInventoryTool.Tests;

/// <summary>Emits small real assemblies for inventory tests.</summary>
internal static class TestAssemblyEmitter
{
    public static string Emit(
        string directory,
        string assemblyName,
        string source,
        params string[] referencePaths)
    {
        Directory.CreateDirectory(directory);
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        };
        references.AddRange(referencePaths.Select(path => MetadataReference.CreateFromFile(path)));

        var compilation = CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var path = Path.Combine(directory, assemblyName + ".dll");
        using var stream = File.Create(path);
        var result = compilation.Emit(stream);
        Assert.True(result.Success,
            $"Sample assembly '{assemblyName}' failed to emit:\n" +
            string.Join('\n', result.Diagnostics));
        return path;
    }

    public static string CreateDirectory() =>
        Path.Combine(Path.GetTempPath(), "api-inventory-test-" + Guid.NewGuid().ToString("N"));

    public static void TryDelete(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch
        {
            // Best effort only; a locked metadata reference must not mask the test result.
        }
    }
}
