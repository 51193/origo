using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.DataSource;

namespace Origo.Core.Save.Storage;

internal static class SavePayloadWriter
{
    public static void WriteProgressOnlyToCurrent(
        IFileSystem fileSystem,
        string saveRootPath,
        DataSourceNode progressNode,
        DataSourceNode progressStateMachinesNode,
        bool overwrite = true)
    {
        WriteProgressOnlyToCurrent(fileSystem, SaveStorageGatewayFactory.CreateIoGateway(fileSystem),
            saveRootPath,
            progressNode, progressStateMachinesNode, overwrite);
    }

    public static void WriteProgressOnlyToCurrent(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        DataSourceNode progressNode,
        DataSourceNode progressStateMachinesNode,
        bool overwrite = true)
    {
        WriteProgressOnlyToCurrent(fileSystem, dataSourceIo, saveRootPath, progressNode, progressStateMachinesNode,
            new DefaultSavePathPolicy(), overwrite);
    }

    public static void WriteProgressOnlyToCurrent(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        DataSourceNode progressNode,
        DataSourceNode progressStateMachinesNode,
        ISavePathPolicy pathPolicy,
        bool overwrite = true)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(dataSourceIo);
        ArgumentNullException.ThrowIfNull(pathPolicy);
        SaveStorageGatewayFactory.ValidateRootPath(saveRootPath, nameof(saveRootPath),
            "Save root path cannot be null or whitespace.");
        ValidateStrictProgressPayload(progressNode, progressStateMachinesNode);

        var currentRel = pathPolicy.GetCurrentDirectory();
        var currentAbs = fileSystem.CombinePath(saveRootPath, currentRel);
        fileSystem.CreateDirectory(currentAbs);

        var progressRel = pathPolicy.GetProgressFile(currentRel);
        var progressAbs = fileSystem.CombinePath(saveRootPath, progressRel);
        SavePathResolver.EnsureParentDirectory(fileSystem, progressAbs);
        dataSourceIo.WriteTree(progressAbs, progressNode, overwrite);

