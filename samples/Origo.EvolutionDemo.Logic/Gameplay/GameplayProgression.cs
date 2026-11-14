using System;
using System.Collections.Generic;
using System.Linq;

namespace Origo.EvolutionDemo.Logic.Gameplay;

/// <summary>Persistent idle-game progression state.</summary>
public sealed class GameplayProgression
{
    /// <summary>Placeholder EP balance used while real economy tuning is deferred.</summary>
    public const long PlaceholderPoints = 1_000_000_000_000_000_000L;

    public long Points { get; set; } = PlaceholderPoints;
    public double LifetimeSeconds { get; set; }
    public HashSet<string> UnlockedIds { get; } = new(StringComparer.Ordinal);
    public HashSet<string> CompletedMilestones { get; } = new(StringComparer.Ordinal);
}

/// <summary>Unlock tree and milestone gate over one immutable content snapshot.</summary>
public sealed class UnlockService(GameplayContentSnapshot content, GameplayProgression progression)
{
    private readonly GameplayProgression _progression = progression;

    public GameplayContentSnapshot Content { get; } = content;

    public bool IsUnlocked(string unlockId) => _progression.UnlockedIds.Contains(unlockId);

    public bool CanUnlock(string unlockId)
    {
        if (!Content.Unlocks.TryGetValue(unlockId, out var definition))
            return false;
        if (IsUnlocked(unlockId))
            return false;
        if (definition.Prerequisites.Any(prerequisite => !IsUnlocked(prerequisite)))
            return false;
        if (definition.RequiredMilestones.Any(milestone => !_progression.CompletedMilestones.Contains(milestone)))
            return false;
        return _progression.Points >= definition.Cost;
    }

    public bool TryUnlock(string unlockId)
    {
        if (!CanUnlock(unlockId))
            return false;
        var definition = Content.Unlocks[unlockId];
        _progression.Points -= definition.Cost;
        _progression.UnlockedIds.Add(unlockId);
        return true;
    }

    public bool IsContentAvailable(string contentId) =>
        Content.Content.TryGetValue(contentId, out var entry)
        && IsUnlocked(entry.UnlockId);
}
