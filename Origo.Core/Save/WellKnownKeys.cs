namespace Origo.Core.Save;

internal static class WellKnownKeys
{
    public const string ActiveSaveId = "origo.active_save_id";
    public const string SessionTopology = "origo.session_topology";

    /// <summary>
    ///     Progress blackboard key: information about mounted background
    ///     sessions (comma-separated).
    ///     Format: <c>mountKey=levelId=syncProcess,mountKey=levelId=syncProcess,...</c>.
    ///     Used to persist background session info and its frame-update
    ///     participation flags during save/load.
    /// </summary>
    public const string BackgroundLevelIds = "origo.background_level_ids";
}
