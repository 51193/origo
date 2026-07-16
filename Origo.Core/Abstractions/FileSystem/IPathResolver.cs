namespace Origo.Core.Abstractions.FileSystem;

/// <summary>
///     Path resolution interface providing platform-correct path combining
///     and parent directory retrieval. Implemented by the adapter layer to
///     correctly handle engine virtual paths (e.g., Godot's
///     <c>res://</c>, <c>user://</c>).
/// </summary>
public interface IPathResolver
{
    string CombinePath(string basePath, string relativePath);

    string GetParentDirectory(string path);
}
