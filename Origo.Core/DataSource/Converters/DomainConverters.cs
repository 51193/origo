using System;
using System.Collections.Generic;
using Origo.Core.Snd.Metadata;
using Origo.Core.StateMachine;

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

internal sealed class StrategyMetaDataConverter : DataSourceConverter<StrategyMetaData>
{
    public override StrategyMetaData Read(DataSourceNode node)
    {
        var meta = new StrategyMetaData();

        if (node.TryGetValue("indices", out var indicesNode) && indicesNode is not null && !indicesNode.IsNull)
            foreach (var element in indicesNode.Elements)
                meta.Indices.Add(element.AsString());

        return meta;
    }

    public override DataSourceNode Write(StrategyMetaData value)
    {
        var indices = DataSourceNode.CreateArray();
        foreach (var index in value.Indices)
            indices.Add(DataSourceNode.CreateString(index));

        return DataSourceNode.CreateObject()
            .Add("indices", indices);
    }
}

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
        var meta = new DataMetaData();

        if (node.TryGetValue("pairs", out var pairsNode) && pairsNode is not null && !pairsNode.IsNull)
            foreach (var key in pairsNode.Keys)
                meta.Pairs[key] = _typedDataConverter.Read(pairsNode[key]);

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

internal sealed class SndMetaDataListConverter : DataSourceConverter<IReadOnlyList<SndMetaData>>
{
    private readonly SndMetaDataConverter _sndMetaDataConverter;

    public SndMetaDataListConverter(SndMetaDataConverter sndMetaDataConverter)
    {
        ArgumentNullException.ThrowIfNull(sndMetaDataConverter);
        _sndMetaDataConverter = sndMetaDataConverter;
    }

    public override IReadOnlyList<SndMetaData> Read(DataSourceNode node)
    {
        var list = new List<SndMetaData>();
        foreach (var element in node.Elements)
            list.Add(_sndMetaDataConverter.Read(element));
        return list;
    }

    public override DataSourceNode Write(IReadOnlyList<SndMetaData> value)
    {
        var array = DataSourceNode.CreateArray();
        foreach (var item in value)
            array.Add(_sndMetaDataConverter.Write(item));
        return array;
    }
}

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

internal sealed class StringDictionaryConverter : DataSourceConverter<IReadOnlyDictionary<string, string>>
{
    public override IReadOnlyDictionary<string, string> Read(DataSourceNode node)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in node.Keys)
            dict[key] = node[key].AsString();
        return dict;
    }

    public override DataSourceNode Write(IReadOnlyDictionary<string, string> value)
    {
        var obj = DataSourceNode.CreateObject();
        foreach (var kvp in value)
            obj.Add(kvp.Key, DataSourceNode.CreateString(kvp.Value));
        return obj;
    }
}

internal sealed class StateMachineContainerPayloadConverter
    : DataSourceConverter<StateMachineContainerPayload>
{
    public override StateMachineContainerPayload Read(DataSourceNode node)
    {
        var payload = new StateMachineContainerPayload();

        if (node.TryGetValue("machines", out var machinesNode) && machinesNode is not null && !machinesNode.IsNull)
            foreach (var element in machinesNode.Elements)
            {
                var entry = new StateMachineEntryPayload();

                if (element.TryGetValue("key", out var keyNode) && keyNode is not null)
                    entry.Key = keyNode.AsString();

                if (element.TryGetValue("pushIndex", out var pushNode) && pushNode is not null)
                    entry.PushIndex = pushNode.AsString();

                if (element.TryGetValue("popIndex", out var popNode) && popNode is not null)
                    entry.PopIndex = popNode.AsString();

                if (element.TryGetValue("stack", out var stackNode) && stackNode is not null && !stackNode.IsNull)
                    foreach (var stackElement in stackNode.Elements)
                        entry.Stack.Add(stackElement.AsString());

                payload.Machines.Add(entry);
            }

        return payload;
    }

    public override DataSourceNode Write(StateMachineContainerPayload value)
    {
        var machines = DataSourceNode.CreateArray();

        foreach (var entry in value.Machines)
        {
            var entryNode = DataSourceNode.CreateObject();

            entryNode.Add("key", DataSourceNode.CreateString(entry.Key));
            entryNode.Add("pushIndex", DataSourceNode.CreateString(entry.PushIndex));
            entryNode.Add("popIndex", DataSourceNode.CreateString(entry.PopIndex));

            var stack = DataSourceNode.CreateArray();
            foreach (var item in entry.Stack)
                stack.Add(DataSourceNode.CreateString(item));
            entryNode.Add("stack", stack);

            machines.Add(entryNode);
        }

        return DataSourceNode.CreateObject()
            .Add("machines", machines);
    }
}
