using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Blackboard;

/// <summary>
///     Default in-memory blackboard implementation using TypedData to store key-value pairs
///     while preserving type information.
/// </summary>
public sealed class Blackboard : IBlackboard
{
    private readonly Dictionary<string, TypedData> _data = new(StringComparer.Ordinal);

    public void SetValue<T>(string key, T value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));

        _data[key] = TypedDataFactory<T>.Create(value);
    }

    public (bool found, T value) TryGet<T>(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Blackboard key cannot be null or whitespace.", nameof(key));

        if (_data.TryGetValue(key, out var td) && TypedDataFactory<T>.TryExtract(td, out var value))
            return (true, value);

        return (false, default!);
    }

    public void Clear() => _data.Clear();

    public IReadOnlyCollection<string> GetKeys() => _data.Keys;

    public IReadOnlyDictionary<string, TypedData> SerializeAll() =>
        new Dictionary<string, TypedData>(_data, StringComparer.Ordinal);

    public void DeserializeAll(IReadOnlyDictionary<string, TypedData> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data.Clear();
        foreach (var pair in data)
            _data[pair.Key] = pair.Value;
    }
}
