using System;

namespace Origo.Core.Save.Storage;

/// <summary>
///     Provides standard relative path assembly rules for save data.
///     All return values are relative paths measured from the save root
///     directory; the concrete root is determined by the adapter layer or
///     system-level blackboard.
/// </summary>
internal static class SavePathLayout
{
    internal const char PathSeparator = '/';

    /// <summary>The active save directory name constant.</summary>
    public const string CurrentDirectoryName = "current";

    /// <summary>The write-in-progress marker file name constant.</summary>
    public const string WriteInProgressMarkerName = ".write_in_progress";

    /// <summary>The payload SHA digest file name constant.</summary>
    public const string PayloadShaFileName = ".payload.sha";

    /// <summary>The level directory name prefix constant.</summary>
    public const string LevelDirectoryPrefix = "level_";

    /// <summary>The strategy/secondary developer custom data subdirectory name constant.</summary>
    public const string ExtraDirectoryName = "extra";

    /// <summary>
    ///     Gets the relative path of the active save directory (i.e., <c>current</c>).
    /// </summary>
    public static string GetCurrentDirectory() => CurrentDirectoryName;

    /// <summary>
    ///     Gets the relative path of the snapshot directory corresponding to
    ///     a save ID (e.g., <c>save_001</c>).
    /// </summary>
    public static string GetSaveDirectory(string saveId)
    {
        if (string.IsNullOrWhiteSpace(saveId))
            throw new ArgumentException("Save id cannot be null or whitespace.", nameof(saveId));

        return $"save_{saveId}";
    }

    /// <summary>
    ///     Gets the relative path of the Progress blackboard JSON file.
    /// </summary>
    public static string GetProgressFile(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
            throw new ArgumentException("Base directory cannot be null or whitespace.", nameof(baseDirectory));

        return Combine(baseDirectory, "progress.json");
    }

    /// <summary>
    ///     Gets the relative path of the Progress state machine snapshot JSON file.
    /// </summary>
    public static string GetProgressStateMachinesFile(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
            throw new ArgumentException("Base directory cannot be null or whitespace.", nameof(baseDirectory));

        return Combine(baseDirectory, "progress_state_machines.json");
    }

    /// <summary>
    ///     Gets the relative path of the custom metadata file (meta.map).
    /// </summary>
    public static string GetCustomMetaFile(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
            throw new ArgumentException("Base directory cannot be null or whitespace.", nameof(baseDirectory));

        return Combine(baseDirectory, "meta.map");
    }

    /// <summary>
    ///     Gets the relative path of the level save subdirectory for a given
    ///     level ID.
    /// </summary>
    public static string GetLevelDirectory(string baseDirectory, string levelId)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
            throw new ArgumentException("Base directory cannot be null or whitespace.", nameof(baseDirectory));
        if (string.IsNullOrWhiteSpace(levelId))
            throw new ArgumentException("Level id cannot be null or whitespace.", nameof(levelId));

        return Combine(baseDirectory, $"{LevelDirectoryPrefix}{levelId}");
    }

    /// <summary>
    ///     Gets the relative path of the level SND scene JSON file.
    /// </summary>
    public static string GetLevelSndSceneFile(string levelDirectory)
    {
        if (string.IsNullOrWhiteSpace(levelDirectory))
            throw new ArgumentException("Level directory cannot be null or whitespace.", nameof(levelDirectory));

        return Combine(levelDirectory, "snd_scene.json");
    }

    /// <summary>
    ///     Gets the relative path of the level Session blackboard JSON file.
    /// </summary>
    public static string GetLevelSessionFile(string levelDirectory)
    {
        if (string.IsNullOrWhiteSpace(levelDirectory))
            throw new ArgumentException("Level directory cannot be null or whitespace.", nameof(levelDirectory));

        return Combine(levelDirectory, "session.json");
    }

    /// <summary>
    ///     Gets the relative path of the level Session state machine snapshot
    ///     JSON file.
    /// </summary>
    public static string GetLevelSessionStateMachinesFile(string levelDirectory)
    {
        if (string.IsNullOrWhiteSpace(levelDirectory))
            throw new ArgumentException("Level directory cannot be null or whitespace.", nameof(levelDirectory));

        return Combine(levelDirectory, "session_state_machines.json");
    }

    /// <summary>
    ///     Gets the relative path of the write-in-progress marker file.
    /// </summary>
    public static string GetWriteInProgressMarker(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
            throw new ArgumentException("Base directory cannot be null or whitespace.", nameof(baseDirectory));

        return Combine(baseDirectory, WriteInProgressMarkerName);
    }

    /// <summary>
    ///     Gets the relative path of the payload SHA digest file.
    /// </summary>
    public static string GetPayloadShaFile(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
            throw new ArgumentException("Base directory cannot be null or whitespace.", nameof(baseDirectory));

        return Combine(baseDirectory, PayloadShaFileName);
    }

    /// <summary>
    ///     Gets the relative path of the strategy/secondary developer custom
    ///     data subdirectory.
    /// </summary>
    public static string GetExtraDirectory(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
            throw new ArgumentException("Base directory cannot be null or whitespace.", nameof(baseDirectory));

        return Combine(baseDirectory, ExtraDirectoryName);
    }

    internal static string Combine(string left, string right)
    {
        if (string.IsNullOrEmpty(left))
            return right;
        if (string.IsNullOrEmpty(right))
            return left;
        return $"{left}{PathSeparator}{right}";
    }
}
