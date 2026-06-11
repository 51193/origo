namespace Origo.Core.DataSource;

/// <summary>
///     数据源节点的类型，描述节点持有的数据结构。
/// </summary>
public enum DataSourceNodeKind
{
    Map,
    Array,
    Text,
    Number,
    Bool,
    Null
}
