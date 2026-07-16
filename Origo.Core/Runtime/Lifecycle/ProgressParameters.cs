namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     Configuration parameters required for starting a ProgressRun.
///     <para>
///         Only passes the identifier (SaveId); no pre-constructed runtime objects (such as IBlackboard)
///         are injected. ProgressRun internally creates its own ProgressBlackboard and restores all state
///         (including session topology) from persistent data via <see cref="ProgressRun.LoadFromPayload" />.
///     </para>
/// </summary>
internal readonly record struct ProgressParameters(string SaveId);
