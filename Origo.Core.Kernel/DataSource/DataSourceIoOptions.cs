using System;
using System.Collections.Generic;
using System.IO;

namespace Origo.Core.DataSource;

/// <summary>
///     DataSource I/O routing configuration center: selects codecs by file suffix.
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
        // A missing suffix (no extension) is a valid lookup key, not an
        // invalid argument; only an explicitly blank value is rejected.
        ArgumentNullException.ThrowIfNull(suffix);
        if (suffix.Length == 0)
            return string.Empty;

        var trimmed = suffix.Trim();
        if (trimmed.Length == 0)
            throw new ArgumentException("Codec suffix cannot be whitespace.", nameof(suffix));

        return trimmed[0] == '.'
            ? trimmed.ToLowerInvariant()
            : $".{trimmed.ToLowerInvariant()}";
    }
}
