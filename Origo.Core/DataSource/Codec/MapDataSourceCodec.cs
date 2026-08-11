using System;
using System.Linq;
using System.Text;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Logging;

namespace Origo.Core.DataSource.Codec;

/// <summary>
///     Codec for the simple key: value format (.map files). Lazy loading is not supported because .map files
///     are always small and flat.
/// </summary>
internal sealed class MapDataSourceCodec(ILogger? logger = null) : IDataSourceCodec
{
    private readonly ILogger _logger = logger ?? NullLogger.Instance;

    public DataSourceNode Decode(string rawText)
    {
        var pairs = KeyValueFileParser.Parse(rawText, "<map>", true, _logger, allowEmptyValues: true);
        var node = DataSourceNode.CreateObject();
        foreach (var (key, value) in pairs)
            node.Add(key, DataSourceNode.CreateString(value));
        return node;
    }

    public string Encode(DataSourceNode node)
    {
        if (node.Kind != DataSourceNodeKind.Map)
            throw new InvalidOperationException("MapDataSourceCodec can only encode Object nodes.");

        var sb = new StringBuilder();

        foreach (var key in node.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var child = node[key];
            if (child.IsNull)
                continue;

            var value = child.AsString();
            // A line break in a value would split the encoded text into
            // several lines that the strict decoder cannot parse back: the
            // codec must reject such values instead of producing files it
            // cannot read.
            if (value.Contains('\n') || value.Contains('\r'))
                throw new InvalidOperationException(
                    $"MapDataSourceCodec cannot encode key '{key}': its value contains " +
                    "a line break, which the strict line-based decoder cannot read back.");

            sb.Append(key);
            sb.Append(": ");
            sb.AppendLine(value);
        }

        return sb.ToString();
    }
}
