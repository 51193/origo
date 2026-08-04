using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Origo.Core.Abstractions.Logging;
using Origo.Core.DataSource;
using Origo.Core.Logging;

namespace Origo.Core.Save.Storage;

/// <summary>
///     Reads save game payloads from the <c>current/</c> directory or a save
///     snapshot. Detects write-in-progress markers and validates required files.
/// </summary>
internal static class SavePayloadReader
{
    public static SaveGamePayload ReadFromCurrent(
        SaveFileHandle handle,
        string saveId,
        string activeLevelId,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(handle);
        var baseRel = handle.PathPolicy.GetCurrentDirectory();

        var markerRel = handle.PathPolicy.GetWriteInProgressMarker(baseRel);
        var markerAbs = handle.GetAbsolutePath(markerRel);
        if (handle.MetaAccess.FileExists(markerAbs))
        {
            var ex = new InvalidOperationException(
                $"Detected write-in-progress marker at '{markerRel}' in current/; interrupted save write must be handled before loading.");
            (logger ?? NullLogger.Instance).Log(
                LogLevel.Error,
                nameof(SavePayloadReader),
                ex.Message);
            throw ex;
        }

        var progressRel = handle.PathPolicy.GetProgressFile(baseRel);
        var progressSmRel = handle.PathPolicy.GetProgressStateMachinesFile(baseRel);
        var (progressNode, progressStateMachinesNode, customMeta) = ReadProgressAndCustomMeta(
            handle,
            baseRel,
            $"Missing required progress.json in current (path='{progressRel}').",
            $"Missing required progress_state_machines.json in current (path='{progressSmRel}').");

        ValidateFormatVersion(baseRel, customMeta);

        var levels = CreateLevelPayloadMap(handle, baseRel, activeLevelId);
        ReadRemainingLevelPayloads(handle, baseRel, levels);

        return CreateSavePayload(
            saveId,
            activeLevelId,
            progressNode,
            progressStateMachinesNode,
            StripFrameworkMetaKeys(customMeta),
            levels);
    }

    public static SaveGamePayload ReadFromSnapshot(
        SaveFileHandle handle,
        string saveId,
        string activeLevelId)
    {
        ArgumentNullException.ThrowIfNull(handle);
        var baseRel = handle.PathPolicy.GetSaveDirectory(saveId);
        var progressRel = handle.PathPolicy.GetProgressFile(baseRel);
        var progressSmRel = handle.PathPolicy.GetProgressStateMachinesFile(baseRel);
        var (progressNode, progressStateMachinesNode, customMeta) = ReadProgressAndCustomMeta(
            handle,
            baseRel,
            $"Missing required progress.json in save '{saveId}' (path='{progressRel}').",
            $"Missing required progress_state_machines.json in save '{saveId}' (path='{progressSmRel}').");

        ValidateFormatVersion(baseRel, customMeta);

        var levels = CreateLevelPayloadMap(handle, baseRel, activeLevelId);
        ReadRemainingLevelPayloads(handle, baseRel, levels);

        return CreateSavePayload(
            saveId,
            activeLevelId,
            progressNode,
            progressStateMachinesNode,
            StripFrameworkMetaKeys(customMeta),
            levels);
    }

