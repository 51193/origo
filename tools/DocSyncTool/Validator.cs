using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DocSyncTool;

internal static partial class Validator
{
    [GeneratedRegex(@"\]\(([^)]+)\)", RegexOptions.Compiled)]
    private static partial Regex LinkRegex();

    public static int Run(Config config) => RunCore(config, out _);

    /// <summary>
    ///     Runs the full validation and also returns the collected parity
    ///     warnings (testable without redirecting console output; parallel
    ///     xUnit tests must not touch the global <see cref="Console" />).
    /// </summary>
    internal static int RunCore(Config config, out List<string> warnings)
    {
        var docsRoot = config.DocsFullPath;
        var errors = new List<string>();
        warnings = [];
        var fileMetadatas = new List<DocFile>();

        var docFiles = FindAllDocFiles(docsRoot, config.Languages);

        foreach (var docFile in docFiles)
        {
            var content = File.ReadAllText(docFile.FullPath);
            ParseMetadata(docFile, content, errors);

            ValidateLinks(docFile, content, docsRoot, errors);

            if (!string.IsNullOrEmpty(docFile.PairId))
                fileMetadatas.Add(docFile);
        }

        ValidatePairs(fileMetadatas, config.Languages, docsRoot, errors);
        ValidateSourceMirror(config, errors);
        warnings = ComputeHeadingParityWarnings(fileMetadatas, config.Languages);

        if (errors.Count > 0)
        {
            Console.Error.WriteLine($"\nValidation FAILED — {errors.Count} error(s):\n");
            foreach (var error in errors)
                Console.Error.WriteLine(error);
            return 1;
        }

        if (warnings.Count > 0)
        {
            Console.WriteLine($"\nValidation passed with {warnings.Count} warning(s) (heading parity hints — verify translations are at content parity):\n");
            foreach (var warning in warnings)
                Console.WriteLine(warning);
        }

        Console.WriteLine($"Validation PASSED — {docFiles.Count} file(s) checked.");
        return 0;
    }

    private static List<DocFile> FindAllDocFiles(string docsRoot, List<string> languages)
    {
        var results = new List<DocFile>();
        var patterns = languages.Select(l => $"*.{l}.md").ToArray();

        foreach (var pattern in patterns)
        {
            var files = Directory.GetFiles(docsRoot, pattern, SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var relPath = Path.GetRelativePath(docsRoot, file).Replace('\\', '/');
                var lang = DocFile.ExtractLanguage(Path.GetFileName(file));
                results.Add(new DocFile(file, relPath, lang));
            }
        }

        return results;
    }

    private static void ParseMetadata(DocFile docFile, string content, List<string> errors)
    {
        var lines = content.Split('\n');

        foreach (var line in lines.Take(5))
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("<!-- docsync-pair:", StringComparison.OrdinalIgnoreCase))
            {
                var pair = ExtractCommentValue(trimmed);
                if (string.IsNullOrWhiteSpace(pair))
                    errors.Add($"ERROR: {docFile.RelativePath} — docsync-pair value is empty");
                else
                    docFile.PairId = pair.Trim();
            }

