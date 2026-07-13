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

    public DataSourceNode Build(ISndSceneAccess sceneAccess)
    {
        ArgumentNullException.ThrowIfNull(sceneAccess);
        var metaList = sceneAccess.BuildMetaList();
        return _world.WriteMetaListNode(metaList);
    }

    public void RecoverInto(ISndSceneAccess sceneHost, DataSourceNode serializedNode)
    {
        ArgumentNullException.ThrowIfNull(sceneHost);
        ArgumentNullException.ThrowIfNull(serializedNode);

        if (serializedNode.Kind != DataSourceNodeKind.Array)
            throw new InvalidOperationException("SND scene serialization data must be in array format.");

        var metaList = _world.Mappings.ResolveMetaListFromJsonArray(
            serializedNode,
            _world.ConverterRegistry);

        sceneHost.RecoverFromMetaList(metaList);
    }
}
