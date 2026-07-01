using System;
using System.Collections.Generic;
using System.Globalization;
using Origo.Core.Abstractions.Snd;
using Origo.Core.DataSource;

namespace Origo.Core.Snd.Archetype;

/// <summary>
///     Loads archetype <c>.map</c> files (key-value object format) and coerces
///     string values to the appropriate typed data entries on an entity.
/// </summary>
public static class SndArchetypeLoader
{
    public static bool TryLoad(ISndFileAccess fileAccess, string path,
        out Dictionary<string, string> attributes)
    {
        ArgumentNullException.ThrowIfNull(fileAccess);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        attributes = [];

        if (!fileAccess.FileExists(path))
            return false;

        var node = fileAccess.ReadFile(path);
        if (node.Kind != DataSourceNodeKind.Map)
            return false;

        foreach (var key in node.Keys)
        {
            var raw = node[key].AsString();
            if (raw != null)
                attributes[key] = raw;
        }

        return attributes.Count > 0;
    }

    public static void ApplyAttributes(Origo.Core.Abstractions.Entity.ISndEntity entity,
        Dictionary<string, string> attributes)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(attributes);

        foreach (var (key, rawValue) in attributes)
        {
            if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                entity.SetData(key, i);
            else if (long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                entity.SetData(key, l);
            else if (float.TryParse(rawValue,
                         NumberStyles.Float,
                         CultureInfo.InvariantCulture,
                         out var f))
                entity.SetData(key, f);
            else if (bool.TryParse(rawValue, out var b))
                entity.SetData(key, b);
            else
                entity.SetData(key, rawValue);
        }
    }
}
