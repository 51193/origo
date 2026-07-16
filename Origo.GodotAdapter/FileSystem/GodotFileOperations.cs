using System;
using System.IO;
using Godot;
using FileAccess = Godot.FileAccess;

namespace Origo.GodotAdapter.FileSystem;

/// <summary>
///     Static helper wrapping Godot <c>FileAccess</c> for file
///     existence checks, read, write, copy, delete, and rename.
/// </summary>
internal static class GodotFileOperations
{
    public static bool Exists(string path) => FileAccess.FileExists(path);

    public static string ReadAllText(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read) ?? throw new FileNotFoundException($"Cannot open file: {path}");
        return file.GetAsText();
    }

    public static void WriteAllText(string path, string content, bool overwrite)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);
        if (!overwrite && FileAccess.FileExists(path))
            throw new IOException($"File already exists and overwrite is disabled: {path}");

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write) ?? throw new IOException($"Cannot open file for writing: {path}");
        file.StoreString(content);
    }

    public static void Copy(string sourcePath, string destinationPath, bool overwrite)
    {
        var content = ReadAllText(sourcePath);
        WriteAllText(destinationPath, content, overwrite);
    }

    public static void Delete(string path)
    {
        if (!FileAccess.FileExists(path))
            return;

        var err = DirAccess.RemoveAbsolute(path);
        if (err != Error.Ok)
            throw new IOException($"Failed to delete file '{path}': {err}");
    }
}
