using System;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.DataSource.Converters;

internal sealed class SndMetaDataConverter : DataSourceConverter<SndMetaData>
{
    private readonly DataMetaDataConverter _dataMetaDataConverter;
    private readonly NodeMetaDataConverter _nodeMetaDataConverter;
    private readonly StrategyMetaDataConverter _strategyMetaDataConverter;

    public SndMetaDataConverter(
        NodeMetaDataConverter nodeMetaDataConverter,
        StrategyMetaDataConverter strategyMetaDataConverter,
        DataMetaDataConverter dataMetaDataConverter)
    {
        ArgumentNullException.ThrowIfNull(nodeMetaDataConverter);
        ArgumentNullException.ThrowIfNull(strategyMetaDataConverter);
        ArgumentNullException.ThrowIfNull(dataMetaDataConverter);
        _nodeMetaDataConverter = nodeMetaDataConverter;
        _strategyMetaDataConverter = strategyMetaDataConverter;
        _dataMetaDataConverter = dataMetaDataConverter;
    }

    public override SndMetaData Read(DataSourceNode node)
    {
        if (node.Kind != DataSourceNodeKind.Map)
            throw new InvalidOperationException(
                $"SND metadata must be a JSON object, but found {node.Kind}. " +
                "The save data is corrupt and cannot be recovered.");

        var meta = new SndMetaData();

        if (node.TryGetValue("name", out var nameNode) && nameNode is not null && !nameNode.IsNull)
            meta.Name = nameNode.AsString();

        if (node.TryGetValue("node", out var nodeMetaNode) && nodeMetaNode is not null && !nodeMetaNode.IsNull)
            meta.NodeMetaData = _nodeMetaDataConverter.Read(nodeMetaNode);

        if (node.TryGetValue("strategy", out var strategyNode) && strategyNode is not null && !strategyNode.IsNull)
            meta.StrategyMetaData = _strategyMetaDataConverter.Read(strategyNode);

        if (node.TryGetValue("data", out var dataNode) && dataNode is not null && !dataNode.IsNull)
            meta.DataMetaData = _dataMetaDataConverter.Read(dataNode);

        return meta;
    }

    public override DataSourceNode Write(SndMetaData value)
    {
        var node = DataSourceNode.CreateObject();

        node.Add("name", DataSourceNode.CreateString(value.Name));

        node.Add("node", value.NodeMetaData is not null
            ? _nodeMetaDataConverter.Write(value.NodeMetaData)
            : DataSourceNode.CreateNull());

        node.Add("strategy", value.StrategyMetaData is not null
            ? _strategyMetaDataConverter.Write(value.StrategyMetaData)
            : DataSourceNode.CreateNull());

        node.Add("data", value.DataMetaData is not null
            ? _dataMetaDataConverter.Write(value.DataMetaData)
            : DataSourceNode.CreateNull());

        return node;
    }
}
