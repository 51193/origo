namespace Origo.Core.Abstractions.FileSystem;

/// <summary>
///     Path resolution interface providing platform-correct path combining
///     and parent directory retrieval. Implemented by the adapter layer to
///     correctly handle engine virtual paths (e.g., Godot's
///     <c>res://</c>, <c>user://</c>).
/// </summary>
public interface IPathResolver
{
    /// <summary>Combines a base path and a relative path using platform-appropriate separators.</summary>
    string CombinePath(string basePath, string relativePath);

    /// <summary>Gets the parent directory path of the given path.</summary>
    string GetParentDirectory(string path);
}
