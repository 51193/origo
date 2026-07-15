using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Origo.Core.Abstractions.FileSystem;

namespace Origo.TestSupport;

public sealed class TestMemoryFileSystem : IFileSystem
{
    private readonly HashSet<string> _directories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    public int ReadAllTextCallCount { get; private set; }

    public bool Exists(string path) => _files.ContainsKey(Normalize(path));

    public bool DirectoryExists(string path)
    {
        var normalized = Normalize(path).TrimEnd('/');
        return _directories.Contains(normalized) ||
               _files.Keys.Any(f => f.StartsWith(normalized + "/", StringComparison.Ordinal));
    }

    public string ReadAllText(string path)
    {
        ReadAllTextCallCount++;
        var normalized = Normalize(path);
        if (!_files.TryGetValue(normalized, out var content))
            throw new FileNotFoundException($"File not found: {normalized}", normalized);
        return content;
    }

    public void WriteAllText(string path, string content, bool overwrite)
    {
        var normalized = Normalize(path);
        if (!overwrite && _files.ContainsKey(normalized))
            throw new IOException($"File already exists: {normalized}");

        _files[normalized] = content;
        EnsureParents(normalized);
    }

    public void Copy(string sourcePath, string destinationPath, bool overwrite)
    {
        var source = Normalize(sourcePath);
        var destination = Normalize(destinationPath);
        if (!_files.TryGetValue(source, out var content))
            throw new FileNotFoundException("Source not found.", source);

        if (!overwrite && _files.ContainsKey(destination))
            throw new IOException($"File already exists: {destination}");

        _files[destination] = content;
        EnsureParents(destination);
    }

    public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive)
    {
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

            if (searchPattern is "*" or "*.*" || file.EndsWith(searchPattern.TrimStart('*'), StringComparison.Ordinal))
                yield return file;
        }
    }

    public void CreateDirectory(string directoryPath)
    {
        var normalized = Normalize(directoryPath).TrimEnd('/');
        if (normalized.Length == 0)
            return;

        _directories.Add(normalized);
        EnsureParents(normalized + "/dummy");
    }

    public void Delete(string path)
    {
        var normalized = Normalize(path);
        _files.Remove(normalized);
    }

    public string CombinePath(string basePath, string relativePath) =>
        Normalize($"{Normalize(basePath).TrimEnd('/')}/{relativePath}");

    public string GetParentDirectory(string path)
    {
        var normalized = Normalize(path).TrimEnd('/');
        var index = normalized.LastIndexOf('/');
        return index <= 0 ? string.Empty : normalized[..index];
    }

    public IEnumerable<string> EnumerateDirectories(string directoryPath)
    {
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
                children.Add(string.Concat(prefix.AsSpan(), rest.AsSpan(0, slash)));
        }

        return children;
    }

    public void Rename(string sourcePath, string destinationPath)
    {
        var src = Normalize(sourcePath).TrimEnd('/');
        var dst = Normalize(destinationPath).TrimEnd('/');

        var srcPrefix = src + "/";
        var filesToMove = _files.Keys.Where(f => f.StartsWith(srcPrefix, StringComparison.Ordinal) || f == src)
            .ToList();
        foreach (var file in filesToMove)
        {
            var newPath = string.Concat(dst.AsSpan(), file.AsSpan(src.Length));
            _files[newPath] = _files[file];
            _files.Remove(file);
            EnsureParents(newPath);
        }

        var dirsToMove = _directories.Where(d => d.StartsWith(srcPrefix, StringComparison.Ordinal) || d == src)
            .ToList();
        foreach (var dir in dirsToMove)
        {
            var newDir = string.Concat(dst.AsSpan(), dir.AsSpan(src.Length));
            _directories.Remove(dir);
            _directories.Add(newDir);
        }

        EnsureParents(dst + "/dummy");
    }

    public void DeleteDirectory(string directoryPath)
    {
        var normalized = Normalize(directoryPath).TrimEnd('/');
        var prefix = normalized + "/";

        var filesToRemove = _files.Keys.Where(f => f.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        foreach (var file in filesToRemove)
            _files.Remove(file);

        var dirsToRemove = _directories.Where(d => d == normalized || d.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();
        foreach (var dir in dirsToRemove)
            _directories.Remove(dir);
    }

    public void SeedFile(string path, string content)
    {
        var normalized = Normalize(path);
        _files[normalized] = content;
        EnsureParents(normalized);
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
