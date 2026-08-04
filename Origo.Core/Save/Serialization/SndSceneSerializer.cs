using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Scene;
using Origo.Core.DataSource;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Save.Serialization;

/// <summary>
///     Bidirectional SND scene serializer: collects entity metadata from
///     a scene host into a <see cref="DataSourceNode" /> array, and
///     recovers entities from a serialized array back into a scene host.
/// </summary>
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

    /// <summary>
    ///     Recovers entities from a serialized array into the scene host and
    ///     returns the recovered metadata list. Entity recovery itself does
    ///     not apply all metadata fields (e.g. observer binding topology),
    ///     so the returned list lets callers restore that state.
    /// </summary>
    public IReadOnlyList<SndMetaData> RecoverInto(ISndSceneAccess sceneHost,
        DataSourceNode serializedNode)
    {
        ArgumentNullException.ThrowIfNull(sceneHost);
        ArgumentNullException.ThrowIfNull(serializedNode);

        if (serializedNode.Kind != DataSourceNodeKind.Array)
            throw new InvalidOperationException("SND scene serialization data must be in array format.");

        var metaList = _world.Mappings.ResolveMetaListFromJsonArray(
            serializedNode,
            _world.ConverterRegistry);

        sceneHost.RecoverFromMetaList(metaList);
        return metaList;
    }
}
