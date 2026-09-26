using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ApiInventoryTool;

/// <summary>Command-line entry point for the shell API inventory.</summary>
internal static class Program
{
    private const string _defaultBaselineRelativePath = "tools/ApiInventoryTool/shell-api-baseline.json";

    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FATAL: {ex.Message}");
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        var command = args[0];
        var options = ParseOptions([.. args.Skip(1)]);
        return command switch
        {
            "generate" => Generate(options),
            "verify" => Verify(options),
            "compare" => Compare(options),
            _ => Fail($"unknown command '{command}'. Run 'ApiInventoryTool --help'."),
        };
    }

    private static int Generate(CommandOptions options)
    {
        var result = Build(options);
        if (!result.Succeeded)
            return FailWithErrors("API inventory generation failed.", result.Errors);

        var output = options.OutputPath ?? Path.Combine(FindRepoRoot(), _defaultBaselineRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, InventoryJson.Serialize(result.Document!));
        Console.WriteLine($"API inventory written: {output}");
        return 0;
    }

    private static int Verify(CommandOptions options)
    {
        var result = Build(options);
        if (!result.Succeeded)
            return FailWithErrors("API inventory build failed.", result.Errors);

        var baselinePath = options.BaselinePath ?? Path.Combine(FindRepoRoot(), _defaultBaselineRelativePath);
        if (!File.Exists(baselinePath))
            return Fail($"baseline not found: '{baselinePath}'. Generate and review the baseline in this change.");

        var baseline = InventoryJson.Deserialize(File.ReadAllText(baselinePath));
        var changes = InventoryComparer.Verify(baseline, result.Document!);
        if (changes.Count == 0)
        {
            Console.WriteLine($"Shell API baseline: OK ({baseline.Assemblies.Count} assemblies).");
            return 0;
        }

        Console.Error.WriteLine("Shell API baseline mismatch. Update the baseline only for reviewed API changes.");
        Console.Error.WriteLine($"Baseline: {baselinePath}");
        Console.Error.WriteLine("Run 'scripts/api-inventory.sh generate' and review the JSON diff in the same change.");
        foreach (var change in changes.Take(100))
            Console.Error.WriteLine($"  {change}");
        if (changes.Count > 100)
            Console.Error.WriteLine($"  ... and {changes.Count - 100} more.");
        return 1;
    }

    private static int Compare(CommandOptions options)
    {
        if (options.PreviousBaselinePath is null || options.OutputPath is null)
            return Fail("compare requires --previous <path> and --current <path>.");

        var previous = InventoryJson.Deserialize(File.ReadAllText(options.PreviousBaselinePath));
        var current = InventoryJson.Deserialize(File.ReadAllText(options.OutputPath));
        var failures = InventoryComparer.ComparePrevious(previous, current);
        if (failures.Count == 0)
        {
            Console.WriteLine("Previous-package shell API validation: OK.");
            return 0;
        }

        Console.Error.WriteLine("Previous-package shell API validation failed (0.1.x source compatibility).");
        foreach (var failure in failures.Take(100))
            Console.Error.WriteLine($"  {failure}");
        if (failures.Count > 100)
            Console.Error.WriteLine($"  ... and {failures.Count - 100} more.");
        return 1;
    }

    private static InventoryBuildResult Build(CommandOptions options)
    {
        if (options.Assemblies.Count == 0)
            throw new InvalidOperationException("at least one --assembly <name>=<path> argument is required.");

        return InventoryBuilder.Build(options.Assemblies, options.ReferenceDirectories, options.ForbiddenAssemblies);
    }

    private static CommandOptions ParseOptions(string[] args)
    {
        var options = new CommandOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var argument = args[i];
            switch (argument)
            {
                case "--assembly":
                    options.Assemblies.Add(ParseAssembly(args, ref i));
                    break;
                case "--reference-dir":
                    options.ReferenceDirectories.Add(RequireValue(args, ref i, argument));
                    break;
                case "--forbid-assembly":
                    options.ForbiddenAssemblies.Add(RequireValue(args, ref i, argument));
                    break;
                case "--baseline":
                    options.BaselinePath = RequireValue(args, ref i, argument);
                    break;
                case "--output":
                    options.OutputPath = RequireValue(args, ref i, argument);
                    break;
                case "--previous":
                    options.PreviousBaselinePath = RequireValue(args, ref i, argument);
                    break;
                case "--current":
                    options.OutputPath = RequireValue(args, ref i, argument);
                    break;
                default:
                    throw new InvalidOperationException($"unknown option '{argument}'.");
            }
        }

        return options;
    }

    private static AssemblyInput ParseAssembly(string[] args, ref int index)
    {
        var value = RequireValue(args, ref index, "--assembly");
        var separator = value.IndexOf('=');
        if (separator <= 0 || separator == value.Length - 1)
            throw new InvalidOperationException($"--assembly expects '<name>=<path>', got '{value}'.");

        return new AssemblyInput(value[..separator], value[(separator + 1)..]);
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
            throw new InvalidOperationException($"{option} requires a value.");

        index++;
        return args[index];
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

        return Directory.GetCurrentDirectory();
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine($"FATAL: {message}");
        return 1;
    }

    private static int FailWithErrors(string prefix, IReadOnlyList<string> errors)
    {
        Console.Error.WriteLine(prefix);
        foreach (var error in errors)
            Console.Error.WriteLine($"  {error}");
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  ApiInventoryTool generate --assembly <name>=<path> [--reference-dir <dir>]... [--output <file>]");
        Console.WriteLine("  ApiInventoryTool verify   --assembly <name>=<path> [--reference-dir <dir>]... [--baseline <file>]");
        Console.WriteLine("  ApiInventoryTool compare  --previous <file> --current <file>");
    }

    private sealed class CommandOptions
    {
        public List<AssemblyInput> Assemblies { get; } = [];
        public List<string> ReferenceDirectories { get; } = [];
        public List<string> ForbiddenAssemblies { get; } = ["Origo.Core.Kernel"];
        public string? BaselinePath { get; set; }
        public string? OutputPath { get; set; }
        public string? PreviousBaselinePath { get; set; }
    }
}
