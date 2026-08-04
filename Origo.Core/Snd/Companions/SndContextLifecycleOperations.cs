using Origo.Core.Abstractions.Snd;
using Origo.Core.Save;

namespace Origo.Core.Snd.Companions;

/// <summary>Lifecycle entry points (continue game, initial save, main menu) for <see cref="SndContext" />.</summary>
internal sealed class SndContextLifecycleOperations(SndContext owner) : ISndLifecycleOperations
{
    public bool HasContinueData()
    {
        var (found, saveId) = owner._systemRun.SystemBlackboard.TryGet<string>(WellKnownKeys.ActiveSaveId);
        return found && !string.IsNullOrWhiteSpace(saveId);
    }

    public bool RequestContinueGame()
    {
        var (found, saveId) = owner._systemRun.SystemBlackboard.TryGet<string>(WellKnownKeys.ActiveSaveId);
        if (!found || string.IsNullOrWhiteSpace(saveId))
            return false;

        owner.EnqueueTrackedSystemDeferred(
            () => { owner.SetProgressRun(owner.LoadOrContinueStrict(saveId)); });
        return true;
    }

    public void RequestLoadInitialSave() =>
        owner.EnqueueTrackedSystemDeferred(owner.ExecuteLoadInitialSaveNow);

    public void RequestLoadMainMenuEntrySave() =>
        owner.EnqueueTrackedSystemDeferred(owner.ExecuteLoadMainMenuEntrySaveNow);
}
