using System;
using System.Collections.Generic;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd.Entity;

/// <summary>
///     Manages data change subscriptions and notifications for a single SND entity.
///     Serves only as an internal Core implementation detail
///     and is not visible outside the assembly.
/// </summary>
internal sealed class DataObserverManager
{
    private readonly Dictionary<string, List<Subscription>> _subscriptions = [];

    public void Subscribe(string name, Action<TypedData, TypedData> callback,
        Func<TypedData, TypedData, bool>? filter = null)
    {
        if (!_subscriptions.TryGetValue(name, out var list))
        {
            list = [];
            _subscriptions[name] = list;
        }

        list.Add(new Subscription
        {
            Callback = callback,
            Filter = filter
        });
    }

    public void Unsubscribe(string name, Action<TypedData, TypedData> callback)
    {
        if (!_subscriptions.TryGetValue(name, out var list)) return;

        foreach (var subscription in list)
            if (subscription.Callback == callback)
                subscription.Removed = true;

        list.RemoveAll(s => s.Callback == callback);
        if (list.Count == 0) _subscriptions.Remove(name);
    }

    /// <summary>
    ///     Fires all callbacks subscribed to the given key with the old and
    ///     new values, in subscription order. A throwing callback aborts the
    ///     remaining callbacks for the same key while the exception propagates
    ///     (fail-fast); the caller has already committed the new value by the
    ///     time this runs. A subscription removed earlier in the same pass is
    ///     skipped: an unmounted observer binding must not receive a data
    ///     change after its <c>OnUnmounted</c> cleanup ran. Snapshot iteration
    ///     keeps re-entrant subscription changes from corrupting the current
    ///     notification pass.
    /// </summary>
    public void NotifyObservers(string name, TypedData oldValue, TypedData newValue)
    {
        if (!_subscriptions.TryGetValue(name, out var list)) return;

        foreach (var subscription in list.ToArray())
        {
            if (subscription.Removed) continue;
            if (subscription.Filter is not null && !subscription.Filter(oldValue, newValue)) continue;
            subscription.Callback(oldValue, newValue);
        }
    }

    public void Clear()
    {
        foreach (var list in _subscriptions.Values)
            foreach (var subscription in list)
                subscription.Removed = true;

        _subscriptions.Clear();
    }

    private sealed class Subscription
    {
        public required Action<TypedData, TypedData> Callback { get; init; }

        public Func<TypedData, TypedData, bool>? Filter { get; init; }

        /// <summary>
        ///     True once this subscription was removed by <see cref="Unsubscribe" />
        ///     or <see cref="Clear" />. A notification pass that started before the
        ///     removal must not invoke a removed callback.
        /// </summary>
        public bool Removed { get; set; }
    }
}
