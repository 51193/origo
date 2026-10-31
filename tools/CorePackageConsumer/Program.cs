using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Origo.Core;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Logging;

var workspace = Path.Combine(Path.GetTempPath(), $"origo-core-consumer-{Guid.NewGuid():N}");
Directory.CreateDirectory(workspace);

try
{
    var fileSystem = new LocalFileSystem();
    var entryConfigPath = Path.Combine(workspace, "entry.json");
    var sndScenePath = Path.Combine(workspace, "main_menu.json");
    fileSystem.WriteAllText(sndScenePath, "[]", overwrite: false);
    fileSystem.WriteAllText(
        entryConfigPath,
        JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["levels"] = new Dictionary<string, object>
            {
                ["main_menu"] = new Dictionary<string, object>
                {
                    ["snd_scene"] = sndScenePath,
                },
            },
            ["main_menu_level"] = "main_menu",
        }),
        overwrite: false);

    using var host = OrigoHost.Create(new OrigoHostOptions
    {
        Meta = new OrigoMeta("CorePackageConsumer", "1.0.0", OrigoMeta.DefaultBanner),
        Logger = NullLogger.Instance,
        FileSystem = fileSystem,
        SaveRootPath = Path.Combine(workspace, "saves"),
        InitialSaveRootPath = Path.Combine(workspace, "initial"),
        EntryConfigPath = entryConfigPath,
        AutoDiscoverStrategies = false,
    });

    ArgumentNullException.ThrowIfNull(host.Runtime);
    ArgumentNullException.ThrowIfNull(host.Context);

    host.Bootstrap();
    host.DriveFrame(1.0 / 60.0);

    if (host.Runtime.SessionManager.ForegroundSession is null)
        throw new InvalidOperationException(
            "The deferred main-menu entry load did not create a foreground session.");

    IReadOnlyList<string> saves = host.Context.Save.ListSaves();
    if (saves.Count != 0)
        throw new InvalidOperationException(
            "A fresh Core host unexpectedly listed existing save slots.");

    var deferredRan = false;
    host.Context.Deferred.EnqueueBusinessDeferred(() => deferredRan = true);
    if (deferredRan)
        throw new InvalidOperationException(
            "The deferred business action ran before the frame boundary.");

    host.DriveFrame(1.0 / 60.0);
    if (!deferredRan)
        throw new InvalidOperationException(
            "The deferred business action did not run during the frame boundary.");

    Console.WriteLine("CORE_CONSUMER_STARTUP_OK");
}
finally
{
    if (Directory.Exists(workspace))
        Directory.Delete(workspace, recursive: true);
}

return 0;

/// <summary>
///     Minimal physical file-system implementation for the package-only
///     fixture. It uses the public Contracts abstraction because a Core-only
///     consumer cannot use the engine adapter's file system or a test-only
///     memory implementation.
/// </summary>
internal sealed class LocalFileSystem : IFileSystem
{
    /// <inheritdoc/>
    public bool Exists(string path) => File.Exists(path);

    /// <inheritdoc/>
    public bool DirectoryExists(string path) => Directory.Exists(path);

    /// <inheritdoc/>
    public string ReadAllText(string path) => File.ReadAllText(path);

    /// <inheritdoc/>
    public void WriteAllText(string path, string content, bool overwrite)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        if (!overwrite && File.Exists(path))
            throw new IOException($"File '{path}' already exists and overwrite is false.");
        File.WriteAllText(path, content);
    }

    /// <inheritdoc/>
    public void Copy(string sourcePath, string destinationPath, bool overwrite)
    {
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.Copy(sourcePath, destinationPath, overwrite);
    }

    /// <inheritdoc/>
    public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive) =>
        Directory.EnumerateFiles(
            directoryPath,
            searchPattern,
            recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

    /// <inheritdoc/>
    public void CreateDirectory(string directoryPath) => Directory.CreateDirectory(directoryPath);

    /// <inheritdoc/>
    public void Delete(string path) => File.Delete(path);

    /// <inheritdoc/>
    public string CombinePath(string basePath, string relativePath) => Path.Combine(basePath, relativePath);

    /// <inheritdoc/>
    public string GetParentDirectory(string path) => Path.GetDirectoryName(path) ?? path;

    /// <inheritdoc/>
    public IEnumerable<string> EnumerateDirectories(string directoryPath) =>
        Directory.EnumerateDirectories(directoryPath);

    /// <inheritdoc/>
    public void Rename(string sourcePath, string destinationPath)
    {
        if (Directory.Exists(sourcePath))
            Directory.Move(sourcePath, destinationPath);
        else
            File.Move(sourcePath, destinationPath);
    }

    /// <inheritdoc/>
    public void DeleteDirectory(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
            Directory.Delete(directoryPath, recursive: true);
    }
}
