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
        ThrowIfWriteInProgressMarkerExists(handle, baseRel, logger);
        return ReadFromBase(handle, saveId, activeLevelId, baseRel, "current");
    }

    public static SaveGamePayload ReadFromSnapshot(
        SaveFileHandle handle,
        string saveId,
        string activeLevelId)
    {
        ArgumentNullException.ThrowIfNull(handle);
        var baseRel = handle.PathPolicy.GetSaveDirectory(saveId);
        return ReadFromBase(handle, saveId, activeLevelId, baseRel, $"save '{saveId}'");
    }

    private static SaveGamePayload ReadFromBase(
        SaveFileHandle handle,
        string saveId,
        string activeLevelId,
        string baseRel,
        string location)
    {
        ValidateActiveLevelId(activeLevelId, location);

        var progressRel = handle.PathPolicy.GetProgressFile(baseRel);
        var progressSmRel = handle.PathPolicy.GetProgressStateMachinesFile(baseRel);
        DataSourceNode? progressNode = null;
        DataSourceNode? progressStateMachinesNode = null;
        Dictionary<string, LevelPayload>? levels = null;
        try
        {
            var (progress, progressSm, customMeta) = ReadProgressAndCustomMeta(
                handle,
                baseRel,
                $"Missing required progress.json in {location} (path='{progressRel}').",
                $"Missing required progress_state_machines.json in {location} (path='{progressSmRel}').");
            progressNode = progress;
            progressStateMachinesNode = progressSm;

            ValidateFormatVersion(baseRel, customMeta);

            levels = CreateLevelPayloadMap(handle, baseRel, activeLevelId);
            ReadRemainingLevelPayloads(handle, baseRel, levels, location);

            return CreateSavePayload(
                saveId,
                activeLevelId,
                progress,
                progressSm,
                StripFrameworkMetaKeys(customMeta),
                levels);
        }
        catch
        {
            // No payload reaches the caller on a failed strict read; release
            // every node tree read before the failure.
            progressNode?.Dispose();
            progressStateMachinesNode?.Dispose();
            if (levels is not null)
                foreach (var level in levels.Values)
                    SavePayloadDisposal.Dispose(level);
            throw;
        }
    }

    /// <summary>
    ///     Rejects saves whose stored format version is below the initial
    ///     format or exceeds the current framework version. A missing version
    ///     key is treated as version 1 (the initial format); a
    ///     present-but-unparseable value is corrupted data and fails the
    ///     strict read. Explicit 0/negative values are not a supported older
    ///     form: a no-metadata v1 save may omit the key, but once the writer
    ///     emits any explicit version it emits the current supported version;
    ///     explicit 0/negative values are corrupted data.
    /// </summary>
    private static void ValidateFormatVersion(string baseRel, IReadOnlyDictionary<string, string>? customMeta)
    {
        // A missing key means the initial format (version 1), not "whatever
        // the current framework version happens to be"; the latter would
        // silently reinterpret a version-1 save after a future format bump.
        var storedVersion = 1;
        if (customMeta is not null
            && customMeta.TryGetValue(SaveGamePayload.FormatVersionMetaKey, out var raw))
        {
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out storedVersion))
                throw new InvalidOperationException(
                    $"Save at '{baseRel}' carries an invalid format version '{raw}': " +
                    "the value is not a valid integer, so the save format cannot be trusted. " +
                    "The save data is corrupted or was written by an incompatible version.");
        }

        if (storedVersion < 1)
            throw new InvalidOperationException(
                $"Save at '{baseRel}' uses format version {storedVersion}, but the initial " +
                "supported format version is 1. A lower value is corrupted data; older saves " +
                "omit the version key and are read as version 1.");

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
        ThrowIfWriteInProgressMarkerExists(handle, currentRel, null);
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

    /// <summary>
    ///     Reads an optional scalar string map file (such as <c>meta.map</c>).
    ///     Returns null only when the file does not exist; an existing file that
    ///     is not a map, has a null/collection value, or cannot be decoded is
    ///     corrupted data and fails the read instead of being reported as
    ///     "absent".
    /// </summary>
    internal static IReadOnlyDictionary<string, string>? ReadOptionalStringMap(
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
            if (valueNode.IsNull)
                throw new InvalidOperationException(
                    $"Map file '{mapFileAbs}' key '{key}' must not be null: " +
                    "a null value cannot be represented as a string without " +
                    "silently drifting to an empty string.");
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
        DataSourceNode? progressStateMachinesNode = null;
        try
        {
            var progressSmRel = handle.PathPolicy.GetProgressStateMachinesFile(baseDirectoryRel);
            var progressSmAbs = handle.GetAbsolutePath(progressSmRel);
            if (!handle.MetaAccess.FileExists(progressSmAbs))
                throw new InvalidOperationException(missingProgressStateMachinesMessage);
            progressStateMachinesNode = handle.IoGateway.ReadTree(progressSmAbs);

            var customMetaRel = handle.PathPolicy.GetCustomMetaFile(baseDirectoryRel);
            var customMetaAbs = handle.GetAbsolutePath(customMetaRel);
            var customMeta = ReadOptionalStringMap(handle, customMetaAbs);

            return (progressNode, progressStateMachinesNode, customMeta);
        }
        catch
        {
            progressNode.Dispose();
            progressStateMachinesNode?.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Removes framework-reserved meta keys (the <c>origo.</c> namespace,
    ///     e.g. the format version) so exposed <c>CustomMeta</c> only carries
    ///     user meta.
    /// </summary>
    internal static IReadOnlyDictionary<string, string>? StripFrameworkMetaKeys(
        IReadOnlyDictionary<string, string>? customMeta)
    {
        if (customMeta is null
            || !customMeta.Keys.Any(k => k.StartsWith(WellKnownKeys.FrameworkMetaKeyPrefix, StringComparison.Ordinal)))
            return customMeta;

        var userMeta = customMeta
            .Where(kv => !kv.Key.StartsWith(WellKnownKeys.FrameworkMetaKeyPrefix, StringComparison.Ordinal))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        return userMeta.Count == 0 ? null : userMeta;
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

        DataSourceNode? sceneNode = null;
        DataSourceNode? sessionNode = null;
        DataSourceNode? stateMachinesNode = null;
        try
        {
            sceneNode = handle.IoGateway.ReadTree(files.SndScene.AbsolutePath);
            sessionNode = handle.IoGateway.ReadTree(files.Session.AbsolutePath);
            stateMachinesNode = handle.IoGateway.ReadTree(files.SessionStateMachines.AbsolutePath);

            return new LevelPayload
            {
                LevelId = levelId,
                SndSceneNode = sceneNode,
                SessionNode = sessionNode,
                SessionStateMachinesNode = stateMachinesNode
            };
        }
        catch
        {
            sceneNode?.Dispose();
            sessionNode?.Dispose();
            stateMachinesNode?.Dispose();
            throw;
        }
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
        string baseRel,
        ILogger? logger)
    {
        var markerRel = handle.PathPolicy.GetWriteInProgressMarker(baseRel);
        var markerAbs = handle.GetAbsolutePath(markerRel);
        if (!handle.MetaAccess.FileExists(markerAbs))
            return;

        var ex = new InvalidOperationException(
            $"Detected write-in-progress marker at '{markerRel}' in current/; interrupted save write must be handled before loading.");
        (logger ?? NullLogger.Instance).Log(LogLevel.Error, nameof(SavePayloadReader), ex.Message);
        throw ex;
    }

    private static void ReadRemainingLevelPayloads(
        SaveFileHandle handle,
        string baseDirectoryRel,
        Dictionary<string, LevelPayload> levels,
        string location)
    {
        var baseAbs = handle.GetAbsolutePath(baseDirectoryRel);
        if (!handle.MetaAccess.DirectoryExists(baseAbs))
            return;

        // Discover first, validate the directory identities, then read: an
        // invalid legacy directory name would otherwise surface as a parameter
        // ArgumentException from path assembly instead of an actionable
        // migration error, and reading valid siblings first would allocate node
        // trees for a save that cannot be returned.
        var levelDirectories = new List<string>();
        var invalidDirectories = new List<string>();
        foreach (var dirAbs in handle.MetaAccess.EnumerateDirectories(baseAbs))
        {
            var levelId = handle.PathPolicy.TryGetLevelIdFromDirectory(dirAbs);
            if (levelId is null || levels.ContainsKey(levelId))
                continue;

            if (!SavePathLayout.IsValidLevelId(levelId))
            {
                invalidDirectories.Add(SaveFileHandle.GetLeafDirectoryName(dirAbs));
                continue;
            }

            levelDirectories.Add(levelId);
        }

        if (invalidDirectories.Count > 0)
            throw CreateLegacyLevelDirectoryException(location, invalidDirectories);

        foreach (var levelId in levelDirectories)
        {
            var levelPayload = TryReadLevelPayload(handle, baseDirectoryRel, levelId);
            if (levelPayload is not null)
                levels[levelId] = levelPayload;
        }
    }

    /// <summary>
    ///     Rejects an active level id that is not a valid level identity before
    ///     it is embedded into a directory path. Such an id only comes from
    ///     legacy save data (mixed case, forbidden characters, trailing period);
    ///     the error tells the user how to migrate it instead of exposing the
    ///     path-assembly parameter validation.
    /// </summary>
    private static void ValidateActiveLevelId(string activeLevelId, string location)
    {
        if (string.IsNullOrWhiteSpace(activeLevelId) || SavePathLayout.IsValidLevelId(activeLevelId))
            return;

        throw new InvalidOperationException(
            $"Save at {location} references active level id '{activeLevelId}', which is not a valid " +
            $"level identity. Level ids must use lowercase ASCII letters, digits, '.', '_' and '-', " +
            "must not be dot-only, and must not end with '.'. Legacy level data must be migrated: " +
            $"rename the 'level_{activeLevelId}' directory to the lowercase/path-safe id and update " +
            "the persisted session topology to match, then retry the load.");
    }

    /// <summary>
    ///     Builds the strict-read failure for legacy level directories whose
    ///     names do not satisfy the level identity rule. The writer intentionally
    ///     preserves such directories for an explicit migration, so the reader
    ///     must name them and instruct the rename instead of failing with an
    ///     opaque path-validation error.
    /// </summary>
    private static InvalidOperationException CreateLegacyLevelDirectoryException(
        string location, List<string> directoryNames)
    {
        var noun = directoryNames.Count == 1 ? "directory" : "directories";
        var names = string.Join("', '", directoryNames);
        return new InvalidOperationException(
            $"Save at {location} contains level {noun} '{names}' whose level id is not a valid level " +
            "identity. Level ids must use lowercase ASCII letters, digits, '.', '_' and '-', must not " +
            "be dot-only, and must not end with '.'. Legacy level data must be migrated: rename each " +
            "directory to its lowercase/path-safe id (for example 'level_Town' -> 'level_town') and " +
            "update the matching session topology entry, then retry the load.");
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
