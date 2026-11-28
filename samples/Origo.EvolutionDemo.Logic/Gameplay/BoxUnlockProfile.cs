using System;
using System.Collections.Generic;
using System.Linq;

namespace Origo.EvolutionDemo.Logic.Gameplay;

/// <summary>
///     Immutable unlock set for one ecology box. A box can allow every unlock or
///     any explicit subset; no global account progression participates.
/// </summary>
public sealed record BoxUnlockProfile
{
    private BoxUnlockProfile(string id, IReadOnlyList<string> unlockIds)
    {
        Id = id;
        UnlockIds = unlockIds;
        UnlockSet = unlockIds.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Stable profile id.</summary>
    public string Id { get; }

    /// <summary>Sorted unlock ids enabled in the box.</summary>
    public IReadOnlyList<string> UnlockIds { get; }

    /// <summary>Set form of <see cref="UnlockIds" />.</summary>
    public IReadOnlySet<string> UnlockSet { get; }

    /// <summary>Creates a validated profile; ids are sorted and must be non-empty.</summary>
    /// <exception cref="ArgumentException"><paramref name="id" /> or the unlock list is empty.</exception>
    public static BoxUnlockProfile Create(string id, IEnumerable<string> unlockIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(unlockIds);
        var sorted = unlockIds
            .Where(static unlock => !string.IsNullOrWhiteSpace(unlock))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static unlock => unlock, StringComparer.Ordinal)
            .ToArray();
        if (sorted.Length == 0)
            throw new ArgumentException("A box profile needs at least one unlock.", nameof(unlockIds));
        return new BoxUnlockProfile(id, sorted);
    }

    /// <summary>Creates a profile containing every unlock in a content snapshot.</summary>
    public static BoxUnlockProfile All(GameplayContentSnapshot content, string id = "all")
    {
        ArgumentNullException.ThrowIfNull(content);
        return Create(id, content.Unlocks.Keys);
    }

    /// <summary>Whether this profile enables the unlock.</summary>
    public bool Allows(string unlockId) => UnlockSet.Contains(unlockId);
}
