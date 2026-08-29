using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace DocSyncTool;

/// <summary>
///     Hash used to detect real documentation-content changes. The DocSync
///     metadata block is deliberately excluded, so auto-rewriting the
///     <c>docsync-revision</c> header (or the reminder comment) is itself a
///     no-op for revision planning and cannot cause CI auto-commit loops.
/// </summary>
internal static class ContentHash
{
    public static string Compute(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(content))));

    public static string Normalize(string content)
    {
        var normalizedNewlines = content.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalizedNewlines.Split('\n');
        var kept = new List<string>(lines.Length);

        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (i < 12 && IsDocSyncMetadataLine(trimmed))
                continue;

            kept.Add(lines[i]);
        }

        return string.Join('\n', kept);
    }

    public static bool IsDocSyncMetadataLine(string trimmedLine)
    {
        return trimmedLine.StartsWith("<!-- docsync-pair:", StringComparison.OrdinalIgnoreCase)
            || trimmedLine.StartsWith("<!-- docsync-revision:", StringComparison.OrdinalIgnoreCase)
            || trimmedLine.StartsWith("<!-- docsync-revision ", StringComparison.OrdinalIgnoreCase);
    }
}
