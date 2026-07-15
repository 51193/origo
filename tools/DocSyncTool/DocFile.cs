using System;
using System.IO;

namespace DocSyncTool;

internal sealed class DocFile
{
    public string FullPath { get; }
    public string RelativePath { get; }
    public string Language { get; }
    public string PairId { get; set; } = "";
    public int Revision { get; set; }

    public DocFile(string fullPath, string relativePath, string language)
    {
        FullPath = fullPath;
        RelativePath = relativePath;
        Language = language;
    }

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
