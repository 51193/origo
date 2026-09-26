using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Snd;

namespace Origo.Core.Snd.Companions;

/// <summary>Provides system and progress-level blackboard access for <see cref="SndContext" />.</summary>
internal sealed class SndContextBlackboardAccess(SndContext owner) : ISndBlackboardAccess
{
    /// <inheritdoc/>
    public IBlackboard SystemBlackboard => owner._systemRun.SystemBlackboard;
    /// <inheritdoc/>
    public IBlackboard? ProgressBlackboard => owner._progressRun?.ProgressBlackboard;
}
