using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Logging;
using Origo.Core.DataSource;
using Origo.Core.Logging;

namespace Origo.Core;

/// <summary>
///     Consumer configuration for the Core shell host facade. All values use
///     stable Contracts types; the kernel constructs the concrete runtime and
///     SND context behind the facade.
/// </summary>
public sealed class OrigoHostOptions
{
    /// <summary>Framework metadata (name, version, banner).</summary>
    public required OrigoMeta Meta { get; init; }

    /// <summary>Logger used by the runtime and kernel services.</summary>
    public ILogger Logger { get; init; } = NullLogger.Instance;

    /// <summary>
    ///     File system used for entry configuration and saves. When null, any
    ///     file access fails fast with an actionable error; host workflows that
    ///     never touch files remain usable.
    /// </summary>
    public IFileSystem? FileSystem { get; init; }

    /// <summary>Root directory for runtime saves.</summary>
    public string SaveRootPath { get; init; } = "origo_saves";

    /// <summary>Root directory for initial (read-only) saves.</summary>
    public string InitialSaveRootPath { get; init; } = "origo_initial";

    /// <summary>Path to the entry configuration file.</summary>
    public string EntryConfigPath { get; init; } = "entry.json";

    /// <summary>Whether strategy types are discovered automatically during bootstrap.</summary>
    public bool AutoDiscoverStrategies { get; init; } = true;

    /// <summary>Optional assembly simple-name prefixes skipped during strategy discovery.</summary>
    public IReadOnlyList<string>? DiscoverySkipPrefixes { get; init; }

    /// <summary>Optional scene alias mapping file path.</summary>
    public string? SceneAliasMapPath { get; init; }

    /// <summary>Optional SND template mapping file path.</summary>
    public string? SndTemplateMapPath { get; init; }

    /// <summary>Optional console input queue.</summary>
    public IConsoleInputSource? ConsoleInput { get; init; }

    /// <summary>Optional console output channel.</summary>
    public IConsoleOutputChannel? ConsoleOutputChannel { get; init; }

    /// <summary>Optional custom data-source converter registration callback.</summary>
    public Action<DataSourceConverterRegistry>? ConfigureConverters { get; init; }
}
