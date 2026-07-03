using System;
using Origo.Core.Utility;

namespace Origo.GodotAdapter.FileSystem;

internal static class GodotPathResolver
{
    public static string Combine(string basePath, string relativePath) =>
        PathUtility.Combine(basePath, relativePath);

    public static string GetParentDirectory(string path) =>
        PathUtility.GetParentDirectory(path);
}
