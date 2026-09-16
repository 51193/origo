using System.Collections.Generic;
using System.Threading;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;

namespace Origo.TestSupport;

/// <summary>Structured observer event captured by test strategies.</summary>
public sealed record TestObserverEvent(
    string EventType,
    string? TargetName,
    string? DataKey,
    TypedData? OldValue,
    TypedData? NewValue)
{
    /// <summary>Creates an observer-mounted event.</summary>
    public static TestObserverEvent OnMounted(string targetName) =>
        new("on_mounted", targetName, null, null, null);

    /// <summary>Creates an observer-unmounted event.</summary>
    public static TestObserverEvent OnUnmounted(string targetName) =>
        new("on_unmounted", targetName, null, null, null);

    /// <summary>Creates a data-change event with the observed old and new values.</summary>
    public static TestObserverEvent OnDataChanged(
        string targetName, string dataKey, TypedData oldValue, TypedData newValue) =>
        new("on_data_changed", targetName, dataKey, oldValue, newValue);
}

/// <summary>
///     AsyncLocal-backed observer event collector shared by test observer
///     strategies without polluting state across parallel tests.
/// </summary>
public static class EventCollector
{
    private static readonly AsyncLocal<List<TestObserverEvent>?> _events = new();

    /// <summary>Current test-local event list; assign before each test.</summary>
    public static List<TestObserverEvent>? Events
    {
        get => _events.Value;
        set => _events.Value = value;
    }
}

/// <summary>
///     Observer strategy that records data changes into
///     <see cref="EventCollector" />.
/// </summary>
public abstract class SharedDataChangeObserverStrategy : ObserverStrategyBase
{
    /// <inheritdoc/>
    public override void OnDataChanged(ISndEntity entity, ISndContext ctx,
        ISndEntity target, string dataKey,
        TypedData oldValue, TypedData newValue) =>
        EventCollector.Events?.Add(
            TestObserverEvent.OnDataChanged(target.Name, dataKey, oldValue, newValue));
}
