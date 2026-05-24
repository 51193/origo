using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Logging;
using Origo.Core.DataSource;
using Origo.Core.Logging;

namespace Origo.Core.Save.Storage;

internal static class SavePayloadReader
{
    public static SaveGamePayload ReadFromCurrent(
        IFileSystem fileSystem,
        string saveRootPath,
        string saveId,
        string activeLevelId,
        ILogger? logger = null)
    {
        return ReadFromCurrent(
            fileSystem,
            SaveStorageGatewayFactory.CreateIoGateway(fileSystem),
            saveRootPath,
            saveId,
            activeLevelId,
            logger);
    }

    public static SaveGamePayload ReadFromCurrent(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string saveId,
        string activeLevelId,
        ILogger? logger = null)
    {
        return ReadFromCurrent(fileSystem, dataSourceIo, saveRootPath, saveId, activeLevelId,
            new DefaultSavePathPolicy(),
            logger);
    }

    public static SaveGamePayload ReadFromCurrent(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string saveId,
        string activeLevelId,
        ISavePathPolicy pathPolicy,
        ILogger? logger = null)
    {
        ValidateSaveRoot(fileSystem, saveRootPath);
        ArgumentNullException.ThrowIfNull(dataSourceIo);
        ArgumentNullException.ThrowIfNull(pathPolicy);
        var baseRel = pathPolicy.GetCurrentDirectory();

        var markerRel = pathPolicy.GetWriteInProgressMarker(baseRel);
        var markerAbs = fileSystem.CombinePath(saveRootPath, markerRel);
        if (fileSystem.Exists(markerAbs))
        {
            var ex = new InvalidOperationException(
                $"Detected write-in-progress marker at '{markerRel}' in current/; interrupted save write must be handled before loading.");
            (logger ?? NullLogger.Instance).Log(
                LogLevel.Error,
                nameof(SavePayloadReader),
                ex.Message);
            throw ex;
        }

        var progressRel = pathPolicy.GetProgressFile(baseRel);
        var progressSmRel = pathPolicy.GetProgressStateMachinesFile(baseRel);
        var (progressNode, progressStateMachinesNode, customMeta) = ReadProgressAndCustomMeta(
            fileSystem,
            dataSourceIo,
            saveRootPath,
            baseRel,
            pathPolicy,
            $"Missing required progress.json in current (path='{progressRel}').",
            $"Missing required progress_state_machines.json in current (path='{progressSmRel}').");

        var levels = CreateLevelPayloadMap(fileSystem, dataSourceIo, saveRootPath, baseRel, activeLevelId, pathPolicy);
        ReadRemainingLevelPayloads(fileSystem, dataSourceIo, saveRootPath, baseRel, levels, pathPolicy);

        return CreateSavePayload(
            saveId,
            activeLevelId,
            progressNode,
            progressStateMachinesNode,
            customMeta,
            levels);
    }

    public static SaveGamePayload ReadFromSnapshot(
        IFileSystem fileSystem,
        string saveRootPath,
        string saveId,
        string activeLevelId)
    {
        return ReadFromSnapshot(fileSystem, SaveStorageGatewayFactory.CreateIoGateway(fileSystem), saveRootPath, saveId,
            activeLevelId);
    }

    public static SaveGamePayload ReadFromSnapshot(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string saveId,
        string activeLevelId)
    {
        return ReadFromSnapshot(fileSystem, dataSourceIo, saveRootPath, saveId, activeLevelId,
            new DefaultSavePathPolicy());
    }

    public static SaveGamePayload ReadFromSnapshot(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string saveId,
        string activeLevelId,
        ISavePathPolicy pathPolicy)
    {
        ValidateSaveRoot(fileSystem, saveRootPath);
        ArgumentNullException.ThrowIfNull(dataSourceIo);
        ArgumentNullException.ThrowIfNull(pathPolicy);
        var baseRel = pathPolicy.GetSaveDirectory(saveId);
        var progressRel = pathPolicy.GetProgressFile(baseRel);
        var progressSmRel = pathPolicy.GetProgressStateMachinesFile(baseRel);
        var (progressNode, progressStateMachinesNode, customMeta) = ReadProgressAndCustomMeta(
            fileSystem,
            dataSourceIo,
            saveRootPath,
            baseRel,
            pathPolicy,
            $"Missing required progress.json in save '{saveId}' (path='{progressRel}').",
            $"Missing required progress_state_machines.json in save '{saveId}' (path='{progressSmRel}').");

        var levels = CreateLevelPayloadMap(fileSystem, dataSourceIo, saveRootPath, baseRel, activeLevelId, pathPolicy);
        ReadRemainingLevelPayloads(fileSystem, dataSourceIo, saveRootPath, baseRel, levels, pathPolicy);

        return CreateSavePayload(
            saveId,
            activeLevelId,
            progressNode,
            progressStateMachinesNode,
            customMeta,
            levels);
    }

