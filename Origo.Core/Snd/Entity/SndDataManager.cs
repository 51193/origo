using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Logging;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd.Entity;

/// <summary>
///     Manages the data dictionary and change notifications for a single SND entity.
///     This type does not depend on a specific engine entity and interacts solely
///     through the ISndEntity interface.
/// </summary>
internal sealed class SndDataManager
{
    private readonly ILogger _logger;

    private readonly DataObserverManager _observerManager = new();
    private readonly Dictionary<string, List<SubscriptionPair>> _subscriptionMap = [];
    private readonly ISndEntity _target;

    private Dictionary<string, TypedData> _data = [];

    public SndDataManager(ISndEntity target, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(logger);
        _target = target;
        _logger = logger;
    }

    public void Subscribe(string name, Action<ISndEntity, TypedData, TypedData> callback,
        Func<ISndEntity, TypedData, TypedData, bool>? filter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(callback);

        var wrappedCallback = new Action<TypedData, TypedData>((oldValue, newValue) =>
            callback(_target, oldValue, newValue));
        var wrappedFilter = filter is null
            ? null
            : new Func<TypedData, TypedData, bool>((oldValue, newValue) =>
                filter(_target, oldValue, newValue));

        _observerManager.Subscribe(
            name,
            wrappedCallback,
            wrappedFilter
        );
        if (!_subscriptionMap.TryGetValue(name, out var list))
        {
            list = [];
            _subscriptionMap[name] = list;
        }

        list.Add(new SubscriptionPair
        {
            OriginalCallback = callback,
            WrappedCallback = wrappedCallback
        });
    }

    public void Unsubscribe(string name, Action<ISndEntity, TypedData, TypedData> callback)
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
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (value is null)
            throw new ArgumentNullException(nameof(value),
                $"Cannot store a null value for key '{name}'. Data values must be non-null.");

        // Create the value before touching the dictionary: if conversion
        // throws (adapter converter failure), no default entry is left behind.
        var newValue = TypedDataFactory<T>.Create(value);

        ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(_data, name, out var exists);
        if (exists && slot.Equals(newValue)) return;

        var oldValue = slot;
        slot = newValue;
        _observerManager.NotifyObservers(name, oldValue, newValue);
    }

    public (bool, T?) TryGetData<T>(string name)
    {
        if (_data.TryGetValue(name, out var td) && TypedDataFactory<T>.TryExtract(td, out var value))
            return (true, value);
        return (false, default);
    }

    public bool TryGetData<T>(string name, out T? value)
    {
        if (_data.TryGetValue(name, out var td) && TypedDataFactory<T>.TryExtract(td, out var extracted))
        {
            value = extracted;
            return true;
        }

        value = default;
        return false;
    }

    public T GetData<T>(string name) where T : notnull => GetRequiredData<T>(name);

    public T GetRequiredData<T>(string name) where T : notnull
    {
        if (_data.TryGetValue(name, out var td) && TypedDataFactory<T>.TryExtract(td, out var value))
            return value;
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
        public required Action<ISndEntity, TypedData, TypedData> OriginalCallback { get; init; }
        public required Action<TypedData, TypedData> WrappedCallback { get; init; }
    }
}
