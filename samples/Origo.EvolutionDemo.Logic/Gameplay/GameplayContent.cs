using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Origo.EvolutionDemo.Logic.Gameplay;

/// <summary>Stable content entry (gene kind, organ, interface, or mechanic).</summary>
public sealed record GameplayContentEntry(string Id, string Kind, string UnlockId);

/// <summary>Code handler contract registered by a startup module.</summary>
public sealed record GameplayHandlerDefinition(string Id, IReadOnlyList<string> RequiredNumericData);

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
    IReadOnlyDictionary<string, MilestoneDefinition> milestones,
    IReadOnlyDictionary<string, GameplayHandlerDefinition> handlers)
{
    public IReadOnlyDictionary<string, GameplayContentEntry> Content { get; } = content;
    public IReadOnlyDictionary<string, UnlockDefinition> Unlocks { get; } = unlocks;
    public IReadOnlyDictionary<string, MechanicVariant> Variants { get; } = variants;
    public IReadOnlyDictionary<string, MilestoneDefinition> Milestones { get; } = milestones;
    public IReadOnlyDictionary<string, GameplayHandlerDefinition> Handlers { get; } = handlers;

    public static GameplayContentSnapshot Empty { get; } = new(
        new Dictionary<string, GameplayContentEntry>(),
        new Dictionary<string, UnlockDefinition>(),
        new Dictionary<string, MechanicVariant>(),
        new Dictionary<string, MilestoneDefinition>(),
        new Dictionary<string, GameplayHandlerDefinition>());
}

/// <summary>Mutable builder used by startup modules and content-pack loading.</summary>
public sealed class GameplayContentRegistry
{
    private readonly Dictionary<string, GameplayContentEntry> _content = [];
    private readonly Dictionary<string, UnlockDefinition> _unlocks = [];
    private readonly Dictionary<string, MechanicVariant> _variants = [];
    private readonly Dictionary<string, MilestoneDefinition> _milestones = [];
    private readonly Dictionary<string, GameplayHandlerDefinition> _handlers = [];

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

    public void AddHandler(GameplayHandlerDefinition handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (!_handlers.TryAdd(handler.Id, handler))
            throw new InvalidOperationException($"Duplicate gameplay handler '{handler.Id}'.");
    }

    public void AddMilestone(MilestoneDefinition milestone)
    {
        ArgumentNullException.ThrowIfNull(milestone);
        if (!_milestones.TryAdd(milestone.Id, milestone))
            throw new InvalidOperationException($"Duplicate milestone id '{milestone.Id}'.");
    }

    public void AddSnapshot(GameplayContentSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        foreach (var entry in snapshot.Content.Values)
            AddContent(entry);
        foreach (var unlock in snapshot.Unlocks.Values)
            AddUnlock(unlock);
        foreach (var variant in snapshot.Variants.Values)
            AddVariant(variant);
        foreach (var milestone in snapshot.Milestones.Values)
            AddMilestone(milestone);
        foreach (var handler in snapshot.Handlers.Values)
            AddHandler(handler);
    }

    public GameplayContentSnapshot BuildSnapshot()
    {
        Validate();
        return new GameplayContentSnapshot(
            new Dictionary<string, GameplayContentEntry>(_content, StringComparer.Ordinal),
            new Dictionary<string, UnlockDefinition>(_unlocks, StringComparer.Ordinal),
            new Dictionary<string, MechanicVariant>(_variants, StringComparer.Ordinal),
            new Dictionary<string, MilestoneDefinition>(_milestones, StringComparer.Ordinal),
            new Dictionary<string, GameplayHandlerDefinition>(_handlers, StringComparer.Ordinal));
    }

    private void Validate()
    {
        foreach (var entry in _content.Values)
        {
            if (string.IsNullOrWhiteSpace(entry.UnlockId) || !_unlocks.ContainsKey(entry.UnlockId))
                throw new InvalidOperationException(
                    $"Content '{entry.Id}' references missing unlock '{entry.UnlockId}'.");
        }

        foreach (var variant in _variants.Values)
        {
            if (string.IsNullOrWhiteSpace(variant.GroupId) || string.IsNullOrWhiteSpace(variant.HandlerId))
                throw new InvalidOperationException($"Variant '{variant.Id}' needs group and handler ids.");
            if (!_unlocks.ContainsKey(variant.UnlockId))
                throw new InvalidOperationException(
                    $"Variant '{variant.Id}' references missing unlock '{variant.UnlockId}'.");
            if (!_handlers.TryGetValue(variant.HandlerId, out var handler))
                throw new InvalidOperationException(
                    $"Variant '{variant.Id}' references unknown handler '{variant.HandlerId}'.");
            foreach (var key in handler.RequiredNumericData)
            {
                if (!variant.Data.TryGetValue(key, out var raw)
                    || !double.TryParse(raw, NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                    throw new InvalidOperationException(
                        $"Variant '{variant.Id}' handler '{handler.Id}' needs numeric data '{key}'.");
            }
        }

        foreach (var unlock in _unlocks.Values)
        {
            foreach (var prerequisite in unlock.Prerequisites)
                if (!_unlocks.ContainsKey(prerequisite))
                    throw new InvalidOperationException(
                        $"Unlock '{unlock.Id}' references missing prerequisite '{prerequisite}'.");
            foreach (var milestone in unlock.RequiredMilestones)
                if (!_milestones.ContainsKey(milestone))
                    throw new InvalidOperationException(
                        $"Unlock '{unlock.Id}' references missing milestone '{milestone}'.");
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
