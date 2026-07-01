namespace Origo.Core.DataSource.Codec;

/// <summary>
///     原始字符串编解码器：将无结构的纯文本（如 SHA 哈希、哨兵标记）包装为单字符串 <see cref="DataSourceNode" />。
///     用于 <c>.sha</c>、<c>.write_in_progress</c> 等无结构化内容的文件后缀。
///     <para>
///         编码：取 <see cref="DataSourceNode.AsString()" /> 作为原始文本输出。
///         解码：将原始文本包裹为 <see cref="DataSourceNode.CreateString" />。
///     </para>
/// </summary>
internal sealed class RawStringDataSourceCodec : IDataSourceCodec
{
    public DataSourceNode Decode(string rawText) =>
        DataSourceNode.CreateString(rawText);

    public string Encode(DataSourceNode node) =>
        node.AsString();
}
