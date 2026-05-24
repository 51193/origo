using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;
using Origo.Core.Snd.Scene;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     系统层运行时容器，持有整个应用生命周期内共享的运行时对象。
///     由 <see cref="SystemRun" /> 持有，作为下层 <see cref="ProgressRun" /> 的构造依赖。
///     <para>
///         暴露面控制：对外仅提供 ProgressRun 构建所需的最小子集，
///         System 层独有能力（如 SystemBlackboard、ActiveSaveSlot 管理）不向下层暴露。
///     </para>
/// </summary>
internal sealed class SystemRuntime
{
    internal SystemRuntime(OrigoRuntime runtime, SystemParameters systemParams)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(systemParams.Logger);
        ArgumentNullException.ThrowIfNull(systemParams.FileSystem);
        if (string.IsNullOrWhiteSpace(systemParams.SaveRootPath))
            throw new ArgumentException("Save root path cannot be null or whitespace.", nameof(systemParams));
        ArgumentNullException.ThrowIfNull(systemParams.StorageService);
        ArgumentNullException.ThrowIfNull(systemParams.SavePathPolicy);

        Logger = systemParams.Logger;
        FileSystem = systemParams.FileSystem;
        SaveRootPath = systemParams.SaveRootPath;
        Runtime = runtime;
        StorageService = systemParams.StorageService;
        SavePathPolicy = systemParams.SavePathPolicy;
    }

    internal ILogger Logger { get; }
    internal IFileSystem FileSystem { get; }
    internal string SaveRootPath { get; }
    internal OrigoRuntime Runtime { get; }
    internal ISaveStorageService StorageService { get; }
    internal ISavePathPolicy SavePathPolicy { get; }

    // ── Convenience accessors ──

    internal SndWorld SndWorld => Runtime.SndWorld;
    internal SndRuntime SndRuntime => Runtime.Snd;
    internal ISndSceneHost ForegroundSceneHost => Runtime.Snd.SceneHost;
    internal IBlackboard SystemBlackboard => Runtime.SystemBlackboard;
}
