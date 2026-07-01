using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;

namespace Origo.Core.Snd.Strategy;

internal static class ObserverStrategyMetadata
{
    private static readonly Dictionary<Type, IReadOnlyCollection<string>> Cache = [];

    internal static IReadOnlyCollection<string> GetDataKeys(Type observerStrategyType)
    {
        if (Cache.TryGetValue(observerStrategyType, out var cached))
            return cached;

        var attributes = observerStrategyType.GetCustomAttributes<ObserveDataAttribute>(false);
        if (attributes is null)
        {
            Cache[observerStrategyType] = [];
            return Cache[observerStrategyType];
        }

        var keys = new List<string>();
        foreach (var attr in attributes)
            if (!string.IsNullOrWhiteSpace(attr.DataKey) && !keys.Contains(attr.DataKey))
                keys.Add(attr.DataKey);

        var result = keys.AsReadOnly();
        Cache[observerStrategyType] = result;
        return result;
    }
}
