using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Origo.Core.Abstractions.FileSystem;

namespace Origo.Core.DataSource;

/// <summary>
///     A pure in-memory <see cref="IFileSystem" /> implementation, with no dependency on any physical
///     file system or engine API. Exposed as a dependency-free reference implementation for tests
///     and adapter layers (see CHANGELOG for the public-status rationale).
/// </summary>
public sealed class MemoryFileSystem : IFileSystem
{
    private readonly HashSet<string> _directories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public bool Exists(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return _files.ContainsKey(Normalize(path));
    }

    /// <inheritdoc />
    public bool DirectoryExists(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var normalized = Normalize(path).TrimEnd('/');
        return _directories.Contains(normalized) ||
               _files.Keys.Any(f => f.StartsWith(normalized + "/", StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public string ReadAllText(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var normalized = Normalize(path);
        if (!_files.TryGetValue(normalized, out var content))
            throw new FileNotFoundException($"File not found: {normalized}", normalized);
        return content;
    }

    /// <inheritdoc />
    public void WriteAllText(string path, string content, bool overwrite)
    {
        ArgumentNullException.ThrowIfNull(path);
        var normalized = Normalize(path);
        if (!overwrite && _files.ContainsKey(normalized))
            throw new IOException($"File already exists: {normalized}");

        _files[normalized] = content;
        EnsureParents(normalized);
    }

    /// <inheritdoc />
    public void Copy(string sourcePath, string destinationPath, bool overwrite)
    {
        ArgumentNullException.ThrowIfNull(sourcePath);
        ArgumentNullException.ThrowIfNull(destinationPath);
        var source = Normalize(sourcePath);
        var destination = Normalize(destinationPath);
        if (!_files.TryGetValue(source, out var content))
            throw new FileNotFoundException("Source not found.", source);

        if (!overwrite && _files.ContainsKey(destination))
            throw new IOException($"File already exists: {destination}");

        _files[destination] = content;
        EnsureParents(destination);
    }

    /// <inheritdoc />
    public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        ArgumentNullException.ThrowIfNull(searchPattern);
        var normalized = Normalize(directoryPath).TrimEnd('/');
        var prefix = normalized + "/";
        foreach (var file in _files.Keys.ToArray())
        {
            if (!file.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            if (!recursive)
            {
                var rest = file[prefix.Length..];
                if (rest.Contains('/'))
                    continue;
            }

            if (searchPattern is "*" or "*.*" ||
                file.EndsWith(searchPattern.TrimStart('*'), StringComparison.Ordinal))
                yield return file;
        }
    }

    /// <inheritdoc />
    public void CreateDirectory(string directoryPath)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        var normalized = Normalize(directoryPath).TrimEnd('/');
        if (normalized.Length == 0)
            return;

        _directories.Add(normalized);
        EnsureParents(normalized + "/dummy");
    }

    /// <inheritdoc />
    public void Delete(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var normalized = Normalize(path);
        _files.Remove(normalized);
    }

    /// <inheritdoc />
    public string CombinePath(string basePath, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(basePath);
        return Normalize($"{Normalize(basePath).TrimEnd('/')}/{relativePath}");
    }

    /// <inheritdoc />
    public string GetParentDirectory(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var normalized = Normalize(path).TrimEnd('/');
        var index = normalized.LastIndexOf('/');
        return index <= 0 ? string.Empty : normalized[..index];
    }

    /// <inheritdoc />
    public IEnumerable<string> EnumerateDirectories(string directoryPath)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        var normalized = Normalize(directoryPath).TrimEnd('/');
        var prefix = normalized + "/";
        var children = new HashSet<string>(StringComparer.Ordinal);

        foreach (var dir in _directories)
        {
            if (!dir.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            var rest = dir[prefix.Length..];
            var slash = rest.IndexOf('/');
            if (slash >= 0)
                rest = rest[..slash];
            if (rest.Length > 0)
                children.Add(prefix + rest);
        }

        foreach (var file in _files.Keys.ToArray())
        {
            if (!file.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            var rest = file[prefix.Length..];
            var slash = rest.IndexOf('/');
            if (slash > 0)
                children.Add(string.Concat(prefix, rest.AsSpan(0, slash)));
        }

        return children;
    }

    /// <inheritdoc />
    public void Rename(string sourcePath, string destinationPath)
    {
        ArgumentNullException.ThrowIfNull(sourcePath);
        ArgumentNullException.ThrowIfNull(destinationPath);
        var src = Normalize(sourcePath).TrimEnd('/');
        var dst = Normalize(destinationPath).TrimEnd('/');

        var srcPrefix = src + "/";
        var filesToMove = _files.Keys
            .Where(f => f.StartsWith(srcPrefix, StringComparison.Ordinal) || f == src)
            .ToList();
        foreach (var file in filesToMove)
        {
            var newPath = string.Concat(dst, file.AsSpan(src.Length));
            _files[newPath] = _files[file];
            _files.Remove(file);
            EnsureParents(newPath);
        }

        var dirsToMove = _directories
            .Where(d => d.StartsWith(srcPrefix, StringComparison.Ordinal) || d == src)
            .ToList();
        foreach (var dir in dirsToMove)
        {
            var newDir = string.Concat(dst, dir.AsSpan(src.Length));
            _directories.Remove(dir);
            _directories.Add(newDir);
        }

        EnsureParents(dst + "/dummy");
    }

    /// <inheritdoc />
    public void DeleteDirectory(string directoryPath)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        var normalized = Normalize(directoryPath).TrimEnd('/');
        var prefix = normalized + "/";

        var filesToRemove = _files.Keys
            .Where(f => f.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();
        foreach (var file in filesToRemove)
            _files.Remove(file);

        var dirsToRemove = _directories
            .Where(d => d == normalized || d.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();
        foreach (var dir in dirsToRemove)
            _directories.Remove(dir);
    }

    private static string Normalize(string path) => path.Replace('\\', '/').Trim();

    private void EnsureParents(string filePath)
    {
        var normalized = Normalize(filePath);
        var index = normalized.LastIndexOf('/');
        while (index > 0)
        {
            var dir = normalized[..index];
            _directories.Add(dir);
            index = dir.LastIndexOf('/');
        }
    }
}
