using System.Collections.Generic;

namespace Origo.Core.Snd.Metadata;

/// <summary>
///     节点相关的元信息，使用键值对形式保存，具体含义由宿主引擎约定。
/// </summary>
public sealed class NodeMetaData
{
    public Dictionary<string, string> Pairs { get; set; } = new();
}
