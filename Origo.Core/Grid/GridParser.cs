using System.Text.Json;

namespace Origo.Core.Grid;

public static class GridParser
{
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

        if (!int.TryParse(parts[0].Trim(), out var x))
            return null;
        if (!int.TryParse(parts[1].Trim(), out var z))
            return null;

        return (x, z);
    }
}
