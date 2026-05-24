namespace Origo.Core.Save;

internal static class WellKnownKeys
{
    public const string ActiveSaveId = "origo.active_save_id";
    public const string SessionTopology = "origo.session_topology";

    /// <summary>
    ///     进度黑板键：已挂载的后台会话信息（逗号分隔）。
    ///     格式：<c>mountKey=levelId=syncProcess,mountKey=levelId=syncProcess,...</c>。
    ///     用于存档 / 读档时持久化后台会话信息及其帧更新参与标识。
    /// </summary>
    public const string BackgroundLevelIds = "origo.background_level_ids";
}
