using System;
using System.Linq;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.DataSource.Converters;

internal sealed class StrategyMetaDataConverter : DataSourceConverter<StrategyMetaData>
{
    public override StrategyMetaData Read(DataSourceNode node)
    {
        var meta = new StrategyMetaData();

        if (node.TryGetValue("lifecycle_indices", out var lifecycleNode) && lifecycleNode is not null && !lifecycleNode.IsNull)
            foreach (var element in lifecycleNode.Elements)
                meta.LifecycleIndices.Add(element.AsString());

        if (node.TryGetValue("active_indices", out var activeNode) && activeNode is not null && !activeNode.IsNull)
            foreach (var element in activeNode.Elements)
                meta.ActiveIndices.Add(element.AsString());

        if (node.TryGetValue("observer_indices", out var observerIndicesNode) && observerIndicesNode is not null && !observerIndicesNode.IsNull)
            foreach (var element in observerIndicesNode.Elements)
            {
                // The writer only ever emits object elements; anything else
                // is corrupt save data and must fail the strict read instead
                // of silently dropping the damaged binding.
                if (element.Kind != DataSourceNodeKind.Map)
                    throw new InvalidOperationException(
                        $"observer_indices entries must be objects ({{ \"target\": [...] }}), but found {element.Kind}. " +
                        "The save data is corrupt and cannot be recovered.");

                var binding = new StrategyMetaData.ObserverBinding();
                if (element.Keys.Count() != 1)
                    throw new InvalidOperationException(
                        $"observer_indices entries must contain exactly one target key " +
                        $"(\"{{ \\\"target\\\": [...] }}\"), but found {element.Keys.Count()} keys. " +
                        "The save data is corrupt and cannot be recovered.");

                binding.Target = element.Keys.First();
                var indicesNode = element[binding.Target];
                if (indicesNode is not null && !indicesNode.IsNull)
                    foreach (var indexElement in indicesNode.Elements)
                        binding.ObserverIndices.Add(indexElement.AsString());

                if (!string.IsNullOrWhiteSpace(binding.Target))
                    meta.ObserverIndices.Add(binding);
            }

        return meta;
    }

    public override DataSourceNode Write(StrategyMetaData value)
    {
        var lifecycleIndices = DataSourceNode.CreateArray();
        foreach (var index in value.LifecycleIndices)
            lifecycleIndices.Add(DataSourceNode.CreateString(index));

        var activeIndices = DataSourceNode.CreateArray();
        foreach (var index in value.ActiveIndices)
            activeIndices.Add(DataSourceNode.CreateString(index));

        var result = DataSourceNode.CreateObject()
            .Add("lifecycle_indices", lifecycleIndices)
            .Add("active_indices", activeIndices);

        var observerIndices = DataSourceNode.CreateArray();
        foreach (var binding in value.ObserverIndices)
        {
            if (string.IsNullOrWhiteSpace(binding.Target))
                continue;

            var indices = DataSourceNode.CreateArray();
            foreach (var index in binding.ObserverIndices)
                indices.Add(DataSourceNode.CreateString(index));

            observerIndices.Add(DataSourceNode.CreateObject()
                .Add(binding.Target, indices));
        }

        result.Add("observer_indices", observerIndices);

        return result;
    }
}