    /// <summary>
    ///     Rejects saves whose stored format version exceeds the current
    ///     framework version (a future format cannot be safely parsed).
    ///     A missing version key is treated as version 1 (the initial format).
    /// </summary>
    private static void ValidateFormatVersion(string baseRel, IReadOnlyDictionary<string, string>? customMeta)
    {
        var storedVersion = SaveGamePayload.CurrentFormatVersion;
        if (customMeta is not null
            && customMeta.TryGetValue(SaveGamePayload.FormatVersionMetaKey, out var raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            storedVersion = parsed;
        }

        if (storedVersion > SaveGamePayload.CurrentFormatVersion)
            throw new InvalidOperationException(
                $"Save at '{baseRel}' uses format version {storedVersion}, but the current " +
                $"framework only supports up to version {SaveGamePayload.CurrentFormatVersion}. " +
                "The save was written by a newer Origo version and cannot be loaded.");
    }

    public static DataSourceNode? ReadProgressNodeFromSnapshot(
        SaveFileHandle handle,
        string saveId)
    {
        ArgumentNullException.ThrowIfNull(handle);

        var saveRel = handle.PathPolicy.GetSaveDirectory(saveId);
        var progressRel = handle.PathPolicy.GetProgressFile(saveRel);
        var progressAbs = handle.GetAbsolutePath(progressRel);

        return handle.MetaAccess.FileExists(progressAbs) ? handle.IoGateway.ReadTree(progressAbs) : null;
    }

    public static LevelPayload? TryReadLevelPayloadFromCurrent(
        SaveFileHandle handle,
        string levelId)
    {
        ArgumentNullException.ThrowIfNull(handle);

        var currentRel = handle.PathPolicy.GetCurrentDirectory();
        ThrowIfWriteInProgressMarkerExists(handle, currentRel);
        return TryReadLevelPayload(handle, currentRel, levelId);
    }

    public static LevelPayload? TryReadLevelPayloadFromSnapshot(
        SaveFileHandle handle,
        string saveId,
        string levelId)
    {
        ArgumentNullException.ThrowIfNull(handle);

        var saveRel = handle.PathPolicy.GetSaveDirectory(saveId);
        return TryReadLevelPayload(handle, saveRel, levelId);
    }

    internal static IReadOnlyDictionary<string, string>? TryReadStringMap(
        SaveFileHandle handle,
        string mapFileAbs)
    {
        if (!handle.MetaAccess.FileExists(mapFileAbs))
            return null;

        using var root = handle.IoGateway.ReadTree(mapFileAbs);
        if (root.Kind != DataSourceNodeKind.Map)
            throw new InvalidOperationException(
                $"Expected map file '{mapFileAbs}' to decode as object node.");

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in root.Keys)
        {
            var valueNode = root[key];
            if (valueNode.Kind is DataSourceNodeKind.Map or DataSourceNodeKind.Array)
                throw new InvalidOperationException(
                    $"Map file '{mapFileAbs}' key '{key}' must be scalar.");
            result[key] = valueNode.AsString();
        }

        return result;
    }

