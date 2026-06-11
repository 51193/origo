namespace Origo.Core.Abstractions.FileSystem;

/// <summary>
///     路径解析接口：提供平台正确的路径拼接和父目录获取。
///     由适配层实现以正确处理引擎虚拟路径（如 Godot 的 <c>res://</c>、<c>user://</c>）。
/// </summary>
public interface IPathResolver
{
    string CombinePath(string basePath, string relativePath);

    string GetParentDirectory(string path);
}
