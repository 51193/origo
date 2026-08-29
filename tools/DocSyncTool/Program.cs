using System;

namespace DocSyncTool;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var repoRoot = FindRepoRoot();
            var config = Config.Load(repoRoot);

            var command = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            return command switch
            {
                "init" => RunInit(config),
                "validate" => Validator.Run(config),
                "generate" => RunGenerate(config),
                _ => PrintUsage()
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FATAL: {ex.Message}");
            return 2;
        }
    }

    private static string FindRepoRoot()
    {
        var dir = Environment.CurrentDirectory;
        while (dir is not null)
        {
            if (System.IO.File.Exists(System.IO.Path.Combine(dir, "AGENTS.md"))
                && System.IO.Directory.Exists(System.IO.Path.Combine(dir, "docs")))
                return dir;

            var parent = System.IO.Path.GetDirectoryName(dir);
            if (parent == dir)
                break;
            dir = parent;
        }

        throw new InvalidOperationException(
            "Cannot find repo root (no AGENTS.md + docs/ found in ancestors). " +
            "Run this tool from within the origo repository.");
    }

    private static int RunInit(Config config)
    {
        Console.WriteLine("DocSyncTool init — migrating existing .md to .zh.md\n");
        Console.WriteLine($"Config languages: [{string.Join(", ", config.Languages)}]");
        Console.WriteLine($"Docs root: {config.DocsFullPath}\n");

        Migrator.Run(config);

        Console.WriteLine("\nRunning generate to create navigation hubs...");
        Generator.Run(config);

        Console.WriteLine("\nRunning validate to check everything...");
        return Validator.Run(config);
    }

    private static int RunGenerate(Config config)
    {
        Console.WriteLine("DocSyncTool generate — creating navigation hubs and status file\n");
        Generator.Run(config);
        Console.WriteLine("\nDone.");
        return 0;
    }

    private static int PrintUsage()
    {
        Console.Error.WriteLine("Usage: dotnet run --project tools/DocSyncTool -- <command>");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Commands:");
        Console.Error.WriteLine("  init       One-time migration: rename .md -> .zh.md, inject metadata, update links");
        Console.Error.WriteLine("  validate   Check pair revisions (auto/manual source state) and links are language-correct");
        Console.Error.WriteLine("  generate   Auto-plan docsync-revision and create README.md hubs + .sync-status.json");
        Console.Error.WriteLine();
        return 1;
    }
}
