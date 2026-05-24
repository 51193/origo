using System.Collections.Generic;

namespace Origo.Core.Abstractions.FileSystem;

/// <summary>
///     抽象文件系统访问接口，屏蔽具体平台与引擎 API。
///     路径操作（拼接、取父目录等）也由实现负责，以正确处理引擎虚拟路径（如 Godot 的 res://、user://）。
/// </summary>
public interface IFileSystem
{
    bool Exists(string path);

    bool DirectoryExists(string path);

    string ReadAllText(string path);

    void WriteAllText(string path, string content, bool overwrite);

    void Copy(string sourcePath, string destinationPath, bool overwrite);

    IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive);

    void CreateDirectory(string directoryPath);

    /// <summary>
    ///     删除指定路径的文件。若不存在则忽略。
    /// </summary>
    void Delete(string path);

    /// <summary>
    ///     将基路径和相对路径拼接在一起，使用平台正确的分隔符。
    /// </summary>
    string CombinePath(string basePath, string relativePath);

    /// <summary>
    ///     获取指定路径的父目录路径。
    /// </summary>
    string GetParentDirectory(string path);

    /// <summary>
    ///     枚举指定目录下的直接子目录，返回完整路径列表。
    /// </summary>
    IEnumerable<string> EnumerateDirectories(string directoryPath);

    /// <summary>
    ///     原子地将目录或文件从 <paramref name="sourcePath" /> 重命名/移动到 <paramref name="destinationPath" />。
    ///     若目标已存在，行为由实现决定（可覆盖或抛异常）。
    /// </summary>
    void Rename(string sourcePath, string destinationPath);

    /// <summary>
    ///     递归删除指定目录及其全部内容。若目录不存在则忽略。
    /// </summary>
    void DeleteDirectory(string directoryPath);
}