        var progressSmRel = pathPolicy.GetProgressStateMachinesFile(currentRel);
        var progressSmAbs = fileSystem.CombinePath(saveRootPath, progressSmRel);
        SavePathResolver.EnsureParentDirectory(fileSystem, progressSmAbs);
        dataSourceIo.WriteTree(progressSmAbs, progressStateMachinesNode, overwrite);
    }

    public static void WriteToCurrent(
        IFileSystem fileSystem,
        string saveRootPath,
        SaveGamePayload payload)
    {
        WriteToCurrent(fileSystem, SaveStorageGatewayFactory.CreateIoGateway(fileSystem), saveRootPath, payload,
            new DefaultSavePathPolicy());
    }

    public static void WriteToCurrent(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        SaveGamePayload payload) =>
        WriteToCurrent(fileSystem, dataSourceIo, saveRootPath, payload, new DefaultSavePathPolicy());

    public static void WriteToCurrent(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        SaveGamePayload payload,
        ISavePathPolicy pathPolicy)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(dataSourceIo);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(pathPolicy);
        SaveStorageGatewayFactory.ValidateRootPath(saveRootPath, nameof(saveRootPath),
            "Save root path cannot be null or whitespace.");

        ValidateStrictProgressPayload(payload.ProgressNode, payload.ProgressStateMachinesNode);

        var currentRel = pathPolicy.GetCurrentDirectory();
        var currentAbs = fileSystem.CombinePath(saveRootPath, currentRel);
        fileSystem.CreateDirectory(currentAbs);

        var markerRel = pathPolicy.GetWriteInProgressMarker(currentRel);
        var markerAbs = fileSystem.CombinePath(saveRootPath, markerRel);
        fileSystem.WriteAllText(markerAbs, "", true);

        WriteProgressOnlyToCurrent(
            fileSystem,
            dataSourceIo,
            saveRootPath,
            payload.ProgressNode,
            payload.ProgressStateMachinesNode,
            pathPolicy);

        WriteCustomMetaToCurrent(fileSystem, dataSourceIo, saveRootPath, currentRel, payload.CustomMeta, pathPolicy);

        if (!payload.Levels.TryGetValue(payload.ActiveLevelId, out _))
            throw new InvalidOperationException(
                $"Active level '{payload.ActiveLevelId}' not found in SaveGamePayload.");

        // Write all level payloads (foreground + background sessions).
        foreach (var level in payload.Levels.Values)
            WriteLevelPayload(fileSystem, dataSourceIo, saveRootPath, currentRel, level, true, pathPolicy);

        fileSystem.Delete(markerAbs);
    }

    public static void WriteLevelPayloadOnly(
        IFileSystem fileSystem,
        string saveRootPath,
        string baseDirectoryRel,
        LevelPayload level,
        bool overwrite = true)
    {
        WriteLevelPayloadOnly(fileSystem, SaveStorageGatewayFactory.CreateIoGateway(fileSystem), saveRootPath,
            baseDirectoryRel, level, overwrite);
    }

    public static void WriteLevelPayloadOnly(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string baseDirectoryRel,
        LevelPayload level,
        bool overwrite = true)
    {
        WriteLevelPayload(fileSystem, dataSourceIo, saveRootPath, baseDirectoryRel, level, overwrite,
            new DefaultSavePathPolicy());
    }

    public static void WriteLevelPayloadOnly(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string baseDirectoryRel,
        LevelPayload level,
        ISavePathPolicy pathPolicy,
        bool overwrite = true) =>
        WriteLevelPayload(fileSystem, dataSourceIo, saveRootPath, baseDirectoryRel, level, overwrite, pathPolicy);

    private static void WriteLevelPayload(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string baseDirectoryRel,
        LevelPayload level,
        bool overwrite,
        ISavePathPolicy pathPolicy)
    {
        var levelDirRel = pathPolicy.GetLevelDirectory(baseDirectoryRel, level.LevelId);
        var levelDirAbs = fileSystem.CombinePath(saveRootPath, levelDirRel);
        fileSystem.CreateDirectory(levelDirAbs);

        var sndSceneRel = pathPolicy.GetLevelSndSceneFile(levelDirRel);
        var sessionRel = pathPolicy.GetLevelSessionFile(levelDirRel);

        var sndSceneAbs = fileSystem.CombinePath(saveRootPath, sndSceneRel);
        var sessionAbs = fileSystem.CombinePath(saveRootPath, sessionRel);

        if (level.SndSceneNode.IsNull)
            throw new InvalidOperationException(
                $"Level payload '{level.LevelId}' missing required SndSceneNode (strict mode).");
        if (level.SessionNode.IsNull)
            throw new InvalidOperationException(
                $"Level payload '{level.LevelId}' missing required SessionNode (strict mode).");
        dataSourceIo.WriteTree(sndSceneAbs, level.SndSceneNode, overwrite);
        dataSourceIo.WriteTree(sessionAbs, level.SessionNode, overwrite);

        var sessionSmRel = pathPolicy.GetLevelSessionStateMachinesFile(levelDirRel);
        var sessionSmAbs = fileSystem.CombinePath(saveRootPath, sessionSmRel);
        SavePathResolver.EnsureParentDirectory(fileSystem, sessionSmAbs);
        if (level.SessionStateMachinesNode.IsNull)
            throw new InvalidOperationException(
                $"Level payload '{level.LevelId}' missing required SessionStateMachinesNode (strict mode).");
        dataSourceIo.WriteTree(sessionSmAbs, level.SessionStateMachinesNode, overwrite);
    }

    private static void ValidateStrictProgressPayload(DataSourceNode progressNode,
        DataSourceNode progressStateMachinesNode)
    {
        ArgumentNullException.ThrowIfNull(progressNode);
        ArgumentNullException.ThrowIfNull(progressStateMachinesNode);
        if (progressNode.IsNull)
            throw new InvalidOperationException("Missing required ProgressNode (strict mode).");
        if (progressStateMachinesNode.IsNull)
            throw new InvalidOperationException("Missing required ProgressStateMachinesNode (strict mode).");
    }

    private static void WriteCustomMetaToCurrent(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string currentRel,
        IReadOnlyDictionary<string, string>? customMeta,
        ISavePathPolicy pathPolicy)
    {
        var customMetaRel = pathPolicy.GetCustomMetaFile(currentRel);
        var customMetaAbs = fileSystem.CombinePath(saveRootPath, customMetaRel);
        if (customMeta is not null && customMeta.Count > 0)
        {
            var mapNode = BuildStringMapNode(customMeta);
            SavePathResolver.EnsureParentDirectory(fileSystem, customMetaAbs);
            dataSourceIo.WriteTree(customMetaAbs, mapNode);
        }
        else if (fileSystem.Exists(customMetaAbs))
        {
            fileSystem.Delete(customMetaAbs);
        }
    }

    private static DataSourceNode BuildStringMapNode(IReadOnlyDictionary<string, string> map)
    {
        var root = DataSourceNode.CreateObject();
        foreach (var pair in map)
            root.Add(pair.Key, DataSourceNode.CreateString(pair.Value));
        return root;
    }
}
