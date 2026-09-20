using System.Collections.Generic;

namespace Origo.Core.Snd.Metadata;

/// <summary>
///     Data dictionary for serialization and deserialization of SND-associated data.
/// </summary>
public sealed class DataMetaData
{
    /// <summary>Key-value data pairs to restore into the entity's data manager.</summary>
    public Dictionary<string, TypedData> Pairs { get; set; } = [];
}
