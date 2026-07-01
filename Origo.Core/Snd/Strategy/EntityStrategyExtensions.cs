using System;
using Origo.Core.Abstractions.Entity;

namespace Origo.Core.Snd.Strategy;

/// <summary>
///     Extension methods for <see cref="ISndEntity" /> strategy management.
/// </summary>
public static class EntityStrategyExtensions
{
    /// <summary>
    ///     Ensures a replaceable resident strategy is present on the entity.
    ///     Reads <paramref name="implKey" /> from entity data to check for a configured
    ///     override (e.g. set by a template), falling back to <paramref name="defaultStrategyIndex" />.
    ///     Uses <see cref="ISndEntity.EnsureStrategy" /> internally for deduplication.
    /// </summary>
    /// <param name="entity">The target entity.</param>
    /// <param name="implKey">
    ///     The data key storing the configured strategy index. Also used as the deduplication marker.
    /// </param>
    /// <param name="defaultStrategyIndex">The default strategy index when no override is configured.</param>
    /// <returns><c>true</c> if the strategy was newly added, <c>false</c> if it was already present.</returns>
    public static bool EnsureReplaceableStrategy(this ISndEntity entity, string implKey, string defaultStrategyIndex)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(implKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultStrategyIndex);

        var (foundConfigured, configuredIndex) = entity.TryGetData<string>(implKey);
        var effectiveIndex = (foundConfigured && !string.IsNullOrWhiteSpace(configuredIndex))
            ? configuredIndex
            : defaultStrategyIndex;

        return ActiveStrategyExtensions.EnsureStrategy(entity, implKey, effectiveIndex);
    }
}
