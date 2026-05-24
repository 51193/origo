using System.Diagnostics.CodeAnalysis;

namespace Origo.Core.DataSource;

/// <summary>
///     数据源节点的类型，描述节点持有的数据结构。
/// </summary>
[SuppressMessage("Naming", "CA1720:Identifier contains type name")]
public enum DataSourceNodeKind
{
    Object,
    Array,
    String,
    Number,
    Boolean,
    Null
}
