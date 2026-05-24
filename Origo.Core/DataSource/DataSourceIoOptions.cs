using System;
using System.Collections.Generic;
using System.IO;

namespace Origo.Core.DataSource;

/// <summary>
///     DataSource I/O 路由配置中心：按文件后缀选择编解码器。
/// </summary>
internal sealed class DataSourceIoOptions
{
    private readonly Dictionary<string, DataSourceCodecKind> _suffixToCodec = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, DataSourceCodecKind> SuffixToCodec => _suffixToCodec;

    public DataSourceIoOptions RegisterSuffix(string suffix, DataSourceCodecKind codecKind)
    {
        var normalized = NormalizeSuffix(suffix);
        _suffixToCodec[normalized] = codecKind;
        return this;
    }

    public bool TryResolveCodecKind(string filePath, out DataSourceCodecKind codecKind, out string normalizedSuffix)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("DataSource file path cannot be null or whitespace.", nameof(filePath));

        normalizedSuffix = NormalizeSuffix(Path.GetExtension(filePath));
        return _suffixToCodec.TryGetValue(normalizedSuffix, out codecKind);
    }

    internal static string NormalizeSuffix(string suffix)
    {
        if (string.IsNullOrWhiteSpace(suffix))
            throw new ArgumentException("Codec suffix cannot be null or whitespace.", nameof(suffix));

        var trimmed = suffix.Trim();
        return trimmed[0] == '.'
            ? trimmed.ToLowerInvariant()
            : $".{trimmed.ToLowerInvariant()}";
    }
}
