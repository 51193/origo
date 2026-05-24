using System.Collections.Generic;

namespace Origo.Core.Snd.Metadata;

/// <summary>
///     与某个 SND 实体关联的策略索引列表。
/// </summary>
public sealed class StrategyMetaData
{
    public List<string> Indices { get; set; } = new();
}
