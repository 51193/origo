using System.Collections.Generic;
using Origo.Core.Abstractions.FileSystem;

namespace Origo.GodotAdapter.FileSystem;

/// <summary>
///     基于 Godot <see cref="FileAccess" /> 与 <see cref="DirAccess" /> 的
///     <see cref="IFileSystem" /> 实现，支持 <c>res://</c> 和 <c>user://</c> 路径。
///     路径拼接和父目录操作也在此实现，以正确处理 Godot 虚拟路径。
/// </summary>
public sealed class GodotFileSystem : IFileSystem
{
    public bool Exists(string path) => GodotFileOperations.Exists(path);

    public bool DirectoryExists(string path) => GodotDirectoryOperations.Exists(path);

    public string ReadAllText(string path) => GodotFileOperations.ReadAllText(path);

    public void WriteAllText(string path, string content, bool overwrite) =>
        GodotFileOperations.WriteAllText(path, content, overwrite);

    public void Copy(string sourcePath, string destinationPath, bool overwrite) =>
        GodotFileOperations.Copy(sourcePath, destinationPath, overwrite);

    public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive) =>
        GodotDirectoryOperations.EnumerateFiles(directoryPath, searchPattern, recursive);

    public void CreateDirectory(string directoryPath) => GodotDirectoryOperations.Create(directoryPath);

    public void Delete(string path) => GodotFileOperations.Delete(path);

    public string CombinePath(string basePath, string relativePath) =>
        GodotPathResolver.Combine(basePath, relativePath);

    public IEnumerable<string> EnumerateDirectories(string directoryPath) =>
        GodotDirectoryOperations.EnumerateDirectories(directoryPath);

    public string GetParentDirectory(string path) => GodotPathResolver.GetParentDirectory(path);

    public void Rename(string sourcePath, string destinationPath) =>
        GodotDirectoryOperations.Rename(sourcePath, destinationPath);

    public void DeleteDirectory(string directoryPath) => GodotDirectoryOperations.DeleteRecursive(directoryPath);
}
