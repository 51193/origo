using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.DataSource;
using Origo.Core.Runtime;
using Origo.Core.Save.Storage;

namespace Origo.Core.Snd;

public sealed class SndContextParameters
{
    public SndContextParameters(
        OrigoRuntime runtime,
        IDataSourceIoGateway dataSourceIo,
        IFileMetaAccess metaAccess,
        IPathResolver pathResolver,
        string saveRootPath,
        string initialSaveRootPath,
        string entryConfigPath)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(dataSourceIo);
        ArgumentNullException.ThrowIfNull(metaAccess);
        ArgumentNullException.ThrowIfNull(pathResolver);

        Runtime = runtime;
        DataSourceIo = dataSourceIo;
        MetaAccess = metaAccess;
        PathResolver = pathResolver;
        SaveRootPath = RequireText(saveRootPath, nameof(saveRootPath), "Save root path cannot be null or whitespace.");
        InitialSaveRootPath = RequireText(initialSaveRootPath, nameof(initialSaveRootPath),
            "Initial save root path cannot be null or whitespace.");
        EntryConfigPath = RequireText(entryConfigPath, nameof(entryConfigPath),
            "Entry config path cannot be null or whitespace.");
    }

    public OrigoRuntime Runtime { get; }
    public IDataSourceIoGateway DataSourceIo { get; }
    public IFileMetaAccess MetaAccess { get; }
    public IPathResolver PathResolver { get; }
    public string SaveRootPath { get; }
    public string InitialSaveRootPath { get; }
    public string EntryConfigPath { get; }

    /// <summary>The level ID for the initial save. Default value is <c>"default"</c>, corresponding to the initial/save_000/level_default/ directory structure.</summary>
    public string InitialLevelId { get; init; } = "default";

    public ISaveStorageService? StorageService { get; init; }
    public ISaveStorageService? InitialStorageService { get; init; }
    public ISavePathPolicy? SavePathPolicy { get; init; }

    /// <summary>Whether to automatically discover and register strategy types from assemblies.</summary>
    public bool AutoDiscoverStrategies { get; init; } = true;

    /// <summary>Assembly name prefixes to skip during automatic strategy discovery.</summary>
    public IReadOnlyList<string>? DiscoverySkipPrefixes { get; init; }

    /// <summary>Scene alias mapping file path. If set, it is automatically loaded during Bootstrap.</summary>
    public string? SceneAliasMapPath { get; init; }

    /// <summary>SND template mapping file path. If set, it is automatically loaded during Bootstrap.</summary>
    public string? SndTemplateMapPath { get; init; }

    /// <summary>
    ///     Converter registration callback invoked before Bootstrap.
    ///     Extension developers can register custom <see cref="DataSourceConverter{T}" /> here,
    ///     ensuring custom types are available before automatic strategy discovery,
    ///     template loading, and entry save loading.
    /// </summary>
    public Action<DataSourceConverterRegistry>? ConfigureConverters { get; init; }

    private static string RequireText(string value, string paramName, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(message, paramName);
        return value;
    }
}
