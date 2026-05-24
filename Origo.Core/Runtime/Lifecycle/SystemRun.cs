using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Save;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     系统级运行时层，持有 <see cref="SystemRuntime" /> 与系统黑板。
///     负责系统级索引维护（如活动存档槽）。
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

        SystemBlackboard.Set(WellKnownKeys.ActiveSaveId, saveId);
    }
}
