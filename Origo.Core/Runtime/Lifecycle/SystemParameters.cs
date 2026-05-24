using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Save.Storage;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     SystemRun 构造所需的配置参数。
///     遵循统一构造协议：每层使用结构化参数对象构造。
/// </summary>
internal readonly record struct SystemParameters(
    ILogger Logger,
    IFileSystem FileSystem,
    string SaveRootPath,
    ISaveStorageService StorageService,
    ISavePathPolicy SavePathPolicy);
