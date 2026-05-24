namespace Origo.Core.DataSource;

/// <summary>
///     数据源转换器的非泛型基类，用于在注册表中进行运行时类型分发。
/// </summary>
public abstract class DataSourceConverterBase
{
    internal abstract object? ReadObject(DataSourceNode node);
    internal abstract DataSourceNode WriteObject(object? value);
}

/// <summary>
///     数据源转换器基类，负责在 <see cref="DataSourceNode" /> 与强类型对象之间双向转换。
/// </summary>
public abstract class DataSourceConverter<T> : DataSourceConverterBase
{
    public abstract T Read(DataSourceNode node);
    public abstract DataSourceNode Write(T value);

    internal override object? ReadObject(DataSourceNode node) => Read(node);

    internal override DataSourceNode WriteObject(object? value) => Write((T)value!);
}
