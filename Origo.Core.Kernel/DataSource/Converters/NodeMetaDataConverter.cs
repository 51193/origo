using System;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.DataSource.Converters;

internal sealed class NodeMetaDataConverter : DataSourceConverter<NodeMetaData>
{
    public override NodeMetaData Read(DataSourceNode node)
    {
        if (node.Kind != DataSourceNodeKind.Map)
            throw new InvalidOperationException(
                $"Node metadata must be a JSON object, but found {node.Kind}. " +
                "The save data is corrupt and cannot be recovered.");

        var meta = new NodeMetaData();

        if (node.TryGetValue("pairs", out var pairsNode) && pairsNode is not null && !pairsNode.IsNull)
        {
            if (pairsNode.Kind != DataSourceNodeKind.Map)
                throw new InvalidOperationException(
                    $"Node metadata 'pairs' must be a JSON object, but found {pairsNode.Kind}. " +
                    "The save data is corrupt and cannot be recovered.");

            foreach (var key in pairsNode.Keys)
                meta.Pairs[key] = StringDataSourceConverter.ReadElement(pairsNode[key]);
        }

        return meta;
    }

    public override DataSourceNode Write(NodeMetaData value)
    {
        var pairs = DataSourceNode.CreateObject();
        foreach (var kvp in value.Pairs)
            pairs.Add(kvp.Key, DataSourceNode.CreateString(kvp.Value));

        return DataSourceNode.CreateObject()
            .Add("pairs", pairs);
    }
}
