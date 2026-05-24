using System.Collections.Generic;

namespace Origo.Core.Save.Meta;

/// <summary>
///     存档槽位条目，包含槽位 ID 与关联的展示用元数据键值对。
///     由 <see cref="Origo.Core.Snd.ISndContext.ListSavesWithMetaData" /> 返回。
/// </summary>
public sealed class SaveMetaDataEntry
{
    /// <summary>存档槽位唯一标识符。</summary>
    public string SaveId { get; init; } = string.Empty;

    /// <summary>该槽位的展示用元数据（来自 <c>meta.map</c>）。</summary>
    public IReadOnlyDictionary<string, string> MetaData { get; init; } = new Dictionary<string, string>();
}
