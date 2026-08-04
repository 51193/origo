namespace Origo.Core.Save;

internal static class WellKnownKeys
{
    /// <summary>Framework-reserved meta.map key prefix; such keys are framework bookkeeping, not user display metadata.</summary>
    public const string FrameworkMetaKeyPrefix = "origo.";

    public const string ActiveSaveId = "origo.active_save_id";
    public const string SessionTopology = "origo.session_topology";
}
