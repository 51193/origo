namespace Origo.Core.DataSource;

/// <summary>
///     The kind of a data source node, describing the data structure the node holds.
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
