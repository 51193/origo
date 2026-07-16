using System.Collections.Generic;

namespace Origo.Core.Snd.Metadata;

/// <summary>
///     Node-related metadata stored as key-value pairs;
///     the specific semantics are defined by the host engine convention.
/// </summary>
public sealed class NodeMetaData
{
    public Dictionary<string, string> Pairs { get; set; } = [];
}
