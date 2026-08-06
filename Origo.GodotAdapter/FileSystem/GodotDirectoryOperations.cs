using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Origo.Core.Utility;

namespace Origo.GodotAdapter.FileSystem;

/// <summary>
///     Static helper wrapping Godot <c>DirAccess</c> for directory
///     existence checks, creation, enumeration, and deletion.
/// </summary>
internal static class GodotDirectoryOperations
{
    public static bool Exists(string path) => DirAccess.DirExistsAbsolute(path);

    public static void Create(string directoryPath)
    {
        var err = DirAccess.MakeDirRecursiveAbsolute(directoryPath);
        if (err != Error.Ok)
            throw new IOException($"Failed to create directory '{directoryPath}': {err}");
    }

    public static IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive)
    {
        using var dir = DirAccess.Open(directoryPath) ?? throw new DirectoryNotFoundException($"Cannot open directory: {directoryPath}");
        dir.IncludeHidden = true;
        var normalizedDir = PathUtility.NormalizeDirectoryPath(directoryPath);
        IEnumerable<string> fileNames = dir.GetFiles();

        var suffix = PathUtility.ExtractGlobSuffix(searchPattern);
        if (suffix is not null)
            fileNames = fileNames.Where(f => f.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

        var result = fileNames.Select(f => $"{normalizedDir}/{f}").ToList();

        if (recursive)
            foreach (var subdir in dir.GetDirectories())
                result.AddRange(EnumerateFiles($"{normalizedDir}/{subdir}", searchPattern, true));

        return result;
    }

    public static IEnumerable<string> EnumerateDirectories(string directoryPath)
    {
        using var dir = DirAccess.Open(directoryPath) ?? throw new DirectoryNotFoundException($"Cannot open directory: {directoryPath}");
        dir.IncludeHidden = true;
        var normalizedDir = PathUtility.NormalizeDirectoryPath(directoryPath);
        return [.. dir.GetDirectories().Select(d => $"{normalizedDir}/{d}")];
    }

    public static void Rename(string sourcePath, string destinationPath)
    {
        using var dir = DirAccess.Open(PathUtility.GetParentDirectory(sourcePath)) ?? throw new DirectoryNotFoundException(
                $"Cannot open parent directory for rename: {sourcePath}");
        var err = dir.Rename(sourcePath, destinationPath);
        if (err != Error.Ok)
            throw new IOException(
                $"Failed to rename '{sourcePath}' to '{destinationPath}': {err}");
    }

    /// <summary>
    ///     Recursively deletes all files and subdirectory contents under the
    ///     given directory, including hidden (dot-prefixed) files such as the
    ///     save write-in-progress marker. Afterwards it attempts to remove the
    ///     directory container itself through its parent handle; when the
    ///     engine (e.g. the editor) holds an open handle on the directory that
    ///     makes the OS reject removal, the empty container is left behind,
    ///     which is harmless.
    /// </summary>
    public static void DeleteRecursive(string directoryPath)
    {
        if (!DirAccess.DirExistsAbsolute(directoryPath))
            return;

        var normalizedDir = PathUtility.NormalizeDirectoryPath(directoryPath);

        using (var dir = DirAccess.Open(directoryPath) ?? throw new InvalidOperationException(
            $"Failed to open directory for deletion: {directoryPath}"))
        {
            dir.IncludeHidden = true;

            foreach (var file in dir.GetFiles())
            {
                var fileErr = dir.Remove($"{normalizedDir}/{file}");
                if (fileErr != Error.Ok)
                    throw new IOException($"Failed to delete file '{file}' in '{directoryPath}': {fileErr}");
            }

            foreach (var subdir in dir.GetDirectories())
                DeleteRecursive($"{normalizedDir}/{subdir}");
        }

        // Best-effort container removal through the parent handle (the same
        // mechanism the integration-test runner uses; DirAccess.RemoveAbsolute
        // is unreliable for user:// paths). The engine can hold an open handle
        // on the directory, so the OS may reject removal even when the
        // container is empty — in that case the empty container is left behind.
        var slash = normalizedDir.LastIndexOf('/');
        if (slash < 0)
            return;
        var leaf = normalizedDir[(slash + 1)..];
        using var parentDir = DirAccess.Open(normalizedDir[..^leaf.Length]);
        if (parentDir is not null)
            _ = parentDir.Remove(leaf);
    }
}
