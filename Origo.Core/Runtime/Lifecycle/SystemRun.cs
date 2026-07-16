using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Save;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     System-level runtime layer, holding <see cref="SystemRuntime" /> and the system blackboard.
///     Responsible for system-level index maintenance (such as the active save slot).
/// </summary>
internal sealed class SystemRun
{
    internal SystemRun(SystemRuntime systemRuntime)
    {
        ArgumentNullException.ThrowIfNull(systemRuntime);
        Runtime = systemRuntime;
        SystemBlackboard = systemRuntime.SystemBlackboard;
    }

    internal SystemRuntime Runtime { get; }

    internal IBlackboard SystemBlackboard { get; }

    internal void SetActiveSaveSlot(string saveId)
    {
        if (string.IsNullOrWhiteSpace(saveId))
            throw new ArgumentException("Save id cannot be null or whitespace.", nameof(saveId));

        SystemBlackboard.SetValue(WellKnownKeys.ActiveSaveId, saveId);
    }
}
