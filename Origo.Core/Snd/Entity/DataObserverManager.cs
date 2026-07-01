using System;
using System.Collections.Generic;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd.Entity;

/// <summary>
///     管理针对单个 SND 实体的数据变更订阅与通知。
///     仅作为 Core 内部实现细节，对程序集外不可见。
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

        list.RemoveAll(s => s.Callback == callback);
        if (list.Count == 0) _subscriptions.Remove(name);
    }

    public void NotifyObservers(string name, TypedData oldValue, TypedData newValue)
    {
        if (!_subscriptions.TryGetValue(name, out var list)) return;

        foreach (var subscription in list.ToArray())
        {
            if (subscription.Filter is not null && !subscription.Filter(oldValue, newValue)) continue;
            subscription.Callback(oldValue, newValue);
        }
    }

    public void Clear() => _subscriptions.Clear();

    private sealed class Subscription
    {
        public required Action<TypedData, TypedData> Callback { get; init; }

        public Func<TypedData, TypedData, bool>? Filter { get; init; }
    }
}
