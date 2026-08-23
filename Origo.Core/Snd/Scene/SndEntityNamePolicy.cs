using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd.Scene;

/// <summary>
///     Single source of truth for entity-name uniqueness validation in the
///     two orchestrated entity-creation paths: runtime spawn
///     (<see cref="SndEntityFactory" />) and load-time scene recovery
///     (<see cref="Save.Serialization.SndSceneSerializer" />).
/// </summary>
internal static class SndEntityNamePolicy
{
    public static void EnsureAvailable(ISndSceneHost host, SndMetaData metaData)
    {
        ArgumentNullException.ThrowIfNull(host);
        var name = RequireName(metaData);
        if (host.FindByName(name) is not null)
            throw new InvalidOperationException(
                $"Entity name '{name}' already exists in the scene; entity names must be unique within a session.");
    }

    public static void EnsureUniqueBatch(ISndSceneHost host, IEnumerable<SndMetaData> metaList)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(metaList);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var metaData in metaList)
        {
            var name = RequireName(metaData);
            if (!seen.Add(name))
                throw new InvalidOperationException(
                    $"Duplicate entity name '{name}' in batch; entity names must be unique within a session.");

            EnsureAvailable(host, metaData);
        }
    }

    private static string RequireName(SndMetaData metaData)
    {
        ArgumentNullException.ThrowIfNull(metaData);
        if (string.IsNullOrWhiteSpace(metaData.Name))
            throw new ArgumentException("SndMetaData.Name cannot be null or whitespace.", nameof(metaData));
        return metaData.Name;
    }
}
