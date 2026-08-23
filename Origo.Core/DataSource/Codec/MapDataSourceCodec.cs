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
            ValidateKey(key);

            var child = node[key];
            if (child.IsNull)
                continue;
            if (child.Kind != DataSourceNodeKind.Text)
                throw new InvalidOperationException(
                    $"MapDataSourceCodec cannot encode key '{key}': child kind {child.Kind} " +
                    "is not representable in the string-only .map format. Use a Text child.");

            var value = child.AsString();
            // The strict decoder trims both fields and splits on the first
            // colon, so keys/values that would change on read-back are
            // rejected instead of producing files that silently corrupt data.
            if (value.Contains('\n') || value.Contains('\r'))
                throw new InvalidOperationException(
                    $"MapDataSourceCodec cannot encode key '{key}': its value contains " +
                    "a line break, which the strict line-based decoder cannot read back.");
            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"MapDataSourceCodec cannot encode key '{key}': its value has leading or trailing " +
                    "whitespace, which the strict decoder would remove.");

            sb.Append(key);
            sb.Append(": ");
            sb.AppendLine(value);
        }

        return sb.ToString();
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new InvalidOperationException("MapDataSourceCodec cannot encode an empty key.");
        if (!string.Equals(key, key.Trim(), StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"MapDataSourceCodec cannot encode key '{key}': leading or trailing whitespace " +
                "would be removed by the strict decoder.");
        if (key.StartsWith('#'))
            throw new InvalidOperationException(
                $"MapDataSourceCodec cannot encode key '{key}': a leading '#' would make the " +
                "strict decoder treat the line as a comment.");
        if (key.Contains(':') || key.Contains('\n') || key.Contains('\r'))
            throw new InvalidOperationException(
                $"MapDataSourceCodec cannot encode key '{key}': ':' and line breaks are reserved " +
                "by the line-based key: value format.");
    }
}