    private static Dictionary<string, LevelPayload> CreateLevelPayloadMap(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string baseRel,
        string activeLevelId,
        ISavePathPolicy pathPolicy)
    {
        var level = ReadLevelPayload(fileSystem, dataSourceIo, saveRootPath, baseRel, activeLevelId, pathPolicy);
        return new Dictionary<string, LevelPayload> { [activeLevelId] = level };
    }

    private static SaveGamePayload CreateSavePayload(
        string saveId,
        string activeLevelId,
        DataSourceNode progressNode,
        DataSourceNode progressStateMachinesNode,
        IReadOnlyDictionary<string, string>? customMeta,
        Dictionary<string, LevelPayload> levels)
    {
        return new SaveGamePayload
        {
            SaveId = saveId,
            ActiveLevelId = activeLevelId,
            ProgressNode = progressNode,
            ProgressStateMachinesNode = progressStateMachinesNode,
            CustomMeta = customMeta,
            Levels = levels
        };
    }

    private static void ValidateSaveRoot(IFileSystem fileSystem, string saveRootPath)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        SaveStorageGatewayFactory.ValidateRootPath(saveRootPath, nameof(saveRootPath),
            "Save root path cannot be null or whitespace.");
    }

    private static (DataSourceNode ProgressNode, DataSourceNode ProgressStateMachinesNode,
        IReadOnlyDictionary<string, string>?
        CustomMeta)
        ReadProgressAndCustomMeta(
            IFileSystem fileSystem,
            IDataSourceIoGateway dataSourceIo,
            string saveRootPath,
            string baseDirectoryRel,
            ISavePathPolicy pathPolicy,
            string missingProgressMessage,
            string missingProgressStateMachinesMessage)
    {
        var progressRel = pathPolicy.GetProgressFile(baseDirectoryRel);
        var progressAbs = fileSystem.CombinePath(saveRootPath, progressRel);
        if (!fileSystem.Exists(progressAbs))
            throw new InvalidOperationException(missingProgressMessage);
        var progressNode = dataSourceIo.ReadTree(progressAbs);

        var progressSmRel = pathPolicy.GetProgressStateMachinesFile(baseDirectoryRel);
        var progressSmAbs = fileSystem.CombinePath(saveRootPath, progressSmRel);
        if (!fileSystem.Exists(progressSmAbs))
            throw new InvalidOperationException(missingProgressStateMachinesMessage);
        var progressStateMachinesNode = dataSourceIo.ReadTree(progressSmAbs);

        var customMetaRel = pathPolicy.GetCustomMetaFile(baseDirectoryRel);
        var customMetaAbs = fileSystem.CombinePath(saveRootPath, customMetaRel);
        var customMeta = TryReadStringMap(fileSystem, dataSourceIo, customMetaAbs);

        return (progressNode, progressStateMachinesNode, customMeta);
    }

    internal static IReadOnlyDictionary<string, string>? TryReadStringMap(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string mapFileAbs)
    {
        if (!fileSystem.Exists(mapFileAbs))
            return null;

        using var root = dataSourceIo.ReadTree(mapFileAbs);
        if (root.Kind != DataSourceNodeKind.Object)
            throw new InvalidOperationException(
                $"Expected map file '{mapFileAbs}' to decode as object node.");

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in root.Keys)
        {
            var valueNode = root[key];
            if (valueNode.Kind is DataSourceNodeKind.Object or DataSourceNodeKind.Array)
                throw new InvalidOperationException(
                    $"Map file '{mapFileAbs}' key '{key}' must be scalar.");
            result[key] = valueNode.AsString();
        }

        return result;
    }

    public static DataSourceNode? ReadProgressNodeFromSnapshot(
        IFileSystem fileSystem,
        string saveRootPath,
        string saveId)
    {
        return ReadProgressNodeFromSnapshot(fileSystem, SaveStorageGatewayFactory.CreateIoGateway(fileSystem),
            saveRootPath,
            saveId);
    }

    public static DataSourceNode? ReadProgressNodeFromSnapshot(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string saveId)
    {
        return ReadProgressNodeFromSnapshot(fileSystem, dataSourceIo, saveRootPath, saveId,
            new DefaultSavePathPolicy());
    }

    public static DataSourceNode? ReadProgressNodeFromSnapshot(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string saveId,
        ISavePathPolicy pathPolicy)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(dataSourceIo);
        ArgumentNullException.ThrowIfNull(pathPolicy);
        SaveStorageGatewayFactory.ValidateRootPath(saveRootPath, nameof(saveRootPath),
            "Save root path cannot be null or whitespace.");

        var saveRel = pathPolicy.GetSaveDirectory(saveId);
        var progressRel = pathPolicy.GetProgressFile(saveRel);
        var progressAbs = fileSystem.CombinePath(saveRootPath, progressRel);

        return fileSystem.Exists(progressAbs) ? dataSourceIo.ReadTree(progressAbs) : null;
    }

    public static LevelPayload? TryReadLevelPayloadFromCurrent(
        IFileSystem fileSystem,
        string saveRootPath,
        string levelId)
    {
        return TryReadLevelPayloadFromCurrent(fileSystem, SaveStorageGatewayFactory.CreateIoGateway(fileSystem),
            saveRootPath,
            levelId);
    }

    public static LevelPayload? TryReadLevelPayloadFromCurrent(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string levelId)
    {
        return TryReadLevelPayloadFromCurrent(fileSystem, dataSourceIo, saveRootPath, levelId,
            new DefaultSavePathPolicy());
    }

    public static LevelPayload? TryReadLevelPayloadFromCurrent(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string levelId,
        ISavePathPolicy pathPolicy)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(pathPolicy);
        SaveStorageGatewayFactory.ValidateRootPath(saveRootPath, nameof(saveRootPath),
            "Save root path cannot be null or whitespace.");

        var currentRel = pathPolicy.GetCurrentDirectory();
        ThrowIfWriteInProgressMarkerExists(fileSystem, saveRootPath, currentRel, pathPolicy);
        return TryReadLevelPayload(fileSystem, dataSourceIo, saveRootPath, currentRel, levelId, pathPolicy);
    }

    public static LevelPayload? TryReadLevelPayloadFromSnapshot(
        IFileSystem fileSystem,
        string snapshotRootPath,
        string saveId,
        string levelId)
    {
        return TryReadLevelPayloadFromSnapshot(fileSystem, SaveStorageGatewayFactory.CreateIoGateway(fileSystem),
            snapshotRootPath, saveId, levelId);
    }

    public static LevelPayload? TryReadLevelPayloadFromSnapshot(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string snapshotRootPath,
        string saveId,
        string levelId)
    {
        return TryReadLevelPayloadFromSnapshot(fileSystem, dataSourceIo, snapshotRootPath, saveId, levelId,
            new DefaultSavePathPolicy());
    }

    public static LevelPayload? TryReadLevelPayloadFromSnapshot(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string snapshotRootPath,
        string saveId,
        string levelId,
        ISavePathPolicy pathPolicy)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(pathPolicy);
        SaveStorageGatewayFactory.ValidateRootPath(snapshotRootPath, nameof(snapshotRootPath),
            "Snapshot root path cannot be null or whitespace.");

        var saveRel = pathPolicy.GetSaveDirectory(saveId);
        return TryReadLevelPayload(fileSystem, dataSourceIo, snapshotRootPath, saveRel, levelId, pathPolicy);
    }

    private static LevelPayload? TryReadLevelPayload(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string rootPath,
        string baseDirectoryRel,
        string levelId,
        ISavePathPolicy pathPolicy)
    {
        var files = LevelFiles.Create(fileSystem, rootPath, baseDirectoryRel, levelId, pathPolicy);
        if (files.AllMissing)
            return null;
        ValidateRequiredLevelFiles(files);

        return new LevelPayload
        {
            LevelId = levelId,
            SndSceneNode = dataSourceIo.ReadTree(files.SndScene.AbsolutePath),
            SessionNode = dataSourceIo.ReadTree(files.Session.AbsolutePath),
            SessionStateMachinesNode = dataSourceIo.ReadTree(files.SessionStateMachines.AbsolutePath)
        };
    }

    private static void ValidateRequiredLevelFiles(LevelFiles files)
    {
        if (!files.SndScene.Exists)
            throw new InvalidOperationException(
                $"Missing required snd_scene.json for level '{files.LevelId}' (path='{files.SndScene.RelativePath}').");
        if (!files.Session.Exists)
            throw new InvalidOperationException(
                $"Missing required session.json for level '{files.LevelId}' (path='{files.Session.RelativePath}').");
        if (!files.SessionStateMachines.Exists)
            throw new InvalidOperationException(
                $"Missing required session_state_machines.json for level '{files.LevelId}' (path='{files.SessionStateMachines.RelativePath}').");
    }

    private static LevelPayload ReadLevelPayload(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string baseDirectoryRel,
        string levelId,
        ISavePathPolicy pathPolicy)
    {
        return TryReadLevelPayload(fileSystem, dataSourceIo, saveRootPath, baseDirectoryRel, levelId, pathPolicy)
               ?? throw new InvalidOperationException($"Missing level '{levelId}'.");
    }

    private static void ThrowIfWriteInProgressMarkerExists(
        IFileSystem fileSystem,
        string saveRootPath,
        string baseRel,
        ISavePathPolicy pathPolicy)
    {
        var markerRel = pathPolicy.GetWriteInProgressMarker(baseRel);
        var markerAbs = fileSystem.CombinePath(saveRootPath, markerRel);
        if (fileSystem.Exists(markerAbs))
            throw new InvalidOperationException(
                $"Detected write-in-progress marker at '{markerRel}' in current/; interrupted save write must be handled before loading.");
    }

    /// <summary>
    ///     枚举基目录下所有 level_* 子目录，读取尚未包含在 <paramref name="levels" /> 中的关卡 payload。
    ///     用于在读取活跃关卡后补充读取后台会话关卡。
    /// </summary>
    private static void ReadRemainingLevelPayloads(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string baseDirectoryRel,
        Dictionary<string, LevelPayload> levels,
        ISavePathPolicy pathPolicy)
    {
        var baseAbs = fileSystem.CombinePath(saveRootPath, baseDirectoryRel);
        if (!fileSystem.DirectoryExists(baseAbs))
            return;

        foreach (var dirAbs in fileSystem.EnumerateDirectories(baseAbs))
        {
            var levelId = TryExtractLevelId(dirAbs);
            if (levelId is null || levels.ContainsKey(levelId))
                continue;

            var levelPayload = TryReadLevelPayload(fileSystem, dataSourceIo, saveRootPath, baseDirectoryRel, levelId,
                pathPolicy);
            if (levelPayload is not null)
                levels[levelId] = levelPayload;
        }
    }

    private static string? TryExtractLevelId(string directoryPath)
    {
        var leaf = SavePathResolver.GetLeafDirectoryName(directoryPath);
        if (!leaf.StartsWith(SavePathLayout.LevelDirectoryPrefix, StringComparison.Ordinal))
            return null;

        var levelId = leaf.Substring(SavePathLayout.LevelDirectoryPrefix.Length);
        return string.IsNullOrWhiteSpace(levelId) ? null : levelId;
    }

    private sealed record LevelFiles(
        string LevelId,
        LevelFile SndScene,
        LevelFile Session,
        LevelFile SessionStateMachines)
    {
        internal bool AllMissing => !SndScene.Exists && !Session.Exists && !SessionStateMachines.Exists;

        internal static LevelFiles Create(
            IFileSystem fileSystem,
            string rootPath,
            string baseDirectoryRel,
            string levelId,
            ISavePathPolicy pathPolicy)
        {
            var levelDirRel = pathPolicy.GetLevelDirectory(baseDirectoryRel, levelId);
            var sndSceneRel = pathPolicy.GetLevelSndSceneFile(levelDirRel);
            var sessionRel = pathPolicy.GetLevelSessionFile(levelDirRel);
            var sessionStateMachinesRel = pathPolicy.GetLevelSessionStateMachinesFile(levelDirRel);

            var sndSceneAbs = fileSystem.CombinePath(rootPath, sndSceneRel);
            var sessionAbs = fileSystem.CombinePath(rootPath, sessionRel);
            var sessionStateMachinesAbs = fileSystem.CombinePath(rootPath, sessionStateMachinesRel);

            return new LevelFiles(
                levelId,
                CreateLevelFile(fileSystem, sndSceneRel, sndSceneAbs),
                CreateLevelFile(fileSystem, sessionRel, sessionAbs),
                CreateLevelFile(fileSystem, sessionStateMachinesRel, sessionStateMachinesAbs));
        }

        private static LevelFile CreateLevelFile(IFileSystem fileSystem, string relativePath, string absolutePath) =>
            new(relativePath, absolutePath, fileSystem.Exists(absolutePath));
    }

    private sealed record LevelFile(string RelativePath, string AbsolutePath, bool Exists);
}
