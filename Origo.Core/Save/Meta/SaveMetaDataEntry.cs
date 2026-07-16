using System.Collections.Generic;

namespace Origo.Core.Save.Meta;

/// <summary>
///     Save slot entry, containing the slot ID and the associated display
///     metadata key-value pairs. Populated by the save system and indirectly
///     consumed by metadata consumers such as the return value of
///     <c>ISndSaveOperations.ListSaves()</c>.
/// </summary>
public sealed class SaveMetaDataEntry
{
    /// <summary>The unique identifier of the save slot.</summary>
    public string SaveId { get; init; } = string.Empty;

    /// <summary>The display metadata for this slot (from <c>meta.map</c>).</summary>
    public IReadOnlyDictionary<string, string> MetaData { get; init; } = new Dictionary<string, string>();
}
