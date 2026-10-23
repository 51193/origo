using System;
using System.Linq;

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
    ///     Validates a save ID against the allowed character set
    ///     (lowercase ASCII letters, digits, <c>.</c>, <c>_</c>, <c>-</c>),
    ///     rejects path-ambiguous dot-only/trailing-dot IDs, and rejects the
    ///     reserved atomic-write suffixes. Save IDs are embedded in directory
    ///     names, so arbitrary characters would allow path traversal or
    ///     directory structure injection, a suffix-ending ID would collide
    ///     with the <c>save_{id}.tmp</c> staging or <c>save_{id}.bak</c>
    ///     backup directory, and lowercase-only IDs keep the ordinal identity
    ///     stable on case-insensitive file systems.
    /// </summary>
    public static void ValidateSaveId(string saveId, string paramName)
    {
        ValidateIdentifier(saveId, paramName, "save id", requireLowercase: true, requirePathSafe: true);
        if (saveId.EndsWith(SaveStorageFacade.SaveTempDirectorySuffix, StringComparison.OrdinalIgnoreCase)
            || saveId.EndsWith(SaveStorageFacade.SaveBackupDirectorySuffix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"'{saveId}' ends with a reserved save-id suffix " +
                $"('{SaveStorageFacade.SaveTempDirectorySuffix}' or " +
                $"'{SaveStorageFacade.SaveBackupDirectorySuffix}'). " +
                "Choose a save id whose name cannot collide with the atomic-write staging or backup directory.",
                paramName);
    }

    /// <summary>
    ///     Validates a level ID embedded in <c>level_{id}</c>. Level IDs use
    ///     the same lowercase, path-safe identity rule as save IDs so a save
    ///     written on one platform resolves to the same directory on
    ///     case-insensitive Windows/macOS file systems.
    /// </summary>
    internal static void ValidateLevelId(string levelId, string paramName) =>
        ValidateIdentifier(levelId, paramName, "level id", requireLowercase: true, requirePathSafe: true);

    /// <summary>
    ///     Shared token validation for identifiers embedded in directory or
    ///     file names. Allows only ASCII letters, digits, <c>.</c>,
    ///     <c>_</c>, and <c>-</c>. The optional rules are opt-in so session
    ///     keys, which are not file-system path components, retain their
    ///     existing token syntax.
    /// </summary>
    internal static void ValidateToken(string value, string paramName, string kind) =>
        ValidateIdentifier(value, paramName, kind, requireLowercase: false, requirePathSafe: false);

    private static void ValidateIdentifier(
        string value,
        string paramName,
        string kind,
        bool requireLowercase,
        bool requirePathSafe)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{kind} cannot be null or whitespace.", paramName);

        foreach (var ch in value)
        {
            if (char.IsAsciiLetterOrDigit(ch) || ch is '.' or '_' or '-')
                continue;
            throw new ArgumentException(
                $"'{value}' contains character '{ch}' which is not allowed in {kind} " +
                "(allowed: ASCII letters, digits, '.', '_', '-').",
                paramName);
        }

        if (requireLowercase && value.Any(char.IsAsciiLetterUpper))
            throw new ArgumentException(
                $"'{value}' must use lowercase ASCII letters in {kind}; " +
                "mixed-case identifiers alias the same directory on case-insensitive file systems " +
                "and cannot be distinguished reliably across platforms.",
                paramName);

        if (requirePathSafe && (value.All(c => c == '.') || value.EndsWith('.')))
            throw new ArgumentException(
                $"'{value}' cannot be a dot-only {kind} or end with '.': " +
                "Windows path normalization strips a trailing period, so the value would alias another directory.",
                paramName);
    }

    /// <summary>
    ///     Gets the relative path of the snapshot directory corresponding to
    ///     a save ID (e.g., <c>save_001</c>).
    /// </summary>
    public static string GetSaveDirectory(string saveId)
    {
        ValidateSaveId(saveId, nameof(saveId));

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

        // Level ids are embedded in directory names (level_<id>); validate the
        // identity like save ids so the token cannot inject path separators,
        // traverse out of the save directory, or alias a same-cased sibling on
        // case-insensitive file systems.
        ValidateLevelId(levelId, nameof(levelId));

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

    /// <summary>
    ///     Removes a path prefix (plus the trailing separator) from a full
    ///     path when it matches; returns the full path unchanged otherwise.
    /// </summary>
    internal static string StripPathPrefix(string fullPath, string prefix)
    {
        var normalizedFull = NormalizeSeparatorsAndScheme(fullPath);
        var normalizedPrefix = NormalizeSeparatorsAndScheme(prefix);
        var prefixToStrip = normalizedPrefix.EndsWith("://", StringComparison.Ordinal)
            ? normalizedPrefix
            : $"{normalizedPrefix.TrimEnd('/')}/";

        return normalizedFull.StartsWith(prefixToStrip, StringComparison.Ordinal)
            ? normalizedFull[prefixToStrip.Length..]
            : normalizedFull;
    }

    private static string NormalizeSeparatorsAndScheme(string path)
    {
        var normalized = path.Replace('\\', '/');
        var schemeIndex = normalized.IndexOf("://", StringComparison.Ordinal);
        if (schemeIndex < 0)
            return normalized;

        var schemeRoot = normalized[..(schemeIndex + 3)];
        var rest = normalized[(schemeIndex + 3)..].TrimStart('/');
        return rest.Length == 0 ? schemeRoot : $"{schemeRoot}{rest}";
    }
}
