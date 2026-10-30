namespace Origo.Core.DataSource.Codec;

/// <summary>
///     Raw string codec: wraps unstructured plain text (such as SHA hashes, sentinel markers) as a
///     single-string <see cref="DataSourceNode" />. Used for file suffixes with no structured content,
///     such as <c>.sha</c>, <c>.write_in_progress</c>.
///     <para>
///         Encoding: takes <see cref="DataSourceNode.AsString()" /> as the raw text output.
///         Decoding: wraps the raw text with <see cref="DataSourceNode.CreateString" />.
///     </para>
/// </summary>
internal sealed class RawStringDataSourceCodec : IDataSourceCodec
{
    public DataSourceNode Decode(string rawText) =>
        DataSourceNode.CreateString(rawText);

    public string Encode(DataSourceNode node)
    {
        var value = node.AsString();
        // A replacement-fallback encoder would collapse an unpaired UTF-16
        // surrogate into U+FFFD, silently changing the file content on
        // read-back; reject it instead of persisting replacement text.
        Utf16Text.ThrowIfNotWellFormed(value, "Raw text value");
        return value;
    }
}