    private static Dictionary<string, LevelPayload> CreateLevelPayloadMap(
        SaveFileHandle handle,
        string baseRel,
        string activeLevelId)
    {
        var level = ReadLevelPayload(handle, baseRel, activeLevelId);
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

    private static (DataSourceNode ProgressNode, DataSourceNode ProgressStateMachinesNode,
        IReadOnlyDictionary<string, string>?
        CustomMeta)
        ReadProgressAndCustomMeta(
            SaveFileHandle handle,
            string baseDirectoryRel,
            string missingProgressMessage,
            string missingProgressStateMachinesMessage)
    {
        var progressRel = handle.PathPolicy.GetProgressFile(baseDirectoryRel);
        var progressAbs = handle.GetAbsolutePath(progressRel);
        if (!handle.MetaAccess.FileExists(progressAbs))
            throw new InvalidOperationException(missingProgressMessage);
        var progressNode = handle.IoGateway.ReadTree(progressAbs);

        var progressSmRel = handle.PathPolicy.GetProgressStateMachinesFile(baseDirectoryRel);
        var progressSmAbs = handle.GetAbsolutePath(progressSmRel);
        if (!handle.MetaAccess.FileExists(progressSmAbs))
            throw new InvalidOperationException(missingProgressStateMachinesMessage);
        var progressStateMachinesNode = handle.IoGateway.ReadTree(progressSmAbs);

        var customMetaRel = handle.PathPolicy.GetCustomMetaFile(baseDirectoryRel);
        var customMetaAbs = handle.GetAbsolutePath(customMetaRel);
        var customMeta = TryReadStringMap(handle, customMetaAbs);

        return (progressNode, progressStateMachinesNode, customMeta);
    }

    /// <summary>
    ///     Removes framework-reserved meta keys (the <c>origo.</c> namespace,
    ///     e.g. the format version) so exposed <c>CustomMeta</c> only carries
    ///     user meta.
    /// </summary>
    private static IReadOnlyDictionary<string, string>? StripFrameworkMetaKeys(
        IReadOnlyDictionary<string, string>? customMeta)
    {
        if (customMeta is null
            || !customMeta.Keys.Any(k => k.StartsWith("origo.", StringComparison.Ordinal)))
            return customMeta;

        return customMeta
            .Where(kv => !kv.Key.StartsWith("origo.", StringComparison.Ordinal))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
    }

    private static LevelPayload? TryReadLevelPayload(
        SaveFileHandle handle,
        string baseDirectoryRel,
        string levelId)
    {
        var files = LevelFiles.Create(handle, baseDirectoryRel, levelId);
        if (files.AllMissing)
            return null;
        ValidateRequiredLevelFiles(files);

        return new LevelPayload
        {
            LevelId = levelId,
            SndSceneNode = handle.IoGateway.ReadTree(files.SndScene.AbsolutePath),
            SessionNode = handle.IoGateway.ReadTree(files.Session.AbsolutePath),
            SessionStateMachinesNode = handle.IoGateway.ReadTree(files.SessionStateMachines.AbsolutePath)
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
        SaveFileHandle handle,
        string baseDirectoryRel,
        string levelId)
    {
        return TryReadLevelPayload(handle, baseDirectoryRel, levelId)
               ?? throw new InvalidOperationException($"Missing level '{levelId}'.");
    }

    private static void ThrowIfWriteInProgressMarkerExists(
        SaveFileHandle handle,
        string baseRel)
    {
        var markerRel = handle.PathPolicy.GetWriteInProgressMarker(baseRel);
        var markerAbs = handle.GetAbsolutePath(markerRel);
        if (handle.MetaAccess.FileExists(markerAbs))
            throw new InvalidOperationException(
                $"Detected write-in-progress marker at '{markerRel}' in current/; interrupted save write must be handled before loading.");
    }

    private static void ReadRemainingLevelPayloads(
        SaveFileHandle handle,
        string baseDirectoryRel,
        Dictionary<string, LevelPayload> levels)
    {
        var baseAbs = handle.GetAbsolutePath(baseDirectoryRel);
        if (!handle.MetaAccess.DirectoryExists(baseAbs))
            return;

        foreach (var dirAbs in handle.MetaAccess.EnumerateDirectories(baseAbs))
        {
            var levelId = TryExtractLevelId(dirAbs);
            if (levelId is null || levels.ContainsKey(levelId))
                continue;

            var levelPayload = TryReadLevelPayload(handle, baseDirectoryRel, levelId);
            if (levelPayload is not null)
                levels[levelId] = levelPayload;
        }
    }

    private static string? TryExtractLevelId(string directoryPath)
    {
        var leaf = SaveFileHandle.GetLeafDirectoryName(directoryPath);
        if (!leaf.StartsWith(SavePathLayout.LevelDirectoryPrefix, StringComparison.Ordinal))
            return null;

        var levelId = leaf[SavePathLayout.LevelDirectoryPrefix.Length..];
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
            SaveFileHandle handle,
            string baseDirectoryRel,
            string levelId)
        {
            var pathPolicy = handle.PathPolicy;
            var levelDirRel = pathPolicy.GetLevelDirectory(baseDirectoryRel, levelId);
            var sndSceneRel = pathPolicy.GetLevelSndSceneFile(levelDirRel);
            var sessionRel = pathPolicy.GetLevelSessionFile(levelDirRel);
            var sessionStateMachinesRel = pathPolicy.GetLevelSessionStateMachinesFile(levelDirRel);

            var sndSceneAbs = handle.GetAbsolutePath(sndSceneRel);
            var sessionAbs = handle.GetAbsolutePath(sessionRel);
            var sessionStateMachinesAbs = handle.GetAbsolutePath(sessionStateMachinesRel);

            return new LevelFiles(
                levelId,
                CreateLevelFile(handle.MetaAccess, sndSceneRel, sndSceneAbs),
                CreateLevelFile(handle.MetaAccess, sessionRel, sessionAbs),
                CreateLevelFile(handle.MetaAccess, sessionStateMachinesRel, sessionStateMachinesAbs));
        }

        private static LevelFile CreateLevelFile(IFileMetaAccess metaAccess, string relativePath, string absolutePath) =>
            new(relativePath, absolutePath, metaAccess.FileExists(absolutePath));
    }

    private sealed record LevelFile(string RelativePath, string AbsolutePath, bool Exists);
}
