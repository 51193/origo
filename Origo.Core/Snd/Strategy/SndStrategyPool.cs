using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Logging;

namespace Origo.Core.Snd.Strategy;

/// <summary>
///     Pool responsible for managing and reusing strategy instances by index.
///     Does not use reflection-based auto-collection; instead, explicit registration
///     is used to improve controllability and testability. Only allows acquiring instances
///     via the generic <see cref="GetStrategy{TBase}" />; if the instance type corresponding
///     to the index does not match the generic type parameter, an exception is thrown with no fallback.
/// </summary>
internal sealed class SndStrategyPool
{
    private readonly Dictionary<string, Func<BaseStrategy>> _factories = [];
    private readonly ILogger _logger;
    private readonly Dictionary<string, BaseStrategy> _pool = [];
    private readonly Dictionary<string, int> _priorities = [];
    private readonly Dictionary<string, int> _refCounts = [];

    public SndStrategyPool(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public void Register(Type strategyType, Func<BaseStrategy> factory)
    {
        ArgumentNullException.ThrowIfNull(strategyType);
        if (strategyType.IsAbstract || !strategyType.IsSealed)
            throw new InvalidOperationException(
                $"Strategy type '{strategyType.FullName}' must be sealed. " +
                "Shared pooled strategies are singletons and must not be inheritable.");
        ValidateStrategyType(strategyType, out var invalidMembers);
        if (invalidMembers.Length > 0)
            throw new InvalidOperationException(
                $"Strategy type '{strategyType.FullName}' declares invalid instance members ({invalidMembers}); " +
                "shared pooled strategies must be stateless.");
        var index = ResolveRequiredIndex(strategyType);
        ArgumentNullException.ThrowIfNull(factory);
        if (_factories.ContainsKey(index))
            throw new InvalidOperationException(
                $"Strategy index '{index}' is already registered. " +
                "Each strategy index must map to exactly one strategy type.");
        _factories[index] = factory;
        _priorities[index] = ResolvePriority(strategyType);
    }

    public void Register<TStrategy>(Func<TStrategy> factory) where TStrategy : BaseStrategy
    {
        ArgumentNullException.ThrowIfNull(factory);
        Register(typeof(TStrategy), () => factory());
    }

    internal bool IsRegistered(string index) => _factories.ContainsKey(index);

    internal IReadOnlyCollection<string> EnumerateRegisteredIndices() => _factories.Keys;

    public TBase GetStrategy<TBase>(string index) where TBase : BaseStrategy
    {
        if (_pool.TryGetValue(index, out var strategy))
        {
            if (strategy is not TBase typed)
                throw new InvalidOperationException(
                    $"Strategy '{index}' instance type '{strategy.GetType().FullName}' is not assignable to '{typeof(TBase).FullName}'.");
            _refCounts[index]++;
            return typed;
        }

        if (_factories.TryGetValue(index, out var factory))
        {
            strategy = factory() ?? throw new InvalidOperationException(
                $"Strategy factory for '{index}' returned null. Strategy factories must return a non-null strategy instance.");
            if (strategy is not TBase typed)
                throw new InvalidOperationException(
                    $"Strategy '{index}' instance type '{strategy.GetType().FullName}' is not assignable to '{typeof(TBase).FullName}'.");
            _pool[index] = strategy;
            _refCounts[index] = 1;
            _logger.Log(LogLevel.Debug, nameof(SndStrategyPool),
                new LogMessageBuilder().AddContext("strategyIndex", index).Build("Created new strategy instance."));
            return typed;
        }

        throw new InvalidOperationException($"Strategy factory for '{index}' not found.");
    }

    public void ReleaseStrategy(string index)
    {
        if (!_refCounts.TryGetValue(index, out var count))
            throw new InvalidOperationException(
                $"Cannot release strategy '{index}': not acquired or already fully released.");

        count--;
        if (count == 0)
        {
            _pool.Remove(index);
            _refCounts.Remove(index);
            _logger.Log(LogLevel.Debug, nameof(SndStrategyPool),
                new LogMessageBuilder().AddContext("strategyIndex", index).Build("Released strategy instance."));
        }
        else
        {
            _refCounts[index] = count;
        }
    }

    internal int GetPriority(string index) =>
        _priorities.TryGetValue(index, out var priority) ? priority : 0;

    /// <summary>
    ///     Emits a warning for every strategy whose pool reference count is
    ///     still non-zero. Called by SndContext workflow teardown so leaked
    ///     strategy references stay observable in production logs, not only
    ///     in test assertions.
    /// </summary>
    internal void LogPoolLeaks()
    {
        foreach (var (index, count) in _refCounts)
        {
            if (count > 0)
            {
                _logger.Log(LogLevel.Warning, nameof(SndStrategyPool),
                    new LogMessageBuilder()
                        .AddContext("strategyIndex", index)
                        .AddContext("refCount", count)
                        .Build("Strategy leak detected — non-zero reference count at teardown."));
            }
        }
    }

    private static string ResolveRequiredIndex(Type strategyType)
    {
        var attr = strategyType.GetCustomAttribute<StrategyIndexAttribute>() ?? throw new InvalidOperationException(
                $"Strategy type '{strategyType.FullName}' must declare [StrategyIndex(\"...\")].");
        if (string.IsNullOrWhiteSpace(attr.Index))
            throw new InvalidOperationException(
                $"Strategy type '{strategyType.FullName}' has an empty StrategyIndexAttribute value.");
        return attr.Index;
    }

    internal static bool ValidateStrategyType(Type strategyType, out string invalidMembers)
    {
        ArgumentNullException.ThrowIfNull(strategyType);
        var names = new List<string>();
        var baseType = typeof(BaseStrategy);
        var current = strategyType;
        while (current is not null && current != baseType && current != typeof(object))
        {
            var fields = current.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(f => !f.IsStatic && !f.IsInitOnly)
                .Select(f => $"{current.Name}.{f.Name}");
            names.AddRange(fields);

            var writableProperties = current.GetProperties(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(p => p.SetMethod is { IsStatic: false })
                .Select(p => $"{current.Name}.{p.Name}");
            names.AddRange(writableProperties);

            current = current.BaseType;
        }

        invalidMembers = string.Join(", ", names);
        return names.Count == 0;
    }

    private static int ResolvePriority(Type strategyType)
    {
        var attr = strategyType.GetCustomAttribute<StrategyIndexAttribute>();
        return attr?.Priority ?? StrategyIndexAttribute.DefaultPriority;
    }
}
