using System;

namespace Origo.Core.DataSource;

/// <summary>
///     Round-trip validation rules for the line-based <c>key: value</c> text
///     format used by <c>.map</c> files. The strict decoder trims both fields,
///     splits on the first colon, and treats lines starting with <c>#</c> as
///     comments, so a key or value that violates these rules would change on
///     read-back (or make the file unreadable). The map codec and the save
///     display-metadata merger share the rules so invalid metadata fails before
///     any save file is written.
/// </summary>
internal static class MapTextFormat
{
    /// <summary>
    ///     Returns the round-trip rule a key violates, or null when the key can
    ///     be encoded and decoded back unchanged.
    /// </summary>
    internal static string? FindKeyError(string key)
    {
        if (string.IsNullOrEmpty(key))
            return "an empty key cannot be read back";
        if (!Utf16Text.IsWellFormed(key))
            return "the key contains an unpaired UTF-16 surrogate, which the encoder cannot write losslessly";
        if (!string.Equals(key, key.Trim(), StringComparison.Ordinal))
            return "leading or trailing whitespace would be removed by the strict decoder";
        if (key.StartsWith('#'))
            return "a leading '#' would make the strict decoder treat the line as a comment";
        if (key.Contains(':') || key.Contains('\n') || key.Contains('\r'))
            return "':' and line breaks are reserved by the line-based key: value format";

        return null;
    }

    /// <summary>
    ///     Returns the round-trip rule a value violates, or null when the value
    ///     can be encoded and decoded back unchanged.
    /// </summary>
    internal static string? FindValueError(string value)
    {
        if (!Utf16Text.IsWellFormed(value))
            return "the value contains an unpaired UTF-16 surrogate, which the encoder cannot write losslessly";
        if (value.Contains('\n') || value.Contains('\r'))
            return "the value contains a line break, which the strict line-based decoder cannot read back";
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            return "the value has leading or trailing whitespace, which the strict decoder would remove";

        return null;
    }
}
