namespace Origo.Core.DataSource;

/// <summary>
///     数据源编解码器接口，负责原始文本与 <see cref="DataSourceNode" /> 之间的双向转换。
///     不同的文件格式（JSON、map 等）各自提供实现。
/// </summary>
internal interface IDataSourceCodec
{
    DataSourceNode Decode(string rawText);
    string Encode(DataSourceNode node);
}
