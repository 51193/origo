namespace Origo.Core.Save.Storage;

/// <summary>
///     存档路径策略接口。提供存档目录和文件路径的拼装规则，
///     使路径布局与业务逻辑解耦，可按环境注入不同路径策略。
/// </summary>
public interface ISavePathPolicy
{
    /// <summary>获取活动存档目录的相对路径。</summary>
    string GetCurrentDirectory();

    /// <summary>根据存档 ID 获取快照目录的相对路径。</summary>
    string GetSaveDirectory(string saveId);

    /// <summary>获取 Progress 黑板 JSON 文件的相对路径。</summary>
    string GetProgressFile(string baseDirectory);

    /// <summary>获取 Progress 状态机快照 JSON 文件的相对路径。</summary>
    string GetProgressStateMachinesFile(string baseDirectory);

    /// <summary>获取自定义元数据文件的相对路径。</summary>
    string GetCustomMetaFile(string baseDirectory);

    /// <summary>获取关卡存档子目录的相对路径。</summary>
    string GetLevelDirectory(string baseDirectory, string levelId);

    /// <summary>获取关卡 SND 场景 JSON 文件的相对路径。</summary>
    string GetLevelSndSceneFile(string levelDirectory);

    /// <summary>获取关卡 Session 黑板 JSON 文件的相对路径。</summary>
    string GetLevelSessionFile(string levelDirectory);

    /// <summary>获取关卡 Session 状态机快照 JSON 文件的相对路径。</summary>
    string GetLevelSessionStateMachinesFile(string levelDirectory);

    /// <summary>获取写入进行中标记文件的相对路径。</summary>
    string GetWriteInProgressMarker(string baseDirectory);
}
