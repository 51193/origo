namespace Origo.Core.DataSource;

/// <summary>
///     The kind of a data source node, describing the data structure the node holds.
/// </summary>
public enum DataSourceNodeKind
{
    /// <summary>Key-value object node.</summary>
    Map,

    /// <summary>Ordered array node.</summary>
    Array,

    /// <summary>Text value node.</summary>
    Text,

    /// <summary>Numeric value node whose raw text is preserved.</summary>
    Number,

    /// <summary>Boolean value node.</summary>
    Bool,

    /// <summary>Null value node.</summary>
    Null
}
