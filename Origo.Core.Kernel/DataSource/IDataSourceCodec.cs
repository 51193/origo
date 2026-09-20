namespace Origo.Core.DataSource;

/// <summary>
///     Data source codec interface, responsible for bidirectional conversion between raw text and
///     <see cref="DataSourceNode" />. Different file formats (JSON, map, etc.) each provide their
///     own implementation.
/// </summary>
internal interface IDataSourceCodec
{
    DataSourceNode Decode(string rawText);
    string Encode(DataSourceNode node);
}
