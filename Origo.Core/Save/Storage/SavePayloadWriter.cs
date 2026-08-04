using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Origo.Core.DataSource;

namespace Origo.Core.Save.Storage;

/// <summary>
///     Writes save game payloads to the <c>current/</c> directory with a
///     write-in-progress marker for atomicity. Also provides payload content
///     hashing for idempotent write detection.
/// </summary>
internal static class SavePayloadWriter
{
    public static void WriteProgressOnlyToCurrent(
        SaveFileHandle handle,
        DataSourceNode progressNode,
        DataSourceNode progressStateMachinesNode,
        bool overwrite = true)
    {
        ArgumentNullException.ThrowIfNull(handle);
        SaveFileHandle.ValidateRootPath(handle.SaveRootPath, nameof(handle.SaveRootPath),
            "Save root path cannot be null or whitespace.");
        ValidateStrictProgressPayload(progressNode, progressStateMachinesNode);

        var currentRel = handle.PathPolicy.GetCurrentDirectory();
        var currentAbs = handle.GetAbsolutePath(currentRel);
        handle.MetaAccess.CreateDirectory(currentAbs);

        var markerAbs = WriteCheckpointMarker(handle, currentRel);
        // If the write throws, the marker is intentionally left on disk so
        // that readers reject the partially written checkpoint.
        WriteProgressFilesToCurrent(handle, progressNode, progressStateMachinesNode, overwrite);
        handle.MetaAccess.Delete(markerAbs);
    }

    private static void WriteProgressFilesToCurrent(
        SaveFileHandle handle,
        DataSourceNode progressNode,
        DataSourceNode progressStateMachinesNode,
        bool overwrite)
    {
        var currentRel = handle.PathPolicy.GetCurrentDirectory();

        var progressRel = handle.PathPolicy.GetProgressFile(currentRel);
        var progressAbs = handle.GetAbsolutePath(progressRel);
        handle.EnsureParentDirectory(progressRel);
        handle.IoGateway.WriteTree(progressAbs, progressNode, overwrite);

        var progressSmRel = handle.PathPolicy.GetProgressStateMachinesFile(currentRel);
        var progressSmAbs = handle.GetAbsolutePath(progressSmRel);
        handle.EnsureParentDirectory(progressSmRel);
        handle.IoGateway.WriteTree(progressSmAbs, progressStateMachinesNode, overwrite);
    }

    private static string WriteCheckpointMarker(SaveFileHandle handle, string currentRel)
    {
        var markerRel = handle.PathPolicy.GetWriteInProgressMarker(currentRel);
        var markerAbs = handle.GetAbsolutePath(markerRel);
        handle.MetaAccess.CreateDirectory(handle.GetAbsolutePath(currentRel));
        handle.IoGateway.WriteTree(markerAbs, DataSourceNode.CreateString(""));
        return markerAbs;
    }

    public static void WriteToCurrent(
        SaveFileHandle handle,
        SaveGamePayload payload)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(payload);

        SaveFileHandle.ValidateRootPath(handle.SaveRootPath, nameof(handle.SaveRootPath),
            "Save root path cannot be null or whitespace.");
        ValidateStrictProgressPayload(payload.ProgressNode, payload.ProgressStateMachinesNode);
        if (!payload.Levels.TryGetValue(payload.ActiveLevelId, out _))
            throw new InvalidOperationException(
                $"Active level '{payload.ActiveLevelId}' not found in SaveGamePayload.");

        var currentRel = handle.PathPolicy.GetCurrentDirectory();
        var currentAbs = handle.GetAbsolutePath(currentRel);
        handle.MetaAccess.CreateDirectory(currentAbs);

        var markerRel = handle.PathPolicy.GetWriteInProgressMarker(currentRel);
        var markerAbs = handle.GetAbsolutePath(markerRel);
        handle.IoGateway.WriteTree(markerAbs, DataSourceNode.CreateString(""));

        WriteProgressFilesToCurrent(
            handle,
            payload.ProgressNode,
            payload.ProgressStateMachinesNode,
            overwrite: true);

        WriteCustomMetaToCurrent(handle, currentRel, payload.CustomMeta);

