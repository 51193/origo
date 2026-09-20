using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Blackboard;

/// <summary>
///     Default in-memory <see cref="IBlackboard" /> implementation backed by a typed-data dictionary
///     that preserves type information for serialization.
/// </summary>
public sealed class Blackboard : IBlackboard
{
    private readonly Dictionary<string, TypedData> _data = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void SetValue<T>(string key, T value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));

        var typedData = TypedDataFactory<T>.Create(value);
        if (value is null && typedData.DataType == typeof(object))
            throw new ArgumentNullException(nameof(value),
                "Cannot store a null value for an unregistered reference type: " +
                "TypedData cannot recover the CLR type from a null reference. " +
                "Register the type as an inline kind or use a registered reference type.");

        _data[key] = typedData;
    }

    /// <inheritdoc />
    public (bool found, T value) TryGet<T>(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Blackboard key cannot be null or whitespace.", nameof(key));

        if (_data.TryGetValue(key, out var td) && TypedDataFactory<T>.TryExtract(td, out var value))
            return (true, value);

        return (false, default!);
    }

    /// <inheritdoc />
    public void Clear() => _data.Clear();

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetKeys() => _data.Keys;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, TypedData> SerializeAll() =>
        new Dictionary<string, TypedData>(_data, StringComparer.Ordinal);

    /// <inheritdoc />
    public void DeserializeAll(IReadOnlyDictionary<string, TypedData> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data.Clear();
        foreach (var pair in data)
            _data[pair.Key] = pair.Value;
    }
}
