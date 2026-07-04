using System.Collections.Generic;
using System.Threading;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;

namespace Origo.Core.Tests;

internal sealed record TestObserverEvent(
    string EventType,
    string? TargetName,
    string? DataKey,
    TypedData? OldValue,
    TypedData? NewValue)
{
    public static TestObserverEvent OnMounted(string targetName) =>
        new("on_mounted", targetName, null, null, null);

    public static TestObserverEvent OnUnmounted(string targetName) =>
        new("on_unmounted", targetName, null, null, null);

    public static TestObserverEvent OnDataChanged(
        string targetName, string dataKey, TypedData oldValue, TypedData newValue) =>
        new("on_data_changed", targetName, dataKey, oldValue, newValue);
}

internal static class EventCollector
{
    private static readonly AsyncLocal<List<TestObserverEvent>?> _events = new();

    public static List<TestObserverEvent>? Events
    {
        get => _events.Value;
        set => _events.Value = value;
    }
}

internal abstract class SharedDataChangeObserverStrategy : ObserverStrategyBase
{
    public override void OnDataChanged(ISndEntity entity, ISndContext ctx,
        ISndEntity target, string dataKey,
        TypedData oldValue, TypedData newValue) =>
        EventCollector.Events?.Add(
            TestObserverEvent.OnDataChanged(target.Name, dataKey, oldValue, newValue));
}
