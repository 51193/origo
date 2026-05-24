using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Logging;

namespace Origo.Core.Snd.Strategy;

/// <summary>
///     负责按索引管理与复用策略实例的池。
///     不使用反射自动收集，而是通过显式注册提升可控性与可测试性。
///     仅允许通过泛型 <see cref="GetStrategy{TBase}" /> 获取实例；若索引对应的实例类型与泛型参数不匹配则抛异常，不做兜底。
/// </summary>
internal sealed class SndStrategyPool
{
    private readonly Dictionary<string, Func<BaseStrategy>> _factories = new();
    private readonly ILogger _logger;
    private readonly Dictionary<string, BaseStrategy> _pool = new();
    private readonly Dictionary<string, int> _refCounts = new();

    public SndStrategyPool(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public void Register(Type strategyType, Func<BaseStrategy> factory)
    {
        ArgumentNullException.ThrowIfNull(strategyType);
        ValidateStrategyType(strategyType, out var invalidMembers);
        if (invalidMembers.Length > 0)
            throw new InvalidOperationException(
                $"Strategy type '{strategyType.FullName}' declares invalid instance members ({invalidMembers}); " +
                "shared pooled strategies must be stateless.");
        var index = ResolveRequiredIndex(strategyType);
        ArgumentNullException.ThrowIfNull(factory);
        _factories[index] = factory;
    }

    public void Register<TStrategy>(Func<TStrategy> factory) where TStrategy : BaseStrategy
    {
        ArgumentNullException.ThrowIfNull(factory);
        Register(typeof(TStrategy), () => factory());
    }

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
            strategy = factory();
            if (strategy is not TBase typed)
                throw new InvalidOperationException(
                    $"Strategy '{index}' instance type '{strategy.GetType().FullName}' is not assignable to '{typeof(TBase).FullName}'.");
            _pool[index] = strategy;
            _refCounts[index] = 1;
            _logger.Log(LogLevel.Info, nameof(SndStrategyPool),
                new LogMessageBuilder().AddSuffix("strategyIndex", index).Build("Created new strategy instance."));
            return typed;
        }

        throw new InvalidOperationException($"Strategy factory for '{index}' not found.");
    }

    private static TBase CastOrThrow<TBase>(BaseStrategy strategy, string index) where TBase : BaseStrategy
    {
        if (strategy is TBase typed) return typed;

        throw new InvalidOperationException(
            $"Strategy '{index}' instance type '{strategy.GetType().FullName}' is not assignable to '{typeof(TBase).FullName}'.");
    }

    public void ReleaseStrategy(string index)
    {
        if (!_refCounts.TryGetValue(index, out var count))
            throw new InvalidOperationException(
                $"Cannot release strategy '{index}': not acquired or already fully released.");

        count--;
        if (count < 0)
            throw new InvalidOperationException(
                $"Reference count for strategy '{index}' went below zero (double release).");

        if (count == 0)
        {
            _pool.Remove(index);
            _refCounts.Remove(index);
            _logger.Log(LogLevel.Info, nameof(SndStrategyPool),
                new LogMessageBuilder().AddSuffix("strategyIndex", index).Build("Released strategy instance."));
        }
        else
        {
            _refCounts[index] = count;
        }
    }

    private static string ResolveRequiredIndex(Type strategyType)
    {
        var attr = strategyType.GetCustomAttribute<StrategyIndexAttribute>();
        if (attr is null)
            throw new InvalidOperationException(
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
                .Where(f => !f.IsStatic)
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
}
