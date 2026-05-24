using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.DataSource;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Save.Serialization;

internal sealed class BlackboardSerializer
{
    private readonly DataSourceConverterRegistry _registry;

    public BlackboardSerializer(DataSourceConverterRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    public DataSourceNode Serialize(IBlackboard blackboard)
    {
        var data = blackboard.SerializeAll();
        return _registry.Write<IReadOnlyDictionary<string, TypedData>>(data);
    }

    public void DeserializeInto(IBlackboard blackboard, DataSourceNode serializedNode)
    {
        ArgumentNullException.ThrowIfNull(serializedNode);
        var dict = _registry.Read<IReadOnlyDictionary<string, TypedData>>(serializedNode);
        if (dict is null)
            throw new InvalidOperationException("Failed to deserialize blackboard data.");
        blackboard.DeserializeAll(dict);
    }
}
