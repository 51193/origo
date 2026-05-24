using System;
using Origo.Core.Serialization;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.DataSource.Converters;

/// <summary>
///     TypedData 与 DataSourceNode 之间的转换器。
///     JSON 格式：{ "type": "System.Int32", "data": 42 }
/// </summary>
internal sealed class TypedDataConverter : DataSourceConverter<TypedData>
{
    private readonly DataSourceConverterRegistry _registry;
    private readonly TypeStringMapping _typeMapping;

    public TypedDataConverter(TypeStringMapping typeMapping, DataSourceConverterRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(typeMapping);
        ArgumentNullException.ThrowIfNull(registry);
        _typeMapping = typeMapping;
        _registry = registry;
    }

    public override TypedData Read(DataSourceNode node)
    {
        var typeName = node["type"].AsString();
        var type = _typeMapping.GetTypeByName(typeName);

        if (!node.TryGetValue("data", out var dataNode) || dataNode is null || dataNode.IsNull)
            return new TypedData(type, null);

        var data = _registry.Read(type, dataNode);
        return new TypedData(type, data);
    }

    public override DataSourceNode Write(TypedData value)
    {
        var typeName = _typeMapping.GetNameByType(value.DataType);

        var node = DataSourceNode.CreateObject();
        node.Add("type", DataSourceNode.CreateString(typeName));
        node.Add("data", _registry.Write(value.DataType, value.Data));

        return node;
    }
}
