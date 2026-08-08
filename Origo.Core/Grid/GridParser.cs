using System.Globalization;
using System.Text.Json;

namespace Origo.Core.Grid;

/// <summary>
///     Parses grid coordinates from various input formats
///     (<c>string</c>, <c>JsonElement</c>) into (X, Z) tuples.
/// </summary>
public static class GridParser
{
    /// <summary>
    ///     Parses grid coordinates from a <c>string</c> (<c>"x,z"</c>) or string
    ///     <see cref="JsonElement" />. Returns <c>null</c> when the input is not
    ///     parseable; extra comma-separated tokens beyond the first two are ignored.
    /// </summary>
    public static (int X, int Z)? ParseCoords(object? input)
    {
        var str = input switch
        {
            string s => s,
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString(),
            _ => null
        };

        if (string.IsNullOrWhiteSpace(str))
            return null;

        var parts = str.Split(',');
        if (parts.Length < 2)
            return null;

        if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var x))
            return null;
        if (!int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var z))
            return null;

        return (x, z);
    }
}
