using System;
using System.Collections.Generic;

namespace Origo.Core.DataSource;

/// <summary>
///     A registry that manages <see cref="DataSourceConverter{T}" /> instances, accessed by type.
/// </summary>
public sealed class DataSourceConverterRegistry
{
    private readonly Dictionary<Type, DataSourceConverterBase> _converters = [];

    /// <summary>Registers a converter for type <typeparamref name="T" />, replacing any previous registration.</summary>
    public void Register<T>(DataSourceConverter<T> converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        _converters[typeof(T)] = converter;
    }

    /// <summary>Gets the converter registered for type <typeparamref name="T" />.</summary>
    /// <exception cref="InvalidOperationException">Thrown when no converter is registered for the type.</exception>
    public DataSourceConverter<T> Get<T>()
    {
        if (_converters.TryGetValue(typeof(T), out var converter))
            return (DataSourceConverter<T>)converter;

        throw new InvalidOperationException(
            $"No DataSourceConverter registered for type '{typeof(T).FullName}'.");
    }

    /// <summary>Deserializes a node into <typeparamref name="T" /> via its registered converter.</summary>
    public T Read<T>(DataSourceNode node)
    {
        if (_converters.TryGetValue(typeof(T), out var converter))
            return ((DataSourceConverter<T>)converter).Read(node);

        // Fall back along the base-class and interface chains so a derived
        // type reads through its registered base converter.
        return (T)FindConverter(typeof(T)).ReadObject(node)!;
    }

    /// <summary>Serializes a value into a node via its registered converter.</summary>
    public DataSourceNode Write<T>(T value)
    {
        if (value is null)
            return DataSourceNode.CreateNull();

        if (_converters.TryGetValue(typeof(T), out var converter))
            return ((DataSourceConverter<T>)converter).Write(value);

        return FindConverter(typeof(T)).WriteObject(value);
    }

    /// <summary>Deserializes a node into the given <paramref name="type" /> via its registered converter.</summary>
    public object? Read(Type type, DataSourceNode node)
    {
        ArgumentNullException.ThrowIfNull(type);
        return FindConverter(type).ReadObject(node);
    }

    /// <summary>Serializes a value into a node via the converter registered for <paramref name="type" />.</summary>
    public DataSourceNode Write(Type type, object? value)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (value is null)
            return DataSourceNode.CreateNull();

        return FindConverter(type).WriteObject(value);
    }

    private DataSourceConverterBase FindConverter(Type type)
    {
        if (_converters.TryGetValue(type, out var converter))
            return converter;

        for (var t = type.BaseType; t is not null; t = t.BaseType)
            if (_converters.TryGetValue(t, out converter))
                return converter;

        foreach (var iface in type.GetInterfaces())
            if (_converters.TryGetValue(iface, out converter))
                return converter;

        throw new InvalidOperationException(
            $"No DataSourceConverter registered for type '{type.FullName}'.");
    }
}
