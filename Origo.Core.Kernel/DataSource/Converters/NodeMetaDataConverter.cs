using System;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.DataSource.Converters;

internal sealed class NodeMetaDataConverter : DataSourceConverter<NodeMetaData>
{
    public override NodeMetaData Read(DataSourceNode node)
    {
        MetaDataPairsCodec.ValidateMetaNode(node, "Node metadata");

        var meta = new NodeMetaData();
        MetaDataPairsCodec.ReadPairs(node, "Node metadata", meta.Pairs, StringDataSourceConverter.ReadElement);
        return meta;
    }

    public override DataSourceNode Write(NodeMetaData value) =>
        MetaDataPairsCodec.WritePairs(value.Pairs, DataSourceNode.CreateString);
}
