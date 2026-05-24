using System;
using System.Linq;
using System.Text;

namespace Origo.Core.DataSource;

/// <summary>
///     简单 key: value 格式的编解码器（.map 文件）。
///     不支持延迟加载，因为 .map 文件始终较小且扁平。
/// </summary>
internal sealed class MapDataSourceCodec : IDataSourceCodec
{
    public DataSourceNode Decode(string rawText)
    {
        var node = DataSourceNode.CreateObject();
        var lines = rawText.Split('\n');

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            var separatorIndex = trimmed.IndexOf(':');
            if (separatorIndex < 0)
                continue;

            var key = trimmed[..separatorIndex].Trim();
            var value = trimmed[(separatorIndex + 1)..].Trim();

            if (key.Length > 0)
                node.Add(key, DataSourceNode.CreateString(value));
        }

        return node;
    }

    public string Encode(DataSourceNode node)
    {
        if (node.Kind != DataSourceNodeKind.Object)
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
