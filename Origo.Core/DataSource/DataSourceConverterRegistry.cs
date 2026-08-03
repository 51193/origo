using System;
using System.Collections.Generic;

namespace Origo.Core.DataSource;

/// <summary>
///     A registry that manages <see cref="DataSourceConverter{T}" /> instances, accessed by type.
/// </summary>
public sealed class DataSourceConverterRegistry
{
    private readonly Dictionary<Type, DataSourceConverterBase> _converters = [];

    public void Register<T>(DataSourceConverter<T> converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        _converters[typeof(T)] = converter;
    }

    public DataSourceConverter<T> Get<T>()
    {
        if (_converters.TryGetValue(typeof(T), out var converter))
            return (DataSourceConverter<T>)converter;

        throw new InvalidOperationException(
            $"No DataSourceConverter registered for type '{typeof(T).FullName}'.");
    }

    public T Read<T>(DataSourceNode node) => Get<T>().Read(node);

    public DataSourceNode Write<T>(T value)
    {
        if (value is null)
            return DataSourceNode.CreateNull();
        return Get<T>().Write(value);
    }

    public object? Read(Type type, DataSourceNode node)
    {
        ArgumentNullException.ThrowIfNull(type);
        return FindConverter(type).ReadObject(node);
    }

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