        foreach (var level in payload.Levels)
        {
            // The dictionary key is the level id used for the on-disk path;
            // a mismatch between key and LevelId would write to the wrong
            // directory while readers index by directory name.
            if (!string.Equals(level.Key, level.Value.LevelId, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Level payload dictionary key '{level.Key}' does not match LevelId '{level.Value.LevelId}'.");
            WriteLevelPayload(handle, currentRel, level.Value, true);
        }

        handle.MetaAccess.Delete(markerAbs);
    }

    public static void WriteLevelPayloadOnly(
        SaveFileHandle handle,
        string baseDirectoryRel,
        LevelPayload level,
        bool overwrite = true) => WriteLevelPayload(handle, baseDirectoryRel, level, overwrite);

    private static void WriteLevelPayload(
        SaveFileHandle handle,
        string baseDirectoryRel,
        LevelPayload level,
        bool overwrite)
    {
        var levelDirRel = handle.PathPolicy.GetLevelDirectory(baseDirectoryRel, level.LevelId);
        var levelDirAbs = handle.GetAbsolutePath(levelDirRel);
        handle.MetaAccess.CreateDirectory(levelDirAbs);

        var sndSceneRel = handle.PathPolicy.GetLevelSndSceneFile(levelDirRel);
        var sessionRel = handle.PathPolicy.GetLevelSessionFile(levelDirRel);

        var sndSceneAbs = handle.GetAbsolutePath(sndSceneRel);
        var sessionAbs = handle.GetAbsolutePath(sessionRel);

        if (level.SndSceneNode.IsNull)
            throw new InvalidOperationException(
                $"Level payload '{level.LevelId}' missing required SndSceneNode (strict mode).");
        if (level.SessionNode.IsNull)
            throw new InvalidOperationException(
                $"Level payload '{level.LevelId}' missing required SessionNode (strict mode).");
        handle.IoGateway.WriteTree(sndSceneAbs, level.SndSceneNode, overwrite);
        handle.IoGateway.WriteTree(sessionAbs, level.SessionNode, overwrite);

        var sessionSmRel = handle.PathPolicy.GetLevelSessionStateMachinesFile(levelDirRel);
        var sessionSmAbs = handle.GetAbsolutePath(sessionSmRel);
        handle.EnsureParentDirectory(sessionSmRel);
        if (level.SessionStateMachinesNode.IsNull)
            throw new InvalidOperationException(
                $"Level payload '{level.LevelId}' missing required SessionStateMachinesNode (strict mode).");
        handle.IoGateway.WriteTree(sessionSmAbs, level.SessionStateMachinesNode, overwrite);
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
        SaveFileHandle handle,
        string currentRel,
        IReadOnlyDictionary<string, string>? customMeta)
    {
        var customMetaRel = handle.PathPolicy.GetCustomMetaFile(currentRel);
        var customMetaAbs = handle.GetAbsolutePath(customMetaRel);

        if (customMeta is null || customMeta.Count == 0)
        {
            if (handle.MetaAccess.FileExists(customMetaAbs))
                handle.MetaAccess.Delete(customMetaAbs);
            return;
        }

        var mapNode = BuildStringMapNode(customMeta);
        mapNode.Add(SaveGamePayload.FormatVersionMetaKey,
            DataSourceNode.CreateString(SaveGamePayload.CurrentFormatVersion.ToString(
                System.Globalization.CultureInfo.InvariantCulture)));

        handle.EnsureParentDirectory(customMetaRel);
        handle.IoGateway.WriteTree(customMetaAbs, mapNode);
    }

    private static DataSourceNode BuildStringMapNode(IReadOnlyDictionary<string, string>? map)
    {
        var root = DataSourceNode.CreateObject();
        if (map is not null)
            foreach (var pair in map)
                root.Add(pair.Key, DataSourceNode.CreateString(pair.Value));
        return root;
    }

    /// <summary>
    ///     Compute a SHA-256 content digest (lowercase hex) for a
    ///     <see cref="SaveGamePayload" />. Combines node tree hashes,
    ///     CustomMeta key-value pairs, and ordered level payloads.
    ///     Used for write idempotency deduplication.
    /// </summary>
    internal static string ComputePayloadHash(SaveGamePayload payload)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        void Feed(DataSourceNode node)
        {
            sha.AppendData(Encoding.UTF8.GetBytes(node.ComputeSha256Hash()));
        }

        void FeedString(string s)
        {
            sha.AppendData(Encoding.UTF8.GetBytes(s));
        }

        Feed(payload.ProgressNode);
        Feed(payload.ProgressStateMachinesNode);

        if (payload.CustomMeta is not null)
            foreach (var kv in payload.CustomMeta.OrderBy(x => x.Key, StringComparer.Ordinal))
                FeedString($"M:{kv.Key}={kv.Value}");

        foreach (var kv in payload.Levels.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            FeedString($"L:{kv.Key}");
            Feed(kv.Value.SndSceneNode);
            Feed(kv.Value.SessionNode);
            Feed(kv.Value.SessionStateMachinesNode);
        }

        return Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
    }
}
