using System;
using System.Collections.Generic;

namespace Origo.EvolutionDemo.Logic.Gameplay;

/// <summary>Validates the genome payloads of a loaded save before simulation resumes.</summary>
public static class LoadedSaveValidation
{
    /// <summary>
    ///     Returns true when every creature row carries a decodable, compilable
    ///     genome. <paramref name="error" /> names the first invalid row.
    /// </summary>
    public static bool TryValidateCreatures(
        IEnumerable<(string Name, string SerializedGenome)> creatures,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(creatures);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, serializedGenome) in creatures)
        {
            if (!seen.Add(serializedGenome))
                continue;
            if (CreatureGenome.TryDecode(serializedGenome, out _))
                continue;
            error = $"creature '{name}' has an unreadable genome";
            return false;
        }

        error = null;
        return true;
    }
}
