using System;
using System.Collections.Generic;
using System.Linq;

namespace Origo.EvolutionDemo.Logic.Gameplay;

/// <summary>A typed mechanic handler that content variants may reference.</summary>
public interface IGameplayHandler<in TContext>
{
    string Id { get; }
    void Execute(TContext context);
}

/// <summary>Startup registry for one handler family (death, spawn, ...).</summary>
public sealed class GameplayHandlerRegistry<TContext>
{
    private readonly Dictionary<string, IGameplayHandler<TContext>> _handlers =
        new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> HandlerIds => _handlers.Keys;

    public void Register(IGameplayHandler<TContext> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (!_handlers.TryAdd(handler.Id, handler))
            throw new InvalidOperationException($"Duplicate handler '{handler.Id}'.");
    }

    public IGameplayHandler<TContext> Require(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _handlers.TryGetValue(id, out var handler)
            ? handler
            : throw new InvalidOperationException($"Unknown gameplay handler '{id}'.");
    }

    /// <summary>Requires every content variant in a group to resolve to a handler.</summary>
    public void Validate(GameplayContentSnapshot content, string groupId)
    {
        ArgumentNullException.ThrowIfNull(content);
        foreach (var variant in content.Variants.Values
                     .Where(variant => string.Equals(variant.GroupId, groupId, StringComparison.Ordinal)))
            _ = Require(variant.HandlerId);
    }
}
