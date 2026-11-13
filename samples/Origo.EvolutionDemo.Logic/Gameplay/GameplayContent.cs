using System;
using System.Collections.Generic;
using System.Linq;

namespace Origo.EvolutionDemo.Logic.Gameplay;

/// <summary>Stable content entry (gene kind, organ, interface, or mechanic).</summary>
public sealed record GameplayContentEntry(string Id, string Kind, string UnlockId);

/// <summary>One mutually exclusive implementation inside a mechanic group.</summary>
public sealed record MechanicVariant(
    string Id,
    string GroupId,
    string HandlerId,
    string? StrategyIndex,
    string UnlockId,
    IReadOnlyList<string> BiomeTags,
    int Priority,
    IReadOnlyDictionary<string, string> Data);

/// <summary>One unlock node in the progression tree.</summary>
public sealed record UnlockDefinition(
    string Id,
    string Title,
    string Category,
    long Cost,
    IReadOnlyList<string> Prerequisites,
    IReadOnlyList<string> Grants,
    IReadOnlyList<string> RequiredMilestones);

/// <summary>Declarative milestone definition.</summary>
public sealed record MilestoneDefinition(
    string Id,
    string EventKey,
    double Threshold,
    long RewardPoints,
    IReadOnlyList<string> Grants);

/// <summary>Immutable gameplay content snapshot applied atomically at a tick boundary.</summary>
public sealed class GameplayContentSnapshot(
    IReadOnlyDictionary<string, GameplayContentEntry> content,
    IReadOnlyDictionary<string, UnlockDefinition> unlocks,
    IReadOnlyDictionary<string, MechanicVariant> variants,
    IReadOnlyDictionary<string, MilestoneDefinition> milestones)
{
    public IReadOnlyDictionary<string, GameplayContentEntry> Content { get; } = content;
    public IReadOnlyDictionary<string, UnlockDefinition> Unlocks { get; } = unlocks;
    public IReadOnlyDictionary<string, MechanicVariant> Variants { get; } = variants;
    public IReadOnlyDictionary<string, MilestoneDefinition> Milestones { get; } = milestones;

    public static GameplayContentSnapshot Empty { get; } = new(
        new Dictionary<string, GameplayContentEntry>(),
        new Dictionary<string, UnlockDefinition>(),
        new Dictionary<string, MechanicVariant>(),
        new Dictionary<string, MilestoneDefinition>());
}

/// <summary>Mutable builder used by startup modules and content-pack loading.</summary>
public sealed class GameplayContentRegistry
{
    private readonly Dictionary<string, GameplayContentEntry> _content = [];
    private readonly Dictionary<string, UnlockDefinition> _unlocks = [];
    private readonly Dictionary<string, MechanicVariant> _variants = [];
    private readonly Dictionary<string, MilestoneDefinition> _milestones = [];

    public void AddContent(GameplayContentEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!_content.TryAdd(entry.Id, entry))
            throw new InvalidOperationException($"Duplicate content id '{entry.Id}'.");
    }

    public void AddUnlock(UnlockDefinition unlock)
    {
        ArgumentNullException.ThrowIfNull(unlock);
        if (!_unlocks.TryAdd(unlock.Id, unlock))
            throw new InvalidOperationException($"Duplicate unlock id '{unlock.Id}'.");
    }

    public void AddVariant(MechanicVariant variant)
    {
        ArgumentNullException.ThrowIfNull(variant);
        if (!_variants.TryAdd(variant.Id, variant))
            throw new InvalidOperationException($"Duplicate mechanic variant id '{variant.Id}'.");
    }

    public void AddMilestone(MilestoneDefinition milestone)
    {
        ArgumentNullException.ThrowIfNull(milestone);
        if (!_milestones.TryAdd(milestone.Id, milestone))
            throw new InvalidOperationException($"Duplicate milestone id '{milestone.Id}'.");
    }

    public GameplayContentSnapshot BuildSnapshot()
    {
        Validate();
        return new GameplayContentSnapshot(
            new Dictionary<string, GameplayContentEntry>(_content, StringComparer.Ordinal),
            new Dictionary<string, UnlockDefinition>(_unlocks, StringComparer.Ordinal),
            new Dictionary<string, MechanicVariant>(_variants, StringComparer.Ordinal),
            new Dictionary<string, MilestoneDefinition>(_milestones, StringComparer.Ordinal));
    }

    private void Validate()
    {
        foreach (var variant in _variants.Values)
        {
            if (string.IsNullOrWhiteSpace(variant.GroupId) || string.IsNullOrWhiteSpace(variant.HandlerId))
                throw new InvalidOperationException($"Variant '{variant.Id}' needs group and handler ids.");
            if (!_unlocks.ContainsKey(variant.UnlockId))
                throw new InvalidOperationException(
                    $"Variant '{variant.Id}' references missing unlock '{variant.UnlockId}'.");
        }

        foreach (var unlock in _unlocks.Values)
        {
            foreach (var prerequisite in unlock.Prerequisites)
                if (!_unlocks.ContainsKey(prerequisite))
                    throw new InvalidOperationException(
                        $"Unlock '{unlock.Id}' references missing prerequisite '{prerequisite}'.");
            foreach (var grant in unlock.Grants)
                if (!_content.ContainsKey(grant))
                    throw new InvalidOperationException(
                        $"Unlock '{unlock.Id}' references missing content grant '{grant}'.");
        }

        foreach (var milestone in _milestones.Values)
            foreach (var grant in milestone.Grants)
                if (!_content.ContainsKey(grant))
                    throw new InvalidOperationException(
                        $"Milestone '{milestone.Id}' references missing grant '{grant}'.");
    }
}
