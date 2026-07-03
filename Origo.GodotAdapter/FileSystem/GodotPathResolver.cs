using System;
using Origo.Core.Utility;

namespace Origo.GodotAdapter.FileSystem;

internal static class GodotPathResolver
{
    public static string Combine(string basePath, string relativePath) =>
        PathUtility.Combine(basePath, relativePath);

    public static string GetParentDirectory(string path) =>
        PathUtility.GetParentDirectory(path);

    public static string NormalizeDirectoryPath(string path) =>
        PathUtility.NormalizeDirectoryPath(path);

    public static string? ExtractGlobSuffix(string searchPattern) =>
        PathUtility.ExtractGlobSuffix(searchPattern);
}
