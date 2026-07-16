using System.Collections.Generic;

namespace Origo.Core.Snd.Metadata;

/// <summary>
///     Data dictionary for serialization and deserialization of SND-associated data.
/// </summary>
public sealed class DataMetaData
{
    public Dictionary<string, TypedData> Pairs { get; set; } = [];
}
