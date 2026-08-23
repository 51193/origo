using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.DataSource;
using Origo.Core.Runtime;
using Origo.Core.Save.Storage;

namespace Origo.Core.Snd;

/// <summary>
///     Constructor parameters for <see cref="SndContext" />: runtime, I/O
///     gateway, and storage wiring, plus optional bootstrap configuration
///     (strategy discovery, alias/template maps, custom converters).
/// </summary>
public sealed class SndContextParameters
{
    /// <summary>
    ///     Creates the parameter set from the runtime and the I/O/storage
    ///     infrastructure the context will use.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when a required reference parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a required path is null or whitespace.</exception>
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

    /// <summary>The runtime the context drives.</summary>
    public OrigoRuntime Runtime { get; }

    /// <summary>Data-source I/O gateway used for all file content reads and writes.</summary>
    public IDataSourceIoGateway DataSourceIo { get; }

    /// <summary>File metadata access used by the save system.</summary>
    public IFileMetaAccess MetaAccess { get; }

    /// <summary>Path resolver used to combine and normalize save paths.</summary>
    public IPathResolver PathResolver { get; }

    /// <summary>Root directory for runtime saves.</summary>
    public string SaveRootPath { get; }

    /// <summary>Root directory for the initial (res://) saves.</summary>
    public string InitialSaveRootPath { get; }

    /// <summary>Path to the entry config file (<c>entry.json</c>).</summary>
    public string EntryConfigPath { get; }

    /// <summary>
    ///     The level ID for the initial save. Default value is <c>"default"</c>,
    ///     corresponding to the initial/save_000/level_default/ directory
    ///     structure. Validated by <see cref="SndContext" /> construction with
    ///     the same token rules as save IDs and level IDs.
    /// </summary>
    public string InitialLevelId { get; init; } = "default";

    /// <summary>Custom save storage service for runtime saves; defaults to <c>DefaultSaveStorageService</c>.</summary>
    public ISaveStorageService? StorageService { get; init; }

    /// <summary>Custom save storage service for the initial (res://) saves; defaults to <c>DefaultSaveStorageService</c>.</summary>
    public ISaveStorageService? InitialStorageService { get; init; }

    /// <summary>Custom save path policy; defaults to <c>DefaultSavePathPolicy</c>.</summary>
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
