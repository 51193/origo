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
    /// <exception cref="InvalidOperationException">
    ///     Thrown when no converter is registered for the type or its base/interface
    ///     chain, or when the resolved converter returns an instance that is not
    ///     assignable to <typeparamref name="T" />.
    /// </exception>
    public T Read<T>(DataSourceNode node)
    {
        if (_converters.TryGetValue(typeof(T), out var converter))
            return ((DataSourceConverter<T>)converter).Read(node);

        // Fall back along the base-class and interface chains so a derived
        // type reads through its registered base converter. The converter's
        // returned instance must actually be assignable to T: a converter
        // registered for an interface may return a different concrete type
        // than the requested one, which would otherwise surface as an opaque
        // InvalidCastException (or, worse, a silently drifted value type).
        var fallback = FindConverter(typeof(T));
        var value = fallback.ReadObject(node);
        if (value is T typed)
            return typed;
        throw new InvalidOperationException(
            $"Converter '{fallback.GetType().Name}' returned " +
            $"'{value?.GetType().Name ?? "null"}', which is not assignable to the requested type " +
            $"'{typeof(T).FullName}'. Register a converter for the requested type, or read through " +
            "the registered interface type.");
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

    /// <summary>
    ///     Deserializes a node into the given <paramref name="type" /> via its registered
    ///     converter. Fails fast when the converter returns an instance that is not
    ///     assignable to <paramref name="type" />.
    /// </summary>
    public object? Read(Type type, DataSourceNode node)
    {
        ArgumentNullException.ThrowIfNull(type);
        var converter = FindConverter(type);
        var value = converter.ReadObject(node);
        if (value is not null && !type.IsInstanceOfType(value))
            throw new InvalidOperationException(
                $"Converter '{converter.GetType().Name}' returned '{value.GetType().Name}', " +
                $"which is not assignable to the requested type '{type.FullName}'. Register a converter " +
                "for the requested type, or read through the registered base/interface type.");
        return value;
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
