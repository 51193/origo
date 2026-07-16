using System.Collections.Generic;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Abstractions.Node;

/// <summary>
///     Abstract SND node container behavior: responsible for node recovery,
///     lookup, release, and export.
/// </summary>
internal interface INodeHost
{
    INodeHandle GetNode(string name);

    IReadOnlyCollection<string> GetNodeNames();

    void Recover(NodeMetaData metaData);

    void Release();

    NodeMetaData SerializeMetaData();
}
