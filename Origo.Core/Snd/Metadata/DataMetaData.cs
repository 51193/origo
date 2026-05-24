using System.Collections.Generic;

namespace Origo.Core.Snd.Metadata;

/// <summary>
///     用于序列化与反序列化 SND 关联的数据字典。
/// </summary>
public sealed class DataMetaData
{
    public Dictionary<string, TypedData> Pairs { get; set; } = new();
}
