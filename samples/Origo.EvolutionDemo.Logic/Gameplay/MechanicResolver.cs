using System;
using System.Collections.Generic;
using System.Linq;

namespace Origo.EvolutionDemo.Logic.Gameplay;

/// <summary>Resolves exactly one variant inside a mutually exclusive mechanic group.</summary>
public sealed class MechanicResolver(GameplayContentSnapshot content, UnlockService unlocks)
{
    private readonly GameplayContentSnapshot _content = content;
    private readonly UnlockService _unlocks = unlocks;

    public MechanicVariant? Resolve(string groupId, string? biomeTag = null)
    {
        var candidates = _content.Variants.Values
            .Where(variant => string.Equals(variant.GroupId, groupId, StringComparison.Ordinal))
            .Where(variant => _unlocks.IsUnlocked(variant.UnlockId))
            .Where(variant => biomeTag is null
                || variant.BiomeTags.Count == 0
                || variant.BiomeTags.Contains(biomeTag, StringComparer.Ordinal))
            .OrderByDescending(static variant => variant.Priority)
            .ThenBy(static variant => variant.Id, StringComparer.Ordinal)
            .ToList();
        return candidates.Count == 0 ? null : candidates[0];
    }
}
