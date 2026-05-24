using System;
using System.Collections.Generic;

namespace Origo.Core.DataSource;

/// <summary>
///     管理 <see cref="DataSourceConverter{T}" /> 实例的注册表，按类型进行存取。
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

    public DataSourceNode Write<T>(T value) => Get<T>().Write(value);

    public object? Read(Type type, DataSourceNode node)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (!_converters.TryGetValue(type, out var converter))
            throw new InvalidOperationException(
                $"No DataSourceConverter registered for type '{type.FullName}'.");

        return converter.ReadObject(node);
    }

    public DataSourceNode Write(Type type, object? value)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (value is null)
            return DataSourceNode.CreateNull();

        if (!_converters.TryGetValue(type, out var converter))
            throw new InvalidOperationException(
                $"No DataSourceConverter registered for type '{type.FullName}'.");

        return converter.WriteObject(value);
    }
}
