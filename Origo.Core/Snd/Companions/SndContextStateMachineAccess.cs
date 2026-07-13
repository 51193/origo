using Origo.Core.Abstractions.Snd;
using Origo.Core.Abstractions.StateMachine;

namespace Origo.Core.Snd.Companions;

internal sealed class SndContextStateMachineAccess(SndContext owner) : ISndStateMachineAccess
{
    public IStateMachineContainer? GetProgressStateMachines() =>
        owner._progressRun?.GetProgressStateMachines();
}
