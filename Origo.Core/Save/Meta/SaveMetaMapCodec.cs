using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Logging;
using Origo.Core.DataSource;

namespace Origo.Core.Save.Meta;

/// <summary>
///     解析/序列化存档元数据 map 文本，格式为按行 "key: value"。
/// </summary>
internal static class SaveMetaMapCodec
{
    public static IReadOnlyDictionary<string, string> Parse(
        string? content,
        ILogger logger,
        string? sourceName = null) =>
        KeyValueFileParser.Parse(content, sourceName ?? "unknown", false, logger);

    public static string Serialize(IReadOnlyDictionary<string, string>? map)
    {
        if (map is null || map.Count == 0)
            return string.Empty;

        return string.Join("\n", map.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}: {p.Value}"));
    }
}
