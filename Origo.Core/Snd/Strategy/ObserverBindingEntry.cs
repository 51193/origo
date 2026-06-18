using System.Collections.Generic;

namespace Origo.Core.Snd.Strategy;

internal sealed class ObserverBindingEntry
{
    public required string TargetName { get; init; }
    public required string ObserverIndex { get; init; }
    public required ObserverStrategyBase Strategy { get; init; }
    public required IReadOnlyCollection<string> DataKeys { get; init; }
    public Dictionary<string, System.Action<Origo.Core.Abstractions.Entity.ISndEntity,
        Origo.Core.Snd.Metadata.TypedData, Origo.Core.Snd.Metadata.TypedData>> DataWrappers { get; } = new();
}
