using System;
using System.Collections.Generic;
using System.Globalization;
using Origo.Core.Abstractions.Snd;
using Origo.Core.Save;
using Origo.Core.Save.Meta;
using Origo.Core.Save.Storage;

namespace Origo.Core.Snd.Companions;

/// <summary>
///     Save game operations (list, load, save, auto-save, continue target,
///     level switch) for <see cref="SndContext" />. All save/load operations
///     are dispatched via deferred actions.
/// </summary>
internal sealed class SndContextSaveOperations(SndContext owner) : ISndSaveOperations
{
    /// <inheritdoc/>
    public void RegisterSaveMetaContributor(ISaveMetaContributor contributor)
    {
        ArgumentNullException.ThrowIfNull(contributor);
        owner._saveMetaContributors.Add(contributor);
    }

    /// <inheritdoc/>
    public void RegisterSaveMetaContributor(
        Func<SaveMetaBuildContext, IReadOnlyDictionary<string, string>> contribute)
    {
        ArgumentNullException.ThrowIfNull(contribute);
        owner._saveMetaContributors.Add(new DelegateSaveMetaContributor(contribute));
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> ListSaves() => owner.StorageService.EnumerateSaveIds();

    /// <inheritdoc/>
    public IReadOnlyList<SaveMetaDataEntry> ListSavesWithMetaData() =>
        owner.StorageService.EnumerateSavesWithMetaData();

    /// <inheritdoc/>
    public void RequestLoadGame(string saveId)
    {
        SavePathLayout.ValidateSaveId(saveId, nameof(saveId));

        owner.EnqueueTrackedSystemDeferred(() =>
            owner.SetProgressRun(owner.LoadOrContinueStrict(saveId)));
    }

    /// <inheritdoc/>
    public void RequestSaveGame(string newSaveId)
    {
        SavePathLayout.ValidateSaveId(newSaveId, nameof(newSaveId));

        owner.EnqueueTrackedSystemDeferred(() =>
        {
            owner.BeginWorkflow();
            try
            {
                var progressRun = owner.EnsureProgressRun();
                var metaContext = progressRun.BuildSaveMetaContext(newSaveId);
                var mergedMeta = SaveMetaMerger.Merge(
                    owner._saveMetaContributors, in metaContext);
                var payload = progressRun.BuildSavePayload(newSaveId, mergedMeta);
                owner.StorageService.WriteSavePayloadToCurrentThenSnapshot(
                    payload, newSaveId, owner.Runtime.Logger);
                progressRun.SetSaveId(newSaveId);
                owner._systemRun.SetActiveSaveSlot(newSaveId);
            }
            finally
            {
                owner.EndWorkflow();
            }
        });
    }

    /// <inheritdoc/>
    public string RequestSaveGameAuto(string? newSaveId = null)
    {
        var effectiveNewSaveId = string.IsNullOrWhiteSpace(newSaveId)
            ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                .ToString(CultureInfo.InvariantCulture)
            : newSaveId;
        RequestSaveGame(effectiveNewSaveId);
        return effectiveNewSaveId;
    }

    /// <inheritdoc/>
    public void SetContinueTarget(string saveId)
    {
        SavePathLayout.ValidateSaveId(saveId, nameof(saveId));
        owner._systemRun.SetActiveSaveSlot(saveId);
    }

    /// <inheritdoc/>
    public void RequestSwitchForegroundLevel(string newLevelId)
    {
        if (string.IsNullOrWhiteSpace(newLevelId))
            throw new ArgumentException(
                "New level id cannot be null or whitespace.", nameof(newLevelId));
        SavePathLayout.ValidateToken(newLevelId, nameof(newLevelId), "level id");

        // A level switch persists the foreground state and progress to disk,
        // so it is tracked as a pending persistence request like save/load.
        owner.EnqueueTrackedSystemDeferred(() =>
        {
            owner.EnsureProgressRun().SwitchForeground(newLevelId);
        });
    }
}
