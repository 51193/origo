using System;
using System.Collections.Generic;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.DataSource.Converters;

internal sealed class BlackboardDataConverter : DataSourceConverter<IReadOnlyDictionary<string, TypedData>>
{
    private readonly TypedDataConverter _typedDataConverter;

    public BlackboardDataConverter(TypedDataConverter typedDataConverter)
    {
        ArgumentNullException.ThrowIfNull(typedDataConverter);
        _typedDataConverter = typedDataConverter;
    }

    public override IReadOnlyDictionary<string, TypedData> Read(DataSourceNode node)
    {
        var dict = new Dictionary<string, TypedData>(StringComparer.Ordinal);
        foreach (var key in node.Keys)
            dict[key] = _typedDataConverter.Read(node[key]);
        return dict;
    }

    public override DataSourceNode Write(IReadOnlyDictionary<string, TypedData> value)
    {
        var obj = DataSourceNode.CreateObject();
        foreach (var kvp in value)
            obj.Add(kvp.Key, _typedDataConverter.Write(kvp.Value));
        return obj;
    }
}
