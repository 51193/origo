using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.DataSource;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;
using Origo.Core.Snd.Scene;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     System-layer runtime container, holding runtime objects shared across the entire application lifecycle.
///     Held by <see cref="SystemRun" /> and serves as a construction dependency for the lower
///     <see cref="ProgressRun" />.
///     <para>
///         Surface control: only exposes the minimal subset needed for ProgressRun construction;
///         System-layer exclusive capabilities (such as SystemBlackboard and ActiveSaveSlot management)
///         are not exposed to lower layers.
///     </para>
/// </summary>
internal sealed class SystemRuntime
{
    internal SystemRuntime(OrigoRuntime runtime, SystemParameters systemParams)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(systemParams.Logger);
        ArgumentNullException.ThrowIfNull(systemParams.MetaAccess);
        ArgumentNullException.ThrowIfNull(systemParams.PathResolver);
        if (string.IsNullOrWhiteSpace(systemParams.SaveRootPath))
            throw new ArgumentException("Save root path cannot be null or whitespace.", nameof(systemParams));
        ArgumentNullException.ThrowIfNull(systemParams.StorageService);
        ArgumentNullException.ThrowIfNull(systemParams.SavePathPolicy);
        ArgumentNullException.ThrowIfNull(systemParams.AdapterSceneHost);

        Logger = systemParams.Logger;
        MetaAccess = systemParams.MetaAccess;
        PathResolver = systemParams.PathResolver;
        SaveRootPath = systemParams.SaveRootPath;
        Runtime = runtime;
        StorageService = systemParams.StorageService;
        SavePathPolicy = systemParams.SavePathPolicy;
        AdapterSceneHost = systemParams.AdapterSceneHost;
    }

    internal ILogger Logger { get; }
    internal IFileMetaAccess MetaAccess { get; }
    internal IPathResolver PathResolver { get; }
    internal string SaveRootPath { get; }
    internal OrigoRuntime Runtime { get; }
    internal ISaveStorageService StorageService { get; }
    internal ISavePathPolicy SavePathPolicy { get; }

    // ── Convenience accessors ──

    internal SndWorld SndWorld => Runtime.SndWorld;
    internal ISndSceneHost AdapterSceneHost { get; }
    internal IBlackboard SystemBlackboard => Runtime.SystemBlackboard;
}
