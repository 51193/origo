using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DocSyncTool;

internal static partial class Migrator
{
    [GeneratedRegex(@"\]\(([^)]+)\)", RegexOptions.Compiled)]
    private static partial Regex LinkRegex();

    public static void Run(Config config)
    {
        var docsRoot = config.DocsFullPath;
        var renameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        Console.WriteLine("=== Phase 1: Scanning .md files ===");
        var mdFiles = Directory.GetFiles(docsRoot, "*.md", SearchOption.AllDirectories)
            .Select(f => new
            {
                FullPath = f,
                RelativePath = Path.GetRelativePath(docsRoot, f).Replace('\\', '/')
            })
            .ToList();

        foreach (var file in mdFiles)
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(file.FullPath);
            var lastDot = nameWithoutExt.LastIndexOf('.');
            var suffix = lastDot >= 0 ? nameWithoutExt[(lastDot + 1)..] : "";

            if (config.Languages.Contains(suffix))
            {
                Console.WriteLine($"  SKIP (already lang-suffixed): {file.RelativePath}");
                continue;
            }

            var content = File.ReadAllText(file.FullPath);

            if (content.StartsWith("<!-- docsync-pair:", StringComparison.Ordinal))
            {
                Console.WriteLine($"  SKIP (already migrated): {file.RelativePath}");
                continue;
            }

            var newRelativePath = ChangeExtension(file.RelativePath, suffix);
            var newFullPath = Path.Combine(docsRoot, newRelativePath.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(newFullPath))
            {
                Console.WriteLine($"  SKIP (target exists): {file.RelativePath} -> {newRelativePath}");
                continue;
            }

            renameMap[file.FullPath] = newFullPath;
            Console.WriteLine($"  WILL RENAME: {file.RelativePath} -> {newRelativePath}");
        }

        if (renameMap.Count == 0)
        {
            Console.WriteLine("Nothing to migrate.");
            return;
        }

        Console.WriteLine($"\n=== Phase 2: Renaming {renameMap.Count} files ===");
        foreach (var (oldPath, newPath) in renameMap)
        {
            var newDir = Path.GetDirectoryName(newPath);
            if (newDir is not null && !Directory.Exists(newDir))
                Directory.CreateDirectory(newDir);

            File.Move(oldPath, newPath);
            var relOld = Path.GetRelativePath(docsRoot, oldPath).Replace('\\', '/');
            var relNew = Path.GetRelativePath(docsRoot, newPath).Replace('\\', '/');
            Console.WriteLine($"  RENAMED: {relOld} -> {relNew}");
        }

        Console.WriteLine($"\n=== Phase 3: Injecting metadata ===");
        foreach (var newPath in renameMap.Values)
        {
            var relPath = Path.GetRelativePath(docsRoot, newPath).Replace('\\', '/');
            var pairId = DocFile.DerivePairId(relPath);
            var lang = DocFile.ExtractLanguage(Path.GetFileName(newPath));

            var content = File.ReadAllText(newPath);
            var header = $"<!-- docsync-pair: {pairId} -->\n" +
                         $"<!-- docsync-revision: 1 -->\n" +
                         "<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->\n";
            File.WriteAllText(newPath, header + content);
            Console.WriteLine($"  INJECTED: {relPath} (pair={pairId}, lang={lang})");
        }

        Console.WriteLine($"\n=== Phase 4: Updating internal links ===");

        foreach (var newPath in renameMap.Values)
        {
            var relPath = Path.GetRelativePath(docsRoot, newPath).Replace('\\', '/');
            var content = File.ReadAllText(newPath);
            var modified = false;

            content = LinkRegex().Replace(content, match =>
            {
                var fullMatch = match.Value;
                var rawTarget = match.Groups[1].Value;

                if (rawTarget.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    || rawTarget.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    return fullMatch;

                var anchorIdx = rawTarget.IndexOf('#');
                var linkPath = anchorIdx >= 0 ? rawTarget[..anchorIdx] : rawTarget;
                var anchor = anchorIdx >= 0 ? rawTarget[anchorIdx..] : "";

                if (!linkPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                    return fullMatch;

                var fileDir = Path.GetDirectoryName(newPath) ?? ".";
                var targetFull = ResolveRelativePath(fileDir, linkPath);
                var targetRel = Path.GetRelativePath(docsRoot, targetFull).Replace('\\', '/');

                if (targetRel.Contains("..", StringComparison.Ordinal))
                    return fullMatch;

                if (targetRel.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    var linkLang = DocFile.ExtractLanguage(Path.GetFileName(targetRel));
                    if (linkLang.Length == 0)
                    {
                        modified = true;
                        var newLinkTarget = linkPath[..^3] + ".zh.md" + anchor;
                        return fullMatch.Replace(rawTarget, newLinkTarget);
                    }
                }

                return fullMatch;
            });

            if (modified)
            {
                File.WriteAllText(newPath, content);
                Console.WriteLine($"  UPDATED LINKS: {relPath}");
            }
        }

        Console.WriteLine($"\n=== Migration complete. {renameMap.Count} files processed. ===");
        Console.WriteLine("Next: run 'dotnet run -- generate' to create navigation hubs.");
    }

    private static string ChangeExtension(string relativePath, string oldSuffix)
    {
        var dir = Path.GetDirectoryName(relativePath) ?? "";
        dir = dir.Replace('\\', '/');
        var nameWithoutExt = Path.GetFileNameWithoutExtension(relativePath);
        var newName = string.IsNullOrEmpty(oldSuffix)
            ? nameWithoutExt + ".zh.md"
            : relativePath;
        return string.IsNullOrEmpty(dir) ? newName : $"{dir}/{newName}";
    }

    private static string ResolveRelativePath(string baseDir, string linkPath)
    {
        var combined = Path.Combine(baseDir, linkPath);
        return Path.GetFullPath(combined);
    }
}
