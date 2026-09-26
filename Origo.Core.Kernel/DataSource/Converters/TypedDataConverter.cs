using System;
using Origo.Core.Serialization;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.DataSource.Converters;

/// <summary>
///     Converter between TypedData and DataSourceNode.
///     JSON format: { "type": "Int32", "data": 42 }
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
            return CreateNullData(typeName, type);

        var data = _registry.Read(type, dataNode);
        if (data is null)
            return CreateNullData(typeName, type);

        var kind = TypedDataTypeMap.GetKindForType(type);
        if (kind == 0)
            return new TypedData(TypedData.UnregisteredKind, 0, data);

        var (inlineBits, refValue) = TypedDataObjectConverter.FromObject(kind, data);
        return new TypedData(kind, inlineBits, refValue);
    }

    /// <summary>
    ///     Builds the null-data TypedData for a registered or unregistered
    ///     type. A null value is representable only for reference types
    ///     (stored through the <c>_ref</c> slot); for registered value types
    ///     null cannot be expressed and is rejected as corrupted data
    ///     (fail-fast), because the framework never writes such entries and
    ///     silently coercing null to <c>default</c> would lose data.
    /// </summary>
    private static TypedData CreateNullData(string typeName, Type type)
    {
        var nullKind = TypedDataTypeMap.GetKindForType(type);
        if (nullKind != 0 && type.IsValueType)
            throw new InvalidOperationException(
                $"Cannot deserialize a null 'data' value for value type '{typeName}': " +
                "value types cannot represent null in TypedData storage. " +
                "The save data is corrupted or was written by an incompatible version.");
        return nullKind != 0
            ? new TypedData(nullKind, 0, null)
            : new TypedData(TypedData.UnregisteredKind, 0, null);
    }

    public override DataSourceNode Write(TypedData value)
    {
        var typeName = _typeMapping.GetNameByType(value.DataType);

        var node = DataSourceNode.CreateObject();
        node.Add("type", DataSourceNode.CreateString(typeName));
        node.Add("data", _registry.Write(value.DataType,
            TypedDataObjectConverter.ToObject(value)));

        return node;
    }
}
