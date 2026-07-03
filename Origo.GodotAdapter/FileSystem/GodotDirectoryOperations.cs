using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

namespace Origo.GodotAdapter.FileSystem;

internal static class GodotDirectoryOperations
{
    public static bool Exists(string path) => DirAccess.DirExistsAbsolute(path);

    public static void Create(string directoryPath) => DirAccess.MakeDirRecursiveAbsolute(directoryPath);

    public static IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive)
    {
        using var dir = DirAccess.Open(directoryPath) ?? throw new DirectoryNotFoundException($"Cannot open directory: {directoryPath}");
        var normalizedDir = GodotPathResolver.NormalizeDirectoryPath(directoryPath);
        IEnumerable<string> fileNames = dir.GetFiles();

        var suffix = GodotPathResolver.ExtractGlobSuffix(searchPattern);
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
        var normalizedDir = GodotPathResolver.NormalizeDirectoryPath(directoryPath);
        return [.. dir.GetDirectories().Select(d => $"{normalizedDir}/{d}")];
    }

    public static void Rename(string sourcePath, string destinationPath)
    {
        using var dir = DirAccess.Open(GodotPathResolver.GetParentDirectory(sourcePath)) ?? throw new DirectoryNotFoundException(
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

        var normalizedDir = GodotPathResolver.NormalizeDirectoryPath(directoryPath);

        foreach (var file in dir.GetFiles())
            dir.Remove($"{normalizedDir}/{file}");

        foreach (var subdir in dir.GetDirectories())
            DeleteRecursive($"{normalizedDir}/{subdir}");

        var parent = DirAccess.Open(GodotPathResolver.GetParentDirectory(directoryPath));
        if (parent is not null)
            using (parent)
            {
                parent.Remove(directoryPath);
            }
    }
}
