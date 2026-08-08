using System;
using System.Collections.Generic;
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
        foreach (var (key, wrapper) in DataWrappers)
            raw.UnsubscribeDataRaw(key, wrapper);

        Strategy.OnUnmounted(entity, ctx, TargetEntity);

        pool.ReleaseStrategy(ObserverIndex);
    }
}
