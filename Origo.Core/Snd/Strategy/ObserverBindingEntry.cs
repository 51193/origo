using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd.Entity;

namespace Origo.Core.Snd.Strategy;

internal sealed class ObserverBindingEntry
{
    public required string ObserverName { get; init; }
    public required string TargetName { get; init; }
    public required string ObserverIndex { get; init; }
    public required ObserverStrategyBase Strategy { get; init; }
    public required IReadOnlyCollection<string> DataKeys { get; init; }
    public Dictionary<string, Action<ISndEntity,
        Origo.Core.Snd.Metadata.TypedData, Origo.Core.Snd.Metadata.TypedData>> DataWrappers
    { get; } = [];

    public ISndEntity? TargetEntity { get; init; }

    internal void FullCleanup(ISndEntity entity, ISndContext ctx, SndStrategyPool pool)
    {
        if (TargetEntity is null)
            throw new InvalidOperationException(
                "Observer binding FullCleanup requires a non-null TargetEntity reference.");

        // Mount hard-casts the target to ISndEntityRawSubscription, so every
        // binding that reaches cleanup has a target implementing it.
        var raw = (ISndEntityRawSubscription)TargetEntity;
        Exception? firstFailure = null;

        // Every cleanup step runs independently: a throwing unsubscribe or a
        // throwing OnUnmounted hook must never skip the pool release. The
        // binding has already been removed from the topology by the caller,
        // so a skipped release here is unrecoverable (ReleaseStrategiesOnly
        // can no longer see it) and would leak the pooled strategy instance.
        foreach (var (key, wrapper) in DataWrappers)
        {
            try
            {
                raw.UnsubscribeDataRaw(key, wrapper);
            }
            catch (Exception ex)
            {
                firstFailure ??= ex;
            }
        }

        try
        {
            Strategy.OnUnmounted(entity, ctx, TargetEntity);
        }
        catch (Exception ex)
        {
            firstFailure ??= ex;
        }

        try
        {
            pool.ReleaseStrategy(ObserverIndex);
        }
        catch (Exception releaseEx)
        {
            if (firstFailure is null)
                throw;
            throw new AggregateException(
                "Observer binding cleanup failed and the pool reference could not be released; see inner exceptions.",
                [firstFailure, releaseEx]);
        }

        if (firstFailure is not null)
            ExceptionDispatchInfo.Capture(firstFailure).Throw();
    }
}
