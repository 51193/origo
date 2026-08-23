using System;
using Origo.Core.Abstractions.Snd;
using Origo.Core.Save;

namespace Origo.Core.Snd.Companions;

/// <summary>Lifecycle entry points (continue game, initial save, main menu) for <see cref="SndContext" />.</summary>
internal sealed class SndContextLifecycleOperations(SndContext owner) : ISndLifecycleOperations
{
    /// <inheritdoc/>
    public bool HasContinueData() => TryGetExistingContinueSaveId(out _);

    /// <inheritdoc/>
    public bool RequestContinueGame()
    {
        if (!TryGetExistingContinueSaveId(out var saveId))
            return false;

        owner.EnqueueTrackedSystemDeferred(
            () => { owner.SetProgressRun(owner.LoadOrContinueStrict(saveId)); });
        return true;
    }

    private bool TryGetExistingContinueSaveId(out string saveId)
    {
        var (found, candidate) = owner._systemRun.SystemBlackboard.TryGet<string>(WellKnownKeys.ActiveSaveId);
        if (!found || string.IsNullOrWhiteSpace(candidate))
        {
            saveId = string.Empty;
            return false;
        }

        foreach (var existingSaveId in owner.StorageService.EnumerateSaveIds())
        {
            if (!string.Equals(existingSaveId, candidate, StringComparison.Ordinal))
                continue;

            saveId = candidate;
            return true;
        }

        saveId = string.Empty;
        return false;
    }

    /// <inheritdoc/>
    public void RequestLoadInitialSave() =>
        owner.EnqueueTrackedSystemDeferred(owner.ExecuteLoadInitialSaveNow);

    /// <inheritdoc/>
    public void RequestLoadMainMenuEntrySave() =>
        owner.EnqueueTrackedSystemDeferred(owner.ExecuteLoadMainMenuEntrySaveNow);
}
