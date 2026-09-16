using System;
using System.IO;

namespace DocSyncTool;

internal sealed class DocFile(string fullPath, string relativePath, string language)
{
    public string FullPath { get; } = fullPath;
    public string RelativePath { get; } = relativePath;
    public string Language { get; } = language;
    public string PairId { get; set; } = "";
    public int Revision { get; set; }
    public string ContentHash { get; set; } = "";

    public static string ExtractLanguage(string fileName)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var dotIdx = nameWithoutExt.LastIndexOf('.');
        if (dotIdx < 0)
            return "";

        var suffix = nameWithoutExt[(dotIdx + 1)..];
        return suffix;
    }

    public static string DerivePairId(string relativePath)
    {
        var dir = Path.GetDirectoryName(relativePath) ?? "";
        dir = dir.Replace('\\', '/');
        if (dir == ".")
            dir = "";

        var nameWithoutExt = Path.GetFileNameWithoutExtension(relativePath);
        var dotIdx = nameWithoutExt.LastIndexOf('.');
        if (dotIdx >= 0)
            nameWithoutExt = nameWithoutExt[..dotIdx];

        var pairId = string.IsNullOrEmpty(dir) ? nameWithoutExt : $"{dir}/{nameWithoutExt}";
        return pairId;
    }
}
