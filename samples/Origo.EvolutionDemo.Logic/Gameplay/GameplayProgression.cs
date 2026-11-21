using System;
using System.Collections.Generic;
using System.Linq;

namespace Origo.EvolutionDemo.Logic.Gameplay;

/// <summary>How the demo treats progression-gated content.</summary>
public enum GameplayMode
{
    /// <summary>Primordial start with milestones/unlocks.</summary>
    Campaign = 0,

    /// <summary>Full sandbox content, used by benchmarks and free play.</summary>
    Sandbox = 1,
}

/// <summary>Persistent account-wide progression state.</summary>
public sealed class GameplayProgression
{
    /// <summary>Passive evolution points awarded per completed cycle.</summary>
    public const long PassivePointsPerCycle = 1;

    /// <summary>Seconds of simulated life needed for one passive point.</summary>
    public const double PassivePointIntervalSeconds = 30d;

    public long Points { get; set; }
    public double LifetimeSeconds { get; set; }
    public double PassivePointProgress { get; set; }
    public GameplayMode Mode { get; set; } = GameplayMode.Sandbox;
    public HashSet<string> UnlockedIds { get; } = new(StringComparer.Ordinal);
    public HashSet<string> CompletedMilestones { get; } = new(StringComparer.Ordinal);

    /// <summary>Unlocks waiting for an innovator injection, if any.</summary>
    public List<string> PendingInnovationUnlocks { get; } = [];
}

/// <summary>Unlock tree and milestone gate over one immutable content snapshot.</summary>
public sealed class UnlockService(GameplayContentSnapshot content, GameplayProgression progression)
{
    private readonly GameplayProgression _progression = progression;

    public GameplayContentSnapshot Content { get; } = content;

    public bool IsUnlocked(string unlockId)
    {
        if (_progression.UnlockedIds.Contains(unlockId))
            return true;
        // Sandbox is the historical benchmark ecosystem: the optional
        // enrichment spawn policy remains off unless explicitly purchased.
        if (_progression.Mode != GameplayMode.Sandbox)
            return false;
        return !string.Equals(unlockId, "unlock.spawn.enrichment", StringComparison.Ordinal);
    }

    public bool CanUnlock(string unlockId)
    {
        if (_progression.Mode == GameplayMode.Sandbox)
            return false;
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

    public bool IsContentAvailable(string contentId)
    {
        if (Content.Content.TryGetValue(contentId, out var entry)
            && _progression.UnlockedIds.Contains(entry.UnlockId))
            return true;
        if (_progression.Mode != GameplayMode.Sandbox)
            return false;
        return !string.Equals(contentId, "mechanic.spawn.enrichment", StringComparison.Ordinal);
    }
}
