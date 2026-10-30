using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
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
    private long _lastAutoSaveId;

    private void EnsureNoCaseVariantSlot(string saveId)
    {
        if (owner.StorageService is ICaseVariantSaveIdQuery query
            && query.HasCaseVariantSlot(saveId))
            throw new ArgumentException(
                $"Save id '{saveId}' differs only by case from a legacy slot. " +
                "Rename the legacy slot to the lowercase id before using it; " +
                "mixed-case ids alias the same directory on case-insensitive file systems.",
                nameof(saveId));
    }

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
    public void DeleteSave(string saveId)
    {
        owner.ThrowIfShutdown();
        SavePathLayout.ValidateSaveId(saveId, nameof(saveId));
        EnsureNoCaseVariantSlot(saveId);

        if (owner.IsWorkflowInProgress || owner._pendingPersistenceRequests != 0)
            throw new InvalidOperationException(
                $"Cannot delete save '{saveId}' while a persistence workflow is running or pending.");

        var (found, activeSaveId) = owner._systemRun.SystemBlackboard.TryGet<string>(WellKnownKeys.ActiveSaveId);
        if (found && string.Equals(activeSaveId, saveId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Cannot delete active save '{saveId}'.");

        owner.StorageService.DeleteSave(saveId);
    }

    /// <inheritdoc/>
    public void RequestLoadGame(string saveId)
    {
        owner.ThrowIfShutdown();
        SavePathLayout.ValidateSaveId(saveId, nameof(saveId));
        EnsureNoCaseVariantSlot(saveId);

        owner.EnqueueTrackedSystemDeferred(() =>
            owner.SetProgressRun(owner.LoadOrContinueStrict(saveId)));
    }

    /// <inheritdoc/>
    public void RequestSaveGame(string newSaveId)
    {
        owner.ThrowIfShutdown();
        SavePathLayout.ValidateSaveId(newSaveId, nameof(newSaveId));
        EnsureNoCaseVariantSlot(newSaveId);

        owner.EnqueueTrackedSystemDeferred(() =>
        {
            owner.BeginWorkflow();
            SaveGamePayload? payload = null;
            try
            {
                var progressRun = owner.EnsureProgressRun();
                var metaContext = progressRun.BuildSaveMetaContext(newSaveId);
                var mergedMeta = SaveMetaMerger.Merge(
                    owner._saveMetaContributors, in metaContext);
                payload = progressRun.BuildSavePayload(newSaveId, mergedMeta);
                owner.StorageService.WriteSavePayloadToCurrentThenSnapshot(
                    payload, newSaveId, owner.Runtime.Logger);
                progressRun.SetSaveId(newSaveId);
                owner._systemRun.SetActiveSaveSlot(newSaveId);
            }
            finally
            {
                if (payload is not null)
                    SavePayloadDisposal.Dispose(payload);
                owner.EndWorkflow();
            }
        });
    }

    /// <inheritdoc/>
    public string RequestSaveGameAuto(string? newSaveId = null)
    {
        var effectiveNewSaveId = string.IsNullOrWhiteSpace(newSaveId)
            ? NextAutoSaveId()
            : newSaveId;
        RequestSaveGame(effectiveNewSaveId);
        return effectiveNewSaveId;
    }

    private string NextAutoSaveId()
    {
        // Auto-save ids are unique per operations instance even when save
        // requests are enqueued from different threads (deferred-action
        // enqueueing is documented as thread-safe). A plain read/compare/write
        // could hand two concurrent callers the same millisecond id; the CAS
        // loop keeps the shared counter monotonic.
        var candidate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        while (true)
        {
            var observed = Interlocked.Read(ref _lastAutoSaveId);
            var next = candidate > observed ? candidate : observed + 1;
            if (Interlocked.CompareExchange(ref _lastAutoSaveId, next, observed) == observed)
                return next.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <inheritdoc/>
    public void SetContinueTarget(string saveId)
    {
        owner.ThrowIfShutdown();
        SavePathLayout.ValidateSaveId(saveId, nameof(saveId));
        EnsureNoCaseVariantSlot(saveId);
        owner._systemRun.SetActiveSaveSlot(saveId);
    }

    /// <inheritdoc/>
    public void RequestSwitchForegroundLevel(string newLevelId)
    {
        owner.ThrowIfShutdown();
        if (string.IsNullOrWhiteSpace(newLevelId))
            throw new ArgumentException(
                "New level id cannot be null or whitespace.", nameof(newLevelId));
        SavePathLayout.ValidateLevelId(newLevelId, nameof(newLevelId));

        // A level switch persists the foreground state and progress to disk,
        // so it is tracked as a pending persistence request like save/load.
        // It also runs under the lifecycle-workflow guard: the switch tears
        // down and mounts the foreground session, and a hook firing inside it
        // (persist/quit/AfterLoad) must not tear that workflow down through
        // SndContext.Shutdown.
        owner.EnqueueTrackedSystemDeferred(() =>
        {
            owner.BeginWorkflow();
            try
            {
                owner.EnsureProgressRun().SwitchForeground(newLevelId);
            }
            finally
            {
                owner.EndWorkflow();
            }
        });
    }
}
