using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Logging;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd.Entity;

/// <summary>
///     负责管理单个 SND 实体的数据字典与变更通知。
///     此类型不依赖具体的引擎实体，仅通过 ISndEntity 接口交互。
/// </summary>
internal sealed class SndDataManager
{
    private readonly ILogger _logger;

    private readonly DataObserverManager _observerManager = new();
    private readonly Dictionary<string, List<SubscriptionPair>> _subscriptionMap = new();
    private readonly ISndEntity _target;

    private Dictionary<string, TypedData> _data = new();

    public SndDataManager(ISndEntity target, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(logger);
        _target = target;
        _logger = logger;
    }

    public void Subscribe(string name, Action<ISndEntity, object?, object?> callback,
        Func<ISndEntity, object?, object?, bool>? filter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(callback);

        var wrappedCallback = new Action<object?, object?>((oldValue, newValue) =>
            callback(_target, oldValue, newValue));
        var wrappedFilter = filter is null
            ? null
            : new Func<object?, object?, bool>((oldValue, newValue) => filter(_target, oldValue, newValue));

        _observerManager.Subscribe(
            name,
            wrappedCallback,
            wrappedFilter
        );
        if (!_subscriptionMap.TryGetValue(name, out var list))
        {
            list = new List<SubscriptionPair>();
            _subscriptionMap[name] = list;
        }

        list.Add(new SubscriptionPair
        {
            OriginalCallback = callback,
            WrappedCallback = wrappedCallback
        });
    }

    public void Unsubscribe(string name, Action<ISndEntity, object?, object?> callback)
    {
        if (!_subscriptionMap.TryGetValue(name, out var list)) return;

        for (var i = list.Count - 1; i >= 0; i--)
        {
            var pair = list[i];
            if (pair.OriginalCallback != callback) continue;

            _observerManager.Unsubscribe(name, pair.WrappedCallback);
            list.RemoveAt(i);
        }

        if (list.Count == 0) _subscriptionMap.Remove(name);
    }

    public void SetData<T>(string name, T value)
    {
        var (isFine, oldValue) = TryGetData<T>(name);

        if (isFine && EqualityComparer<T>.Default.Equals(oldValue, value)) return;

        _data[name] = new TypedData(typeof(T), value);
        _observerManager.NotifyObservers(name, oldValue, value);
    }

    public (bool, T?) TryGetData<T>(string name)
    {
        if (_data.TryGetValue(name, out var typedData) && typedData.Data is T value) return (true, value);
        return (false, default);
    }

    public T GetData<T>(string name) => GetRequiredData<T>(name);

    public T GetRequiredData<T>(string name)
    {
        if (_data.TryGetValue(name, out var typedData) && typedData.Data is T value) return value;
        var message = $"Data with name '{name}' not found or is not of type '{typeof(T).Name}'.";
        _logger.Log(LogLevel.Error, nameof(SndDataManager), new LogMessageBuilder().Build(message));
        throw new InvalidOperationException(message);
    }

    public void Recover(DataMetaData meta)
    {
        _data = new Dictionary<string, TypedData>(meta.Pairs);
        _logger.Log(LogLevel.Info, nameof(SndDataManager),
            new LogMessageBuilder().Build($"Loaded {_data.Count} data entries."));
    }

    public void Release()
    {
        _observerManager.Clear();
        _subscriptionMap.Clear();
        _data.Clear();
    }

    public DataMetaData SerializeMeta()
    {
        return new DataMetaData
        {
            Pairs = new Dictionary<string, TypedData>(_data)
        };
    }

    private sealed class SubscriptionPair
    {
        public required Action<ISndEntity, object?, object?> OriginalCallback { get; init; }
        public required Action<object?, object?> WrappedCallback { get; init; }
    }
}
