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

    public static int Run(Config config)
    {
        var docsRoot = config.DocsFullPath;
        var errors = new List<string>();
        var fileMetadatas = new Dictionary<string, DocFile>(StringComparer.OrdinalIgnoreCase);

        var docFiles = FindAllDocFiles(docsRoot, config.Languages);

        foreach (var docFile in docFiles)
        {
            var content = File.ReadAllText(docFile.FullPath);
            ParseMetadata(docFile, content, errors);

            ValidateLinks(docFile, content, config.Languages, docsRoot, errors);

            if (!string.IsNullOrEmpty(docFile.PairId))
                fileMetadatas[docFile.PairId] = docFile;
        }

        ValidatePairs(fileMetadatas, config.Languages, errors);

        if (errors.Count > 0)
        {
            Console.Error.WriteLine($"\nValidation FAILED — {errors.Count} error(s):\n");
            foreach (var error in errors)
                Console.Error.WriteLine(error);
            return 1;
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

    private static void ValidateLinks(DocFile docFile, string content, List<string> languages,
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
                continue;

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

    private static void ValidatePairs(Dictionary<string, DocFile> fileMetadatas, List<string> languages,
        List<string> errors)
    {
        var pairs = fileMetadatas.Values
            .GroupBy(f => f.PairId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (pairId, files) in pairs)
        {
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
}
