using System;
using Origo.Core.Abstractions.Scene;
using Origo.Core.DataSource;
using Origo.Core.Snd;

namespace Origo.Core.Save.Serialization;

internal sealed class SndSceneSerializer
{
    private readonly SndWorld _world;

    public SndSceneSerializer(SndWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        _world = world;
    }

    public DataSourceNode Serialize(ISndSceneAccess sceneAccess)
    {
        ArgumentNullException.ThrowIfNull(sceneAccess);
        var metaList = sceneAccess.SerializeMetaList();
        return _world.WriteMetaListNode(metaList);
    }

    public void DeserializeInto(ISndSceneAccess sceneAccess, DataSourceNode serializedNode, bool clearBeforeLoad)
    {
        ArgumentNullException.ThrowIfNull(sceneAccess);
        ArgumentNullException.ThrowIfNull(serializedNode);

        if (serializedNode.Kind != DataSourceNodeKind.Array)
            throw new InvalidOperationException("SND 场景序列化数据必须为数组格式。");

        var metaList = _world.Mappings.ResolveMetaListFromJsonArray(
            serializedNode,
            _world.ConverterRegistry);

        if (clearBeforeLoad)
            sceneAccess.ClearAll();
        sceneAccess.LoadFromMetaList(metaList);
    }
}
