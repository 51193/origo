using Origo.Core.Snd.Metadata;

namespace Origo.Core.DataSource.Converters;

internal sealed class NodeMetaDataConverter : DataSourceConverter<NodeMetaData>
{
    public override NodeMetaData Read(DataSourceNode node)
    {
        var meta = new NodeMetaData();

        if (node.TryGetValue("pairs", out var pairsNode) && pairsNode is not null && !pairsNode.IsNull)
            foreach (var key in pairsNode.Keys)
                meta.Pairs[key] = pairsNode[key].AsString();

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
