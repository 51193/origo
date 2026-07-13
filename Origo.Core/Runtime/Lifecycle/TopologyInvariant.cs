using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Save;

namespace Origo.Core.Runtime.Lifecycle;

internal static class TopologyInvariant
{
    public static void EnsureActiveLevel(IBlackboard blackboard, string expectedLevelId, string context)
    {
        ArgumentNullException.ThrowIfNull(blackboard);
        if (string.IsNullOrWhiteSpace(expectedLevelId))
            throw new ArgumentException("Expected level ID cannot be null or whitespace.", nameof(expectedLevelId));

        var (found, rawTopology) = blackboard.TryGet<string>(WellKnownKeys.SessionTopology);
        if (!found || string.IsNullOrWhiteSpace(rawTopology))
            throw new InvalidOperationException(
                $"Progress blackboard missing required '{WellKnownKeys.SessionTopology}' ({context}).");

        var topologyActiveLevelId = SessionTopologyCodec.ExtractForegroundLevelId(rawTopology);
        if (!string.Equals(topologyActiveLevelId, expectedLevelId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Progress '{WellKnownKeys.SessionTopology}' foreground ('{topologyActiveLevelId}') " +
                $"does not match expected level ID '{expectedLevelId}' ({context}).");
    }
}
