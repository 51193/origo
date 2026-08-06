using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        ValidatePairs(fileMetadatas, config.Languages, errors);
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
            errors.Add($"ERROR: {docFile.RelativePath} — missing revision-bump reminder comment (required after docsync-revision header; see META.zh.md)");
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

    private static void ValidateLinks(DocFile docFile, string content,
        string docsRoot, List<string> errors)
    {
        var cleanContent = StripCodeSpans(content);
        var matches = LinkRegex().Matches(cleanContent);
        var thisLang = docFile.Language;

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var rawTarget = match.Groups[1].Value;
            var displayText = match.Value;

            if (rawTarget.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || rawTarget.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                continue;

            var anchorIdx = rawTarget.IndexOf('#');
            var linkPath = anchorIdx >= 0 ? rawTarget[..anchorIdx] : rawTarget;

            var dir = Path.GetDirectoryName(docFile.FullPath) ?? ".";
            string resolved;
            try
            {
                resolved = Path.GetFullPath(Path.Combine(dir, linkPath));
            }
            catch
            {
                continue;
            }

            if (!resolved.StartsWith(docsRoot, StringComparison.OrdinalIgnoreCase))
            {
                // A language-suffixed doc link that escapes the docs mirror
                // is always broken: the mirror is self-contained, so any
                // .zh.md/.en.md target must live under docs/. Bare .md links
                // may legitimately point at repo-root documents (AGENTS.md).
                var escapedName = Path.GetFileName(resolved);
                if (escapedName.EndsWith(".zh.md", StringComparison.OrdinalIgnoreCase)
                    || escapedName.EndsWith(".en.md", StringComparison.OrdinalIgnoreCase))
                {
                    var lineHint = FindLineNumber(content, match.Index);
                    errors.Add($"ERROR: {docFile.RelativePath}:{lineHint} — language-suffixed link escapes the docs mirror: '{displayText}' resolves outside {docsRoot}");
                }

                continue;
            }

            var relResolved = Path.GetRelativePath(docsRoot, resolved).Replace('\\', '/');

            if (relResolved.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                && !relResolved.EndsWith($".{thisLang}.md", StringComparison.OrdinalIgnoreCase))
            {
                var linkLang = DocFile.ExtractLanguage(Path.GetFileName(relResolved));

                if (linkLang.Length > 0 && linkLang != thisLang)
                {
                    var lineHint = FindLineNumber(content, match.Index);
                    errors.Add($"ERROR: {docFile.RelativePath}:{lineHint} — cross-language link: '{displayText}' targets .{linkLang}.md from a .{thisLang}.md file");
                }
                else if (linkLang.Length == 0)
                {
                    var lineHint = FindLineNumber(content, match.Index);
                    errors.Add($"ERROR: {docFile.RelativePath}:{lineHint} — bare .md link without language suffix: '{displayText}' (should be .{thisLang}.md)");
                }
            }

            if (relResolved.EndsWith($".{thisLang}.md", StringComparison.OrdinalIgnoreCase))
            {
                var targetFull = Path.Combine(docsRoot, relResolved.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(targetFull))
                {
                    var lineHint = FindLineNumber(content, match.Index);
                    errors.Add($"ERROR: {docFile.RelativePath}:{lineHint} — broken link: '{displayText}' (target file not found: {relResolved})");
                }
            }
        }
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
        List<string> errors)
    {
        var pairs = fileMetadatas
            .GroupBy(f => f.PairId)
            .ToDictionary(g => g.Key, g => g.ToList());

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
