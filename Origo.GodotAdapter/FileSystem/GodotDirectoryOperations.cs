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

    public static void DeleteRecursive(string directoryPath)
    {
        if (!DirAccess.DirExistsAbsolute(directoryPath))
            return;

        using var dir = DirAccess.Open(directoryPath) ?? throw new InvalidOperationException(
            $"Failed to open directory for deletion: {directoryPath}");

        var normalizedDir = PathUtility.NormalizeDirectoryPath(directoryPath);

        foreach (var file in dir.GetFiles())
        {
            var fileErr = dir.Remove($"{normalizedDir}/{file}");
            if (fileErr != Error.Ok)
                throw new IOException($"Failed to delete file '{file}' in '{directoryPath}': {fileErr}");
        }

        foreach (var subdir in dir.GetDirectories())
            DeleteRecursive($"{normalizedDir}/{subdir}");

        // Godot's DirAccess.Remove/RemoveAbsolute is unreliable for user://
        // directory removal. Resolve to real OS path and use RemoveAbsolute
        // with the globalized path so the engine does not need to translate
        // the virtual prefix during the remove call.
        if (directoryPath.StartsWith("user://", StringComparison.Ordinal))
        {
            var realPath = ProjectSettings.GlobalizePath(directoryPath);
            var realErr = DirAccess.RemoveAbsolute(realPath);
            if (realErr != Error.Ok)
                throw new IOException(
                    $"Failed to remove user:// directory '{directoryPath}' " +
                    $"(resolved to '{realPath}'): {realErr}");
            return;
        }

        var parent = DirAccess.Open(PathUtility.GetParentDirectory(directoryPath));
        if (parent is not null)
            using (parent)
            {
                var parentErr = parent.Remove(System.IO.Path.GetFileName(directoryPath.TrimEnd('/')));
                if (parentErr != Error.Ok)
                    throw new IOException($"Failed to remove directory '{directoryPath}' via parent: {parentErr}");
            }
        else
        {
            var absErr = DirAccess.RemoveAbsolute(directoryPath);
            if (absErr != Error.Ok)
                throw new IOException($"Failed to remove directory '{directoryPath}': {absErr}");
        }
    }
}
