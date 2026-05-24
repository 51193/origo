using System;
using System.IO;
using Godot;
using FileAccess = Godot.FileAccess;

namespace Origo.GodotAdapter.FileSystem;

internal static class GodotFileOperations
{
    public static bool Exists(string path) => FileAccess.FileExists(path);

    public static string ReadAllText(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file is null)
            throw new FileNotFoundException($"Cannot open file: {path}");
        return file.GetAsText();
    }

    public static void WriteAllText(string path, string content, bool overwrite)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);
        if (!overwrite && FileAccess.FileExists(path))
            throw new IOException($"File already exists and overwrite is disabled: {path}");

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file is null)
            throw new IOException($"Cannot open file for writing: {path}");
        file.StoreString(content);
    }

    public static void Copy(string sourcePath, string destinationPath, bool overwrite)
    {
        var content = ReadAllText(sourcePath);
        WriteAllText(destinationPath, content, overwrite);
    }

    public static void Delete(string path)
    {
        if (FileAccess.FileExists(path))
            DirAccess.RemoveAbsolute(path);
    }
}
