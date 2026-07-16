using System;
using System.Linq;
using System.Text;
using Origo.Core.Logging;

namespace Origo.Core.DataSource.Codec;

/// <summary>
///     Codec for the simple key: value format (.map files). Lazy loading is not supported because .map files
///     are always small and flat.
/// </summary>
internal sealed class MapDataSourceCodec : IDataSourceCodec
{
    public DataSourceNode Decode(string rawText)
    {
        var pairs = KeyValueFileParser.Parse(rawText, "<map>", true, NullLogger.Instance, allowEmptyValues: true);
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

            sb.Append(key);
            sb.Append(": ");
            sb.AppendLine(child.AsString());
        }

        return sb.ToString();
    }
}
