using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Runtime;
using Origo.Core.Save.Storage;

namespace Origo.Core.Snd;

public sealed class SndContextParameters
{
    public SndContextParameters(
        OrigoRuntime runtime,
        IFileSystem fileSystem,
        string saveRootPath,
        string initialSaveRootPath,
        string entryConfigPath)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(fileSystem);

        Runtime = runtime;
        FileSystem = fileSystem;
        SaveRootPath = RequireText(saveRootPath, nameof(saveRootPath), "Save root path cannot be null or whitespace.");
        InitialSaveRootPath = RequireText(initialSaveRootPath, nameof(initialSaveRootPath),
            "Initial save root path cannot be null or whitespace.");
        EntryConfigPath = RequireText(entryConfigPath, nameof(entryConfigPath),
            "Entry config path cannot be null or whitespace.");
    }

    public OrigoRuntime Runtime { get; }
    public IFileSystem FileSystem { get; }
    public string SaveRootPath { get; }
    public string InitialSaveRootPath { get; }
    public string EntryConfigPath { get; }
    public ISaveStorageService? StorageService { get; init; }
    public ISaveStorageService? InitialStorageService { get; init; }
    public ISavePathPolicy? SavePathPolicy { get; init; }

    /// <summary>是否自动发现并注册程序集中的策略类型。</summary>
    public bool AutoDiscoverStrategies { get; init; } = true;

    /// <summary>策略自动发现时跳过的程序集名前缀。</summary>
    public IReadOnlyList<string>? DiscoverySkipPrefixes { get; init; }

    /// <summary>场景别名映射文件路径。若设置则 Bootstrap 时自动加载。</summary>
    public string? SceneAliasMapPath { get; init; }

    /// <summary>SND 模板映射文件路径。若设置则 Bootstrap 时自动加载。</summary>
    public string? SndTemplateMapPath { get; init; }

    private static string RequireText(string value, string paramName, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(message, paramName);
        return value;
    }
}
