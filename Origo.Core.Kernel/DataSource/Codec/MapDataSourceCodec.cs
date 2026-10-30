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
        var pairs = KeyValueFileParser.Parse(rawText, "<map>", _logger);
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
            // The strict decoder trims both fields, splits on the first colon,
            // and treats a leading '#' as a comment; keys/values that would
            // change on read-back are rejected instead of producing files that
            // silently corrupt data. The same rules are applied to display
            // metadata before a save starts (see MapTextFormat).
            if (MapTextFormat.FindKeyError(key) is { } keyError)
                throw new InvalidOperationException(
                    $"MapDataSourceCodec cannot encode key '{key}': {keyError}.");

            var child = node[key];
            if (child.IsNull)
                continue;
            if (child.Kind != DataSourceNodeKind.Text)
                throw new InvalidOperationException(
                    $"MapDataSourceCodec cannot encode key '{key}': child kind {child.Kind} " +
                    "is not representable in the string-only .map format. Use a Text child.");

            var value = child.AsString();
            if (MapTextFormat.FindValueError(value) is { } valueError)
                throw new InvalidOperationException(
                    $"MapDataSourceCodec cannot encode key '{key}': {valueError}.");

            sb.Append(key);
            sb.Append(": ");
            sb.AppendLine(value);
        }

        return sb.ToString();
    }
}