            if (trimmed.StartsWith("<!-- docsync-revision:", StringComparison.OrdinalIgnoreCase))
            {
                var rev = ExtractCommentValue(trimmed);
                if (!int.TryParse(rev?.Trim(), out var revision) || revision < 1)
                    errors.Add($"ERROR: {docFile.RelativePath} — docsync-revision must be a positive integer, got '{rev}'");
                else
                    docFile.Revision = revision;
            }
        }

        if (string.IsNullOrEmpty(docFile.PairId))
            errors.Add($"ERROR: {docFile.RelativePath} — missing '<!-- docsync-pair: ... -->' header");

        if (docFile.Revision == 0)
            errors.Add($"ERROR: {docFile.RelativePath} — missing '<!-- docsync-revision: N -->' header");

        var hasReminder = false;
        foreach (var line in lines.Take(10))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("<!-- docsync-revision ", StringComparison.OrdinalIgnoreCase)
                && !trimmed.StartsWith("<!-- docsync-revision:", StringComparison.OrdinalIgnoreCase))
            {
                hasReminder = true;
                break;
            }
        }

        if (!hasReminder)
            errors.Add($"ERROR: {docFile.RelativePath} — missing managed-revision reminder comment (required after docsync-revision header; see META.zh.md)");
    }

    private static string ExtractCommentValue(string commentLine)
    {
        var start = commentLine.IndexOf(':', StringComparison.Ordinal);
        if (start < 0)
            return "";

        var end = commentLine.LastIndexOf("-->", StringComparison.Ordinal);
        if (end < 0)
            return "";

        return commentLine[(start + 1)..end].Trim();
    }

    [GeneratedRegex(@"\x60{3}[\s\S]*?\x60{3}|\x60{2}[^\n]*?\x60{2}|\x60[^\x60\r\n]+\x60", RegexOptions.Compiled)]
    private static partial Regex CodeSpanRegex();

    [GeneratedRegex(@"^\[[^\]]+\]:\s*(.+?)\s*$", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex ReferenceDefinitionRegex();

    [GeneratedRegex(@"\[[^\]]+\]\[([^\]]+)\]", RegexOptions.Compiled)]
    private static partial Regex FullReferenceLinkRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\[\]", RegexOptions.Compiled)]
    private static partial Regex CollapsedReferenceLinkRegex();

    [GeneratedRegex(@"^(#{1,6})\s+(.+?)\s*$", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex HeadingRegex();

    private static void ValidateLinks(DocFile docFile, string content,
        string docsRoot, List<string> errors)
    {
        var cleanContent = StripCodeSpans(content);

        foreach (Match match in LinkRegex().Matches(cleanContent))
        {
            var rawTarget = match.Groups[1].Value;
            ValidateLinkTarget(docFile, content, rawTarget, match.Value, match.Index,
                docsRoot, errors);
        }

        ValidateReferenceLinks(docFile, content, cleanContent, docsRoot, errors);
    }

    private static void ValidateReferenceLinks(DocFile docFile, string content,
        string cleanContent, string docsRoot, List<string> errors)
    {
        var definitions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in ReferenceDefinitionRegex().Matches(cleanContent))
        {
            var label = match.Groups[1].Value.Trim();
            var target = match.Groups[2].Value.Trim();
            if (!definitions.TryAdd(label, target))
            {
                var lineHint = FindLineNumber(content, match.Index);
                errors.Add($"ERROR: {docFile.RelativePath}:{lineHint} — duplicate reference link definition '{label}'");
                continue;
            }

            ValidateLinkTarget(docFile, content, target,
                $"[{label}]: {target}", match.Index, docsRoot, errors);
        }

        foreach (Match match in FullReferenceLinkRegex().Matches(cleanContent))
        {
            var label = match.Groups[1].Value.Trim();
            if (label.Length == 0 || !definitions.ContainsKey(label))
            {
                var lineHint = FindLineNumber(content, match.Index);
                errors.Add($"ERROR: {docFile.RelativePath}:{lineHint} — reference link '{match.Value}' has no matching definition");
            }
        }

        foreach (Match match in CollapsedReferenceLinkRegex().Matches(cleanContent))
        {
            var label = match.Groups[1].Value.Trim();
            if (label.Length == 0 || !definitions.ContainsKey(label))
            {
                var lineHint = FindLineNumber(content, match.Index);
                errors.Add($"ERROR: {docFile.RelativePath}:{lineHint} — collapsed reference link '{match.Value}' has no matching definition");
            }
        }
    }

    private static void ValidateLinkTarget(
        DocFile docFile,
        string content,
        string rawTarget,
        string displayText,
        int charIndex,
        string docsRoot,
        List<string> errors)
    {
        if (rawTarget.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || rawTarget.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return;

        var anchorIdx = rawTarget.IndexOf('#');
        var linkPath = anchorIdx >= 0 ? rawTarget[..anchorIdx] : rawTarget;
        var anchor = anchorIdx >= 0 ? rawTarget[(anchorIdx + 1)..] : null;

        if (linkPath.Length == 0)
        {
            if (!string.IsNullOrEmpty(anchor))
                ValidateAnchor(docFile, content, docFile.FullPath, anchor, charIndex, errors);
            return;
        }

        var dir = Path.GetDirectoryName(docFile.FullPath) ?? ".";
        string resolved;
        try
        {
            resolved = Path.GetFullPath(Path.Combine(dir, linkPath));
        }
        catch
        {
            return;
        }

        var relativeToRoot = Path.GetRelativePath(docsRoot, resolved);
        if (relativeToRoot.StartsWith("..", StringComparison.Ordinal))
        {
            var escapedName = Path.GetFileName(resolved);
            if (escapedName.EndsWith(".zh.md", StringComparison.OrdinalIgnoreCase)
                || escapedName.EndsWith(".en.md", StringComparison.OrdinalIgnoreCase))
            {
                var lineHint = FindLineNumber(content, charIndex);
                errors.Add($"ERROR: {docFile.RelativePath}:{lineHint} — language-suffixed link escapes the docs mirror: '{displayText}' resolves outside {docsRoot}");
            }

            return;
        }

        var relResolved = Path.GetRelativePath(docsRoot, resolved).Replace('\\', '/');

        if (relResolved.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            && !relResolved.EndsWith($".{docFile.Language}.md", StringComparison.OrdinalIgnoreCase))
        {
            var linkLang = DocFile.ExtractLanguage(Path.GetFileName(relResolved));
            if (linkLang.Length > 0 && linkLang != docFile.Language)
            {
                var lineHint = FindLineNumber(content, charIndex);
                errors.Add($"ERROR: {docFile.RelativePath}:{lineHint} — cross-language link: '{displayText}' targets .{linkLang}.md from a .{docFile.Language}.md file");
            }
            else if (linkLang.Length == 0)
            {
                var lineHint = FindLineNumber(content, charIndex);
                errors.Add($"ERROR: {docFile.RelativePath}:{lineHint} — bare .md link without language suffix: '{displayText}' (should be .{docFile.Language}.md)");
            }
        }

        if (relResolved.EndsWith($".{docFile.Language}.md", StringComparison.OrdinalIgnoreCase))
        {
            var targetFull = Path.Combine(docsRoot, relResolved.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(targetFull))
            {
                var lineHint = FindLineNumber(content, charIndex);
                errors.Add($"ERROR: {docFile.RelativePath}:{lineHint} — broken link: '{displayText}' (target file not found: {relResolved})");
                return;
            }

            if (!string.IsNullOrEmpty(anchor))
                ValidateAnchor(docFile, content, targetFull, anchor, charIndex, errors);

            return;
        }

        // Directory targets (e.g. generated hub entries) must exist too.
        var targetDir = Path.Combine(docsRoot, relResolved.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(targetDir))
        {
            var lineHint = FindLineNumber(content, charIndex);
            errors.Add($"ERROR: {docFile.RelativePath}:{lineHint} — broken directory link: '{displayText}' (target not found: {relResolved})");
        }
    }

    private static void ValidateAnchor(
        DocFile docFile,
        string content,
        string targetFull,
        string anchor,
        int charIndex,
        List<string> errors)
    {
        var targetContent = string.Equals(targetFull, docFile.FullPath, StringComparison.Ordinal)
            ? content
            : File.ReadAllText(targetFull);
        var cleanTarget = StripCodeSpans(targetContent);
        var headings = HeadingRegex().Matches(cleanTarget)
            .Select(m => SlugifyHeading(m.Groups[2].Value))
            .ToHashSet(StringComparer.Ordinal);

        if (headings.Contains(anchor))
            return;

        var lineHint = FindLineNumber(content, charIndex);
        errors.Add($"ERROR: {docFile.RelativePath}:{lineHint} — broken anchor link: '#{anchor}' was not found in {Path.GetRelativePath(docFile.FullPath, targetFull)}");
    }

    [GeneratedRegex(@"[^\p{L}\p{N}\s_-]", RegexOptions.Compiled)]
    private static partial Regex SlugRemoveRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex SlugWhitespaceRegex();

    private static string SlugifyHeading(string heading)
    {
        var text = SlugRemoveRegex().Replace(heading, "");
        text = SlugWhitespaceRegex().Replace(text.Trim(), "-").ToLowerInvariant();
        return text;
    }

    private static int FindLineNumber(string content, int charIndex)
    {
        var line = 1;
        for (var i = 0; i < charIndex && i < content.Length; i++)
        {
            if (content[i] == '\n')
                line++;
        }
        return line;
    }

    private static string StripCodeSpans(string content)
    {
        return CodeSpanRegex().Replace(content, match =>
        {
            var span = match.Value;
            var lines = span.Split('\n');
            var replacement = new string[lines.Length];
            for (var j = 0; j < lines.Length; j++)
            {
                if (j == lines.Length - 1)
                    replacement[j] = new string(' ', lines[j].Length);
                else
                    replacement[j] = "";
            }
            return string.Join('\n', replacement);
        });
    }

    private static void ValidatePairs(List<DocFile> fileMetadatas, List<string> languages,
        string docsRoot, List<string> errors)
    {
        var pairs = fileMetadatas
            .GroupBy(f => f.PairId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var previousRevisions = LoadPreviousRevisions(docsRoot);

        foreach (var (pairId, files) in pairs)
        {
            foreach (var file in files)
            {
                var derived = DocFile.DerivePairId(file.RelativePath);
                if (!string.Equals(pairId, derived, StringComparison.Ordinal))
                    errors.Add(
                        $"ERROR: pair '{pairId}' — file '{file.RelativePath}' declares a docsync-pair header " +
                        $"that does not match its path (expected '{derived}'). " +
                        "Fix the header or move the file so both match.");
            }

            if (files.Count != languages.Count)
            {
                var presentLangs = files.Select(f => f.Language).OrderBy(l => l).ToList();
                var missingLangs = languages.Except(presentLangs).OrderBy(l => l).ToList();
                foreach (var missing in missingLangs)
                    errors.Add($"ERROR: pair '{pairId}' — missing translation for language '{missing}'");
            }

            var revisions = files.Select(f => f.Revision).Distinct().ToList();
            if (revisions.Count > 1)
            {
                var detail = string.Join(", ", files.Select(f => $"{f.Language}={f.Revision}"));
                errors.Add($"ERROR: pair '{pairId}' — revision mismatch ({detail})");
            }

            if (previousRevisions.TryGetValue(pairId, out var previous))
            {
                foreach (var file in files)
                {
                    if (previous.TryGetValue(file.Language, out var previousRevision)
                        && file.Revision < previousRevision)
                        errors.Add(
                            $"ERROR: pair '{pairId}' — revision for {file.Language} moved backwards " +
                            $"({previousRevision} -> {file.Revision}); docsync-revision must be monotonic");
                }
            }
        }
    }

    private static Dictionary<string, Dictionary<string, int>> LoadPreviousRevisions(string docsRoot)
    {
        var statusPath = Path.Combine(docsRoot, ".sync-status.json");
        if (!File.Exists(statusPath))
            return [];

        try
        {
            var status = JsonSerializer.Deserialize<StatusSnapshot>(File.ReadAllText(statusPath));
            if (status?.Pairs is null)
                return [];

            return status.Pairs.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.PreviousRevisions ?? [],
                StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed class StatusSnapshot
    {
        [JsonPropertyName("pairs")]
        public Dictionary<string, StatusPairSnapshot>? Pairs { get; set; }
    }

    private sealed class StatusPairSnapshot
    {
        [JsonPropertyName("previous_revisions")]
        public Dictionary<string, int>? PreviousRevisions { get; set; }
    }


    private static void ValidateSourceMirror(Config config, List<string> errors)
    {
        foreach (var sourceRoot in config.SourceMirrorRoots)
        {
            var rootFullPath = Path.GetFullPath(Path.Combine(config.RepoRoot, sourceRoot));
            if (!Directory.Exists(rootFullPath))
            {
                errors.Add($"ERROR: source mirror root does not exist: {sourceRoot}");
                continue;
            }

            var sourceDirectories = Directory.GetDirectories(rootFullPath, "*", SearchOption.AllDirectories)
                .Prepend(rootFullPath)
                .Where(dir => Directory.GetFiles(dir, "*.cs", SearchOption.TopDirectoryOnly).Length > 0)
                .Select(dir => Path.GetRelativePath(config.RepoRoot, dir).Replace('\\', '/'))
                .Where(dir => !dir.StartsWith("Origo.", StringComparison.Ordinal)
                              || (!dir.Contains("/obj/", StringComparison.Ordinal)
                                  && !dir.Contains("/bin/", StringComparison.Ordinal)
                                  && !dir.Contains("/.godot/", StringComparison.Ordinal)))
                .OrderBy(dir => dir, StringComparer.Ordinal)
                .ToList();

            foreach (var sourceDir in sourceDirectories)
            {
                var overrideKey = sourceDir.Replace('\\', '/');
                var docDirRel = config.SourceDocOverrides.TryGetValue(overrideKey, out var overrideValue)
                    ? overrideValue.TrimStart('/')
                    : $"docs/{sourceDir}";
                var docDir = Path.GetFullPath(Path.Combine(config.RepoRoot, docDirRel));
                if (!Directory.Exists(docDir))
                {
                    errors.Add($"ERROR: source directory '{sourceDir}' has no documentation directory '{docDirRel}'");
                    continue;
                }

                var sourceFiles = Directory.GetFiles(
                        Path.Combine(config.RepoRoot, sourceDir), "*.cs", SearchOption.TopDirectoryOnly)
                    .Select(file => Path.GetFileName(file))
                    .OrderBy(file => file, StringComparer.Ordinal)
                    .ToList();

                foreach (var language in config.Languages)
                {
                    var docFile = Path.Combine(docDir, $"README.{language}.md");
                    if (!File.Exists(docFile))
                    {
                        errors.Add($"ERROR: source directory '{sourceDir}' is missing '{Path.GetRelativePath(config.RepoRoot, docFile).Replace('\\', '/')}'");
                        continue;
                    }

                    var content = File.ReadAllText(docFile);
                    foreach (var sourceFile in sourceFiles)
                    {
                        var basename = Path.GetFileNameWithoutExtension(sourceFile);
                        var listed = content.Contains($"`{sourceFile}`", StringComparison.Ordinal)
                            || content.Contains($"`{Path.DirectorySeparatorChar}{sourceFile}`", StringComparison.Ordinal)
                            || content.Contains($"`/{sourceFile}`", StringComparison.Ordinal)
                            || content.Contains($"`\\{sourceFile}`", StringComparison.Ordinal)
                            || content.Contains($"/{sourceFile}`", StringComparison.Ordinal)
                            || content.Contains($"\\{sourceFile}`", StringComparison.Ordinal)
                            || (sourceFile != basename
                                && content.Contains($"`{basename}`", StringComparison.Ordinal));
                        if (!listed)
                            errors.Add($"ERROR: {Path.GetRelativePath(config.RepoRoot, docFile).Replace('\\', '/')} does not list source file `{sourceFile}` from '{sourceDir}'");
                    }
                }
            }
        }
    }

    /// <summary>
    ///     Computes heading-structure parity warnings for a bilingual pair
    ///     (## / ### counts). Equal docsync revisions only prove the metadata
    ///     was bumped, not that the translations stayed at content parity; a
    ///     section count mismatch is a hint to verify the translation is
    ///     complete. Warning-only: English may legitimately consolidate
    ///     sections, and reworded titles never match literally.
    /// </summary>
    internal static List<string> ComputeHeadingParityWarnings(List<DocFile> fileMetadatas, List<string> languages)
    {
        var warnings = new List<string>();
        var pairs = fileMetadatas
            .GroupBy(f => f.PairId)
            .Where(g => g.Count() == languages.Count);

        foreach (var group in pairs)
        {
            var byLang = group.ToDictionary(f => f.Language, f => f.FullPath);
            var counts = byLang.ToDictionary(
                kvp => kvp.Key,
                kvp =>
                {
                    var content = StripCodeSpans(File.ReadAllText(kvp.Value));
                    var h2 = H2HeadingRegex().Count(content);
                    var h3 = H3HeadingRegex().Count(content);
                    return (H2: h2, H3: h3);
                });

            var (firstH2, firstH3) = counts.Values.First();
            foreach (var (lang, c) in counts)
            {
                if (c.H2 != firstH2 || c.H3 != firstH3)
                {
                    var detail = string.Join(", ", counts.Select(kvp => $"{kvp.Key} {kvp.Value.H2}x## {kvp.Value.H3}x###"));
                    warnings.Add(
                        $"WARNING: pair '{group.Key}' — heading structure differs between languages ({detail}); " +
                        "verify the translation is at content parity");
                    break;
                }
            }
        }

        return warnings;
    }

    [GeneratedRegex(@"(?m)^##\s", RegexOptions.Compiled)]
    private static partial Regex H2HeadingRegex();

    [GeneratedRegex(@"(?m)^###\s", RegexOptions.Compiled)]
    private static partial Regex H3HeadingRegex();
}
