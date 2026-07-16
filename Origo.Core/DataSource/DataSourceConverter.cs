namespace Origo.Core.DataSource;

/// <summary>
///     Non-generic base class for data source converters, used for runtime type dispatch in the registry.
/// </summary>
public abstract class DataSourceConverterBase
{
    internal abstract object? ReadObject(DataSourceNode node);
    internal abstract DataSourceNode WriteObject(object? value);
}

/// <summary>
///     Base class for data source converters, responsible for bidirectional conversion between
///     <see cref="DataSourceNode" /> and strongly-typed objects.
/// </summary>
public abstract class DataSourceConverter<T> : DataSourceConverterBase
{
    public abstract T Read(DataSourceNode node);
    public abstract DataSourceNode Write(T value);

    internal override object? ReadObject(DataSourceNode node) => Read(node);

    internal override DataSourceNode WriteObject(object? value) => Write((T)value!);
}
