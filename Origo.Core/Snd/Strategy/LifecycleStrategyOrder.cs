using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Origo.Core.Snd.Strategy;

/// <summary>
///     Validates startup ordering declarations and builds a deterministic order over
///     the complete lifecycle registry. Entity managers project this order onto their
///     mounted strategies, retaining constraints through unmounted intermediate types.
/// </summary>
internal static class LifecycleStrategyOrder
{
    internal sealed record Declaration(bool IsLifecycle, string[] Before, string[] After);

    internal static Declaration ReadDeclaration(Type type, string index)
    {
        var attribute = type.GetCustomAttribute<StrategyIndexAttribute>()!;
        var before = ValidateIndices(attribute.Before, index, nameof(attribute.Before));
        var after = ValidateIndices(attribute.After, index, nameof(attribute.After));
        var isLifecycle = typeof(LifecycleStrategyBase).IsAssignableFrom(type);
        if (!isLifecycle && (before.Length > 0 || after.Length > 0))
            throw new InvalidOperationException($"Strategy '{index}' is not a lifecycle strategy and cannot declare ordering constraints.");
        return new Declaration(isLifecycle, before, after);
    }

    private static string[] ValidateIndices(string[] indices, string owner, string relation)
    {
        if (indices is null)
            throw new InvalidOperationException($"Strategy '{owner}' has a null {relation} declaration.");
        foreach (var index in indices)
        {
            if (string.IsNullOrWhiteSpace(index))
                throw new InvalidOperationException($"Strategy '{owner}' has a blank {relation} target.");
            if (index == owner)
                throw new InvalidOperationException($"Strategy '{owner}' cannot order itself through {relation}.");
        }
        return [.. indices.Distinct(StringComparer.Ordinal)];
    }

    internal static Dictionary<string, int> Build(IReadOnlyDictionary<string, Declaration> declarations)
    {
        var edges = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var incoming = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (index, declaration) in declarations)
            if (declaration.IsLifecycle)
            {
                edges.Add(index, new SortedSet<string>(StringComparer.Ordinal));
                incoming.Add(index, 0);
            }

        foreach (var (index, declaration) in declarations.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            foreach (var target in declaration.Before)
                AddEdge(index, target, index, target);
            foreach (var target in declaration.After)
                AddEdge(target, index, index, target);
        }

        var ready = new SortedSet<string>(incoming.Where(pair => pair.Value == 0).Select(pair => pair.Key), StringComparer.Ordinal);
        var order = new Dictionary<string, int>(StringComparer.Ordinal);
        while (ready.Count > 0)
        {
            var index = ready.Min!;
            ready.Remove(index);
            order.Add(index, order.Count);
            foreach (var next in edges[index])
                if (--incoming[next] == 0)
                    ready.Add(next);
        }
        if (order.Count != edges.Count)
            throw new InvalidOperationException($"Lifecycle strategy ordering cycle: {FindCycle(edges)}.");
        return order;

        void AddEdge(string from, string to, string owner, string target)
        {
            if (!declarations.TryGetValue(target, out var referenced))
                throw new InvalidOperationException($"Lifecycle strategy '{owner}' references unregistered ordering target '{target}'.");
            if (!referenced.IsLifecycle)
                throw new InvalidOperationException($"Lifecycle strategy '{owner}' references non-lifecycle ordering target '{target}'.");
            if (edges[from].Add(to))
                incoming[to]++;
        }
    }

    private static string FindCycle(IReadOnlyDictionary<string, SortedSet<string>> edges)
    {
        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        var path = new List<string>();
        foreach (var index in edges.Keys.Order(StringComparer.Ordinal))
        {
            var cycle = Visit(index);
            if (cycle is not null)
                return cycle;
        }
        throw new InvalidOperationException("An inconsistent ordering graph did not contain a cycle.");

        string? Visit(string index)
        {
            if (state.TryGetValue(index, out var visited))
                return visited == 1 ? string.Join(" -> ", path.Skip(path.IndexOf(index)).Append(index)) : null;
            state[index] = 1;
            path.Add(index);
            foreach (var next in edges[index])
            {
                var cycle = Visit(next);
                if (cycle is not null)
                    return cycle;
            }
            path.RemoveAt(path.Count - 1);
            state[index] = 2;
            return null;
        }
    }
}
