using System;

namespace Origo.Core.Save.Storage;

/// <summary>
///     提供与存档相关的标准相对路径拼装规则。
///     所有返回值均为相对于存档根目录的相对路径，具体根由适配层或系统级黑板决定。
/// </summary>
internal static class SavePathLayout
{
    /// <summary>活动存档目录名称常量。</summary>
    public const string CurrentDirectoryName = "current";

    /// <summary>写入进行中标记文件的名称常量。</summary>
    public const string WriteInProgressMarkerName = ".write_in_progress";

    /// <summary>关卡目录名称前缀常量。</summary>
    public const string LevelDirectoryPrefix = "level_";

    /// <summary>
    ///     获取活动存档目录的相对路径（即 <c>current</c>）。
    /// </summary>
    public static string GetCurrentDirectory() => CurrentDirectoryName;

    /// <summary>
    ///     根据存档 ID 获取对应快照目录的相对路径（如 <c>save_001</c>）。
    /// </summary>
    public static string GetSaveDirectory(string saveId)
    {
        if (string.IsNullOrWhiteSpace(saveId))
            throw new ArgumentException("Save id cannot be null or whitespace.", nameof(saveId));

        return $"save_{saveId}";
    }

    /// <summary>
    ///     获取 Progress 黑板 JSON 文件的相对路径。
    /// </summary>
    public static string GetProgressFile(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
            throw new ArgumentException("Base directory cannot be null or whitespace.", nameof(baseDirectory));

        return Combine(baseDirectory, "progress.json");
    }

    /// <summary>
    ///     获取 Progress 状态机快照 JSON 文件的相对路径。
    /// </summary>
    public static string GetProgressStateMachinesFile(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
            throw new ArgumentException("Base directory cannot be null or whitespace.", nameof(baseDirectory));

        return Combine(baseDirectory, "progress_state_machines.json");
    }

    /// <summary>
    ///     获取自定义元数据文件（meta.map）的相对路径。
    /// </summary>
    public static string GetCustomMetaFile(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
            throw new ArgumentException("Base directory cannot be null or whitespace.", nameof(baseDirectory));

        return Combine(baseDirectory, "meta.map");
    }

    /// <summary>
    ///     根据关卡 ID 获取该关卡存档子目录的相对路径。
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
    ///     获取关卡 SND 场景 JSON 文件的相对路径。
    /// </summary>
    public static string GetLevelSndSceneFile(string levelDirectory)
    {
        if (string.IsNullOrWhiteSpace(levelDirectory))
            throw new ArgumentException("Level directory cannot be null or whitespace.", nameof(levelDirectory));

        return Combine(levelDirectory, "snd_scene.json");
    }

    /// <summary>
    ///     获取关卡 Session 黑板 JSON 文件的相对路径。
    /// </summary>
    public static string GetLevelSessionFile(string levelDirectory)
    {
        if (string.IsNullOrWhiteSpace(levelDirectory))
            throw new ArgumentException("Level directory cannot be null or whitespace.", nameof(levelDirectory));

        return Combine(levelDirectory, "session.json");
    }

    /// <summary>
    ///     获取关卡 Session 状态机快照 JSON 文件的相对路径。
    /// </summary>
    public static string GetLevelSessionStateMachinesFile(string levelDirectory)
    {
        if (string.IsNullOrWhiteSpace(levelDirectory))
            throw new ArgumentException("Level directory cannot be null or whitespace.", nameof(levelDirectory));

        return Combine(levelDirectory, "session_state_machines.json");
    }

    /// <summary>
    ///     获取写入进行中标记文件的相对路径。
    /// </summary>
    public static string GetWriteInProgressMarker(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
            throw new ArgumentException("Base directory cannot be null or whitespace.", nameof(baseDirectory));

        return Combine(baseDirectory, WriteInProgressMarkerName);
    }

    private static string Combine(string left, string right)
    {
        if (string.IsNullOrEmpty(left))
            return right;
        if (string.IsNullOrEmpty(right))
            return left;
        return $"{left}/{right}";
    }
}
