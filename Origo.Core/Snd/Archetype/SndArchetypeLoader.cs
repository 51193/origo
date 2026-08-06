using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Snd;
using Origo.Core.DataSource;
using Origo.Core.Utility;

namespace Origo.Core.Snd.Archetype;

/// <summary>
///     Loads archetype <c>.map</c> files (key-value object format) and coerces
///     string values to the appropriate typed data entries on an entity.
/// </summary>
public static class SndArchetypeLoader
{
    /// <summary>
    ///     Attempts to load an archetype <c>.map</c> file into a string-keyed
    ///     attribute dictionary. Returns false when the file does not exist or
    ///     is not a key-value map.
    /// </summary>
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

    /// <summary>
    ///     Applies string attributes to an entity, inferring each value's
    ///     runtime type via the shared string-to-typed inference (int → long →
    ///     float → bool → string).
    /// </summary>
    public static void ApplyAttributes(Origo.Core.Abstractions.Entity.ISndEntity entity,
        Dictionary<string, string> attributes)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(attributes);

        foreach (var (key, rawValue) in attributes)
        {
            ValueInference.SetData(entity, key, rawValue);
        }
    }
}
