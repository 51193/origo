using System;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.DataSource.Converters;

internal sealed class DataMetaDataConverter : DataSourceConverter<DataMetaData>
{
    private readonly TypedDataConverter _typedDataConverter;

    public DataMetaDataConverter(TypedDataConverter typedDataConverter)
    {
        ArgumentNullException.ThrowIfNull(typedDataConverter);
        _typedDataConverter = typedDataConverter;
    }

    public override DataMetaData Read(DataSourceNode node)
    {
        if (node.Kind != DataSourceNodeKind.Map)
            throw new InvalidOperationException(
                $"Data metadata must be a JSON object, but found {node.Kind}. " +
                "The save data is corrupt and cannot be recovered.");

        var meta = new DataMetaData();

        if (node.TryGetValue("pairs", out var pairsNode) && pairsNode is not null && !pairsNode.IsNull)
        {
            if (pairsNode.Kind != DataSourceNodeKind.Map)
                throw new InvalidOperationException(
                    $"Data metadata 'pairs' must be a JSON object, but found {pairsNode.Kind}. " +
                    "The save data is corrupt and cannot be recovered.");

            foreach (var key in pairsNode.Keys)
                meta.Pairs[key] = _typedDataConverter.Read(pairsNode[key]);
        }

        return meta;
    }

    public override DataSourceNode Write(DataMetaData value)
    {
        var pairs = DataSourceNode.CreateObject();
        foreach (var kvp in value.Pairs)
            pairs.Add(kvp.Key, _typedDataConverter.Write(kvp.Value));

        return DataSourceNode.CreateObject()
            .Add("pairs", pairs);
    }
}
