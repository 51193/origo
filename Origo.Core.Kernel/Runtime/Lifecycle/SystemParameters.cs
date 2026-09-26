using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.DataSource;
using Origo.Core.Save.Storage;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     Configuration parameters required for constructing a SystemRun.
///     Follows the unified construction protocol: each layer is constructed using a structured parameter object.
/// </summary>
internal readonly record struct SystemParameters(
    ILogger Logger,
    IFileMetaAccess MetaAccess,
    IPathResolver PathResolver,
    string SaveRootPath,
    ISaveStorageService StorageService,
    ISavePathPolicy SavePathPolicy,
    ISndSceneHost AdapterSceneHost);
